using System.Data;
using Bowtie.Models;
using Tuxedo;

namespace Bowtie.Introspection
{
    public class SqlServerIntrospector : IDatabaseIntrospector
    {
        public DatabaseProvider Provider => DatabaseProvider.SqlServer;

        public async Task<List<TableModel>> GetTablesAsync(IDbConnection connection, string? schema = null)
        {
            var sql = @"
                SELECT 
                    t.TABLE_SCHEMA as SchemaName,
                    t.TABLE_NAME as TableName
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                AND (@Schema IS NULL OR t.TABLE_SCHEMA = @Schema)
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            var tableInfos = await connection.QueryAsync<(string SchemaName, string TableName)>(sql, new { Schema = schema });
            var tables = new List<TableModel>();

            foreach (var (schemaName, tableName) in tableInfos)
            {
                var table = new TableModel
                {
                    Name = tableName,
                    Schema = schemaName,
                    ModelType = typeof(object) // Placeholder since we don't have the actual type
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
            var sql = @"
                SELECT 
                    c.COLUMN_NAME as ColumnName,
                    c.DATA_TYPE as DataType,
                    c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                    c.NUMERIC_PRECISION as Precision,
                    c.NUMERIC_SCALE as Scale,
                    CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IsNullable,
                    CASE WHEN c.COLUMN_DEFAULT IS NOT NULL THEN 1 ELSE 0 END as HasDefault,
                    c.COLUMN_DEFAULT as DefaultValue,
                    CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END as IsIdentity,
                    c.COLLATION_NAME as Collation
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @TableName
                AND (@Schema IS NULL OR c.TABLE_SCHEMA = @Schema)
                ORDER BY c.ORDINAL_POSITION";

            var columnInfos = await connection.QueryAsync(sql, new { TableName = tableName, Schema = schema });
            var columns = new List<ColumnModel>();

            foreach (dynamic col in columnInfos)
            {
                var column = new ColumnModel
                {
                    Name = col.ColumnName,
                    DataType = MapSqlServerTypeToNetType(col.DataType, col.MaxLength, col.Precision, col.Scale),
                    MaxLength = col.MaxLength,
                    Precision = col.Precision,
                    Scale = col.Scale,
                    IsNullable = col.IsNullable == 1,
                    IsIdentity = col.IsIdentity == 1,
                    DefaultValue = col.HasDefault == 1 ? col.DefaultValue : null,
                    Collation = col.Collation,
                    PropertyType = typeof(string), // Default - would need more sophisticated mapping
                    PropertyInfo = null! // Not available from introspection
                };

                columns.Add(column);
            }

            return columns;
        }

        public async Task<List<IndexModel>> GetIndexesAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            var sql = @"
                SELECT DISTINCT
                    i.name as IndexName,
                    i.is_unique as IsUnique,
                    i.type_desc as IndexType,
                    CASE WHEN i.is_primary_key = 1 THEN 1 ELSE 0 END as IsPrimaryKey
                FROM sys.indexes i
                INNER JOIN sys.objects o ON i.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE o.name = @TableName
                AND (@Schema IS NULL OR s.name = @Schema)
                AND i.name IS NOT NULL
                AND i.is_primary_key = 0"; // Exclude primary key constraint

            var indexInfos = await connection.QueryAsync(sql, new { TableName = tableName, Schema = schema });
            var indexes = new List<IndexModel>();

            foreach (dynamic idx in indexInfos)
            {
                var index = new IndexModel
                {
                    Name = idx.IndexName,
                    IsUnique = idx.IsUnique,
                    IndexType = MapSqlServerIndexType(idx.IndexType),
                    IsClustered = idx.IndexType?.ToString()?.Contains("CLUSTERED") == true
                };

                // Get index columns
                var columnSql = @"
                    SELECT 
                        c.name as ColumnName,
                        ic.key_ordinal as KeyOrdinal,
                        ic.is_descending_key as IsDescending
                    FROM sys.index_columns ic
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    INNER JOIN sys.objects o ON i.object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE o.name = @TableName
                    AND (@Schema IS NULL OR s.name = @Schema)
                    AND i.name = @IndexName
                    ORDER BY ic.key_ordinal";

                var columns = await connection.QueryAsync(columnSql, new { TableName = tableName, Schema = schema, IndexName = idx.IndexName });
                
                foreach (dynamic col in columns)
                {
                    index.Columns.Add(new IndexColumnModel
                    {
                        ColumnName = col.ColumnName,
                        Order = col.KeyOrdinal,
                        IsDescending = col.IsDescending
                    });
                }

                indexes.Add(index);
            }

            return indexes;
        }

        public async Task<List<ConstraintModel>> GetConstraintsAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            var constraints = new List<ConstraintModel>();

            // Primary Key constraints
            var pkSql = @"
                SELECT 
                    kc.CONSTRAINT_NAME as ConstraintName,
                    kc.COLUMN_NAME as ColumnName
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc
                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                WHERE tc.TABLE_NAME = @TableName
                AND (@Schema IS NULL OR tc.TABLE_SCHEMA = @Schema)
                AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY kc.ORDINAL_POSITION";

            var pkColumns = await connection.QueryAsync(pkSql, new { TableName = tableName, Schema = schema });
            var pkGroups = pkColumns.GroupBy(c => ((dynamic)c).ConstraintName);

            foreach (var group in pkGroups)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = group.Key,
                    Type = ConstraintType.PrimaryKey,
                    Columns = group.Select(c => ((dynamic)c).ColumnName.ToString() as string).Where(s => s != null).Cast<string>().ToList()
                });
            }

            // Foreign Key constraints
            var fkSql = @"
                SELECT 
                    rc.CONSTRAINT_NAME as ConstraintName,
                    kcu.COLUMN_NAME as ColumnName,
                    ccu.TABLE_NAME as ReferencedTable,
                    ccu.COLUMN_NAME as ReferencedColumn,
                    rc.DELETE_RULE as DeleteRule,
                    rc.UPDATE_RULE as UpdateRule
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON rc.UNIQUE_CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
                WHERE kcu.TABLE_NAME = @TableName
                AND (@Schema IS NULL OR kcu.TABLE_SCHEMA = @Schema)";

            var fkInfos = await connection.QueryAsync(fkSql, new { TableName = tableName, Schema = schema });

            foreach (dynamic fk in fkInfos)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = fk.ConstraintName,
                    Type = ConstraintType.ForeignKey,
                    Columns = new List<string> { fk.ColumnName },
                    ReferencedTable = fk.ReferencedTable,
                    ReferencedColumn = fk.ReferencedColumn,
                    OnDelete = MapReferentialAction(fk.DeleteRule),
                    OnUpdate = MapReferentialAction(fk.UpdateRule)
                });
            }

            // Check constraints
            var checkSql = @"
                SELECT 
                    cc.CONSTRAINT_NAME as ConstraintName,
                    cc.CHECK_CLAUSE as CheckClause
                FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                WHERE tc.TABLE_NAME = @TableName
                AND (@Schema IS NULL OR tc.TABLE_SCHEMA = @Schema)";

            var checkInfos = await connection.QueryAsync(checkSql, new { TableName = tableName, Schema = schema });

            foreach (dynamic check in checkInfos)
            {
                constraints.Add(new ConstraintModel
                {
                    Name = check.ConstraintName,
                    Type = ConstraintType.Check,
                    CheckExpression = check.CheckClause
                });
            }

            return constraints;
        }

        public async Task<bool> TableExistsAsync(IDbConnection connection, string tableName, string? schema = null)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @TableName
                AND TABLE_TYPE = 'BASE TABLE'
                AND (@Schema IS NULL OR TABLE_SCHEMA = @Schema)";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, Schema = schema });
            return count > 0;
        }

        public async Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName, string? schema = null)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                AND COLUMN_NAME = @ColumnName
                AND (@Schema IS NULL OR TABLE_SCHEMA = @Schema)";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, ColumnName = columnName, Schema = schema });
            return count > 0;
        }

        private string MapSqlServerTypeToNetType(string sqlType, int? maxLength, int? precision, int? scale)
        {
            return sqlType.ToUpper() switch
            {
                "BIT" => "BOOL",
                "TINYINT" => "BYTE",
                "SMALLINT" => "INT16",
                "INT" => "INT32",
                "BIGINT" => "INT64",
                "REAL" => "SINGLE",
                "FLOAT" => "DOUBLE",
                "DECIMAL" or "NUMERIC" => precision.HasValue ? $"DECIMAL({precision},{scale ?? 0})" : "DECIMAL",
                "DATETIME" or "DATETIME2" or "SMALLDATETIME" => "DATETIME",
                "DATETIMEOFFSET" => "DATETIMEOFFSET",
                "TIME" => "TIMESPAN",
                "UNIQUEIDENTIFIER" => "GUID",
                "NVARCHAR" or "VARCHAR" or "NCHAR" or "CHAR" => maxLength.HasValue ? $"STRING({maxLength})" : "STRING",
                "NTEXT" or "TEXT" => "STRING(MAX)",
                _ => sqlType
            };
        }

        private IndexType MapSqlServerIndexType(string sqlServerType)
        {
            return sqlServerType?.ToUpper() switch
            {
                "CLUSTERED" => IndexType.Clustered,
                "NONCLUSTERED" => IndexType.NonClustered,
                "CLUSTERED COLUMNSTORE" => IndexType.ColumnStore,
                "NONCLUSTERED COLUMNSTORE" => IndexType.ColumnStore,
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