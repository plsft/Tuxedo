using System.Data;
using Bowtie.Models;
using Tuxedo;

namespace Bowtie.Introspection
{
    public class PostgreSqlIntrospector : IDatabaseIntrospector
    {
        public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

        public async Task<List<TableModel>> GetTablesAsync(IDbConnection connection, string? schema = null)
        {
            schema ??= "public";
            
            var sql = @"
                SELECT 
                    schemaname as schema_name,
                    tablename as table_name
                FROM pg_tables
                WHERE schemaname = @Schema
                ORDER BY tablename";

            var tableInfos = await connection.QueryAsync<(string schema_name, string table_name)>(sql, new { Schema = schema });
            var tables = new List<TableModel>();

            foreach (var (schemaName, tableName) in tableInfos)
            {
                var table = new TableModel
                {
                    Name = tableName,
                    Schema = schemaName,
                    ModelType = typeof(object) // Placeholder
                };

                table.Columns.AddRange(await GetColumnsAsync(connection, tableName, schemaName));
                table.Indexes.AddRange(await GetIndexesAsync(connection, tableName, schemaName));
                table.Constraints.AddRange(await GetConstraintsAsync(connection, tableName, schemaName));

                tables.Add(table);
            }

            return tables;
        }

        public async Task<List<ColumnModel>> GetColumnsAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            schema ??= "public";
            
            var sql = @"
                SELECT 
                    c.column_name,
                    c.data_type,
                    c.character_maximum_length,
                    c.numeric_precision,
                    c.numeric_scale,
                    c.is_nullable,
                    c.column_default,
                    CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END as is_identity,
                    c.collation_name
                FROM information_schema.columns c
                WHERE c.table_name = @TableName
                AND c.table_schema = @Schema
                ORDER BY c.ordinal_position";

            var columnInfos = await connection.QueryAsync(sql, new { TableName = tableName, Schema = schema });
            var columns = new List<ColumnModel>();

            foreach (dynamic col in columnInfos)
            {
                var column = new ColumnModel
                {
                    Name = col.column_name,
                    DataType = MapPostgreSqlTypeToNetType(col.data_type, col.character_maximum_length, col.numeric_precision, col.numeric_scale),
                    MaxLength = col.character_maximum_length,
                    Precision = col.numeric_precision,
                    Scale = col.numeric_scale,
                    IsNullable = col.is_nullable == "YES",
                    IsIdentity = col.is_identity,
                    DefaultValue = col.column_default,
                    Collation = col.collation_name,
                    PropertyType = typeof(string), // Default
                    PropertyInfo = null!
                };

                columns.Add(column);
            }

            return columns;
        }

        public async Task<List<IndexModel>> GetIndexesAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            schema ??= "public";
            
            var sql = @"
                SELECT DISTINCT
                    i.relname as index_name,
                    idx.indisunique as is_unique,
                    am.amname as index_type
                FROM pg_class t
                JOIN pg_namespace n ON n.oid = t.relnamespace
                JOIN pg_index idx ON t.oid = idx.indrelid
                JOIN pg_class i ON i.oid = idx.indexrelid
                JOIN pg_am am ON i.relam = am.oid
                WHERE t.relname = @TableName
                AND n.nspname = @Schema
                AND NOT idx.indisprimary -- Exclude primary key
                ORDER BY i.relname";

            var indexInfos = await connection.QueryAsync(sql, new { TableName = tableName, Schema = schema });
            var indexes = new List<IndexModel>();

            foreach (dynamic idx in indexInfos)
            {
                var index = new IndexModel
                {
                    Name = idx.index_name,
                    IsUnique = idx.is_unique,
                    IndexType = MapPostgreSqlIndexType(idx.index_type)
                };

                // Get index columns
                var columnSql = @"
                    SELECT 
                        a.attname as column_name,
                        a.attnum as column_position
                    FROM pg_class t
                    JOIN pg_namespace n ON n.oid = t.relnamespace
                    JOIN pg_index idx ON t.oid = idx.indrelid
                    JOIN pg_class i ON i.oid = idx.indexrelid
                    JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(idx.indkey)
                    WHERE t.relname = @TableName
                    AND n.nspname = @Schema
                    AND i.relname = @IndexName
                    ORDER BY a.attnum";

                var columns = await connection.QueryAsync(columnSql, new { TableName = tableName, Schema = schema, IndexName = idx.index_name });
                
                foreach (dynamic col in columns)
                {
                    index.Columns.Add(new IndexColumnModel
                    {
                        ColumnName = col.column_name,
                        Order = col.column_position,
                        IsDescending = false // PostgreSQL doesn't store this info easily
                    });
                }

                indexes.Add(index);
            }

            return indexes;
        }

        public async Task<List<ConstraintModel>> GetConstraintsAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            schema ??= "public";
            var constraints = new List<ConstraintModel>();

            // Primary Key constraints
            var pkSql = @"
                SELECT 
                    kcu.constraint_name,
                    kcu.column_name
                FROM information_schema.key_column_usage kcu
                JOIN information_schema.table_constraints tc ON kcu.constraint_name = tc.constraint_name
                WHERE tc.table_name = @TableName
                AND tc.table_schema = @Schema
                AND tc.constraint_type = 'PRIMARY KEY'
                ORDER BY kcu.ordinal_position";

            var pkColumns = await connection.QueryAsync(pkSql, new { TableName = tableName, Schema = schema });
            var pkGroups = pkColumns.GroupBy(c => ((dynamic)c).constraint_name);

            foreach (var group in pkGroups)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = group.Key,
                    Type = ConstraintType.PrimaryKey,
                    Columns = group.Select(c => ((dynamic)c).column_name.ToString() as string).Where(s => s != null).Cast<string>().ToList()
                });
            }

            // Foreign Key constraints
            var fkSql = @"
                SELECT 
                    rc.constraint_name,
                    kcu.column_name,
                    ccu.table_name as referenced_table,
                    ccu.column_name as referenced_column,
                    rc.delete_rule,
                    rc.update_rule
                FROM information_schema.referential_constraints rc
                JOIN information_schema.key_column_usage kcu ON rc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage ccu ON rc.unique_constraint_name = ccu.constraint_name
                WHERE kcu.table_name = @TableName
                AND kcu.table_schema = @Schema";

            var fkInfos = await connection.QueryAsync(fkSql, new { TableName = tableName, Schema = schema });

            foreach (dynamic fk in fkInfos)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = fk.constraint_name,
                    Type = ConstraintType.ForeignKey,
                    Columns = new List<string> { fk.column_name },
                    ReferencedTable = fk.referenced_table,
                    ReferencedColumn = fk.referenced_column,
                    OnDelete = MapReferentialAction(fk.delete_rule),
                    OnUpdate = MapReferentialAction(fk.update_rule)
                });
            }

            // Check constraints
            var checkSql = @"
                SELECT 
                    cc.constraint_name,
                    cc.check_clause
                FROM information_schema.check_constraints cc
                JOIN information_schema.table_constraints tc ON cc.constraint_name = tc.constraint_name
                WHERE tc.table_name = @TableName
                AND tc.table_schema = @Schema";

            var checkInfos = await connection.QueryAsync(checkSql, new { TableName = tableName, Schema = schema });

            foreach (dynamic check in checkInfos)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = check.constraint_name,
                    Type = ConstraintType.Check,
                    CheckExpression = check.check_clause
                });
            }

            return constraints;
        }

        public async Task<bool> TableExistsAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            schema ??= "public";
            
            var sql = @"
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_name = @TableName
                AND table_schema = @Schema
                AND table_type = 'BASE TABLE'";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, Schema = schema });
            return count > 0;
        }

        public async Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName, string? schema = null)
        {
            schema ??= "public";
            
            var sql = @"
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_name = @TableName
                AND column_name = @ColumnName
                AND table_schema = @Schema";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, ColumnName = columnName, Schema = schema });
            return count > 0;
        }

        private string MapPostgreSqlTypeToNetType(string pgType, int? maxLength, int? precision, int? scale)
        {
            return pgType.ToLower() switch
            {
                "boolean" => "BOOL",
                "smallint" => "INT16",
                "integer" => "INT32",
                "bigint" => "INT64",
                "real" => "SINGLE",
                "double precision" => "DOUBLE",
                "numeric" or "decimal" => precision.HasValue ? $"DECIMAL({precision},{scale ?? 0})" : "DECIMAL",
                "timestamp" or "timestamp without time zone" => "DATETIME",
                "timestamp with time zone" or "timestamptz" => "DATETIMEOFFSET",
                "interval" => "TIMESPAN",
                "uuid" => "GUID",
                "character varying" or "varchar" or "character" or "char" or "text" => 
                    maxLength.HasValue ? $"STRING({maxLength})" : "STRING",
                "jsonb" => "STRING", // JSONB mapped to string for now
                "json" => "STRING",
                _ => pgType
            };
        }

        private IndexType MapPostgreSqlIndexType(string pgType)
        {
            return pgType?.ToLower() switch
            {
                "btree" => IndexType.BTree,
                "hash" => IndexType.Hash,
                "gin" => IndexType.GIN,
                "gist" => IndexType.GiST,
                "brin" => IndexType.BRIN,
                "spgist" => IndexType.SPGiST,
                _ => IndexType.BTree
            };
        }

        private ReferentialAction MapReferentialAction(string action)
        {
            return action?.ToUpper() switch
            {
                "CASCADE" => ReferentialAction.Cascade,
                "SET NULL" => ReferentialAction.SetNull,
                "SET DEFAULT" => ReferentialAction.SetDefault,
                "RESTRICT" => ReferentialAction.Restrict,
                _ => ReferentialAction.NoAction
            };
        }
    }
}