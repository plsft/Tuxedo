using System.Text;
using Bowtie.Models;

namespace Bowtie.DDL
{
    public class PostgreSqlDdlGenerator : BaseDdlGenerator
    {
        public override DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

        public override string GenerateCreateIndex(IndexModel index, string tableName)
        {
            var sb = new StringBuilder();
            
            sb.Append("CREATE ");
            
            if (index.IsUnique)
            {
                sb.Append("UNIQUE ");
            }
            
            sb.Append($"INDEX {QuoteIdentifier(index.Name)} ON {QuoteIdentifier(tableName)}");
            
            // Add index method for PostgreSQL-specific types
            if (index.IndexType != IndexType.BTree)
            {
                var method = index.IndexType switch
                {
                    IndexType.Hash => "HASH",
                    IndexType.GIN => "GIN",
                    IndexType.GiST => "GIST",
                    IndexType.BRIN => "BRIN",
                    IndexType.SPGiST => "SPGIST",
                    _ => "BTREE"
                };
                sb.Append($" USING {method}");
            }
            
            var columns = index.Columns
                .OrderBy(c => c.Order)
                .Select(c => $"{QuoteIdentifier(c.ColumnName)}{(c.IsDescending ? " DESC" : " ASC")}")
                .ToList();
            
            sb.AppendLine($" ({string.Join(", ", columns)})");
            
            if (!string.IsNullOrEmpty(index.WhereClause))
            {
                sb.AppendLine($"WHERE {index.WhereClause}");
            }
            
            sb.Append(";");
            return sb.ToString();
        }

        public override string GenerateDropIndex(IndexModel index, string tableName)
        {
            return $"DROP INDEX {QuoteIdentifier(index.Name)};";
        }

        public override string GenerateAddColumn(ColumnModel column, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {GenerateColumnDefinition(column)};";
        }

        public override string GenerateDropColumn(string columnName, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} DROP COLUMN {QuoteIdentifier(columnName)};";
        }

        public override string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName)
        {
            var statements = new List<string>();
            
            // Change data type
            if (currentColumn.DataType != targetColumn.DataType)
            {
                statements.Add($"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {QuoteIdentifier(targetColumn.Name)} " +
                              $"TYPE {MapNetTypeToDbType(targetColumn.PropertyType, targetColumn)};");
            }
            
            // Change nullability
            if (currentColumn.IsNullable != targetColumn.IsNullable)
            {
                var nullClause = targetColumn.IsNullable ? "DROP NOT NULL" : "SET NOT NULL";
                statements.Add($"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {QuoteIdentifier(targetColumn.Name)} {nullClause};");
            }
            
            // Change default value
            if (!Equals(currentColumn.DefaultValue, targetColumn.DefaultValue))
            {
                if (targetColumn.DefaultValue != null)
                {
                    var defaultValue = targetColumn.IsDefaultRawSql 
                        ? targetColumn.DefaultValue.ToString() 
                        : FormatDefaultValue(targetColumn.DefaultValue);
                    statements.Add($"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {QuoteIdentifier(targetColumn.Name)} " +
                                  $"SET DEFAULT {defaultValue};");
                }
                else
                {
                    statements.Add($"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {QuoteIdentifier(targetColumn.Name)} DROP DEFAULT;");
                }
            }
            
            return string.Join("\n", statements);
        }

        public override string MapNetTypeToDbType(Type netType, ColumnModel column)
        {
            if (!string.IsNullOrEmpty(column.DataType))
            {
                return column.DataType;
            }
            
            var underlyingType = Nullable.GetUnderlyingType(netType) ?? netType;
            
            return underlyingType.Name switch
            {
                nameof(Boolean) => "BOOLEAN",
                nameof(Byte) => "SMALLINT",
                nameof(SByte) => "SMALLINT",
                nameof(Int16) => "SMALLINT",
                nameof(UInt16) => "INTEGER",
                nameof(Int32) => "INTEGER",
                nameof(UInt32) => "BIGINT",
                nameof(Int64) => "BIGINT",
                nameof(UInt64) => "NUMERIC(20,0)",
                nameof(Single) => "REAL",
                nameof(Double) => "DOUBLE PRECISION",
                nameof(Decimal) => column.Precision.HasValue 
                    ? $"NUMERIC({column.Precision},{column.Scale ?? 0})" 
                    : "NUMERIC(18,2)",
                nameof(DateTime) => "TIMESTAMP",
                nameof(DateTimeOffset) => "TIMESTAMPTZ",
                nameof(TimeSpan) => "INTERVAL",
                nameof(Guid) => "UUID",
                nameof(String) => column.MaxLength.HasValue 
                    ? (column.MaxLength.Value == -1 ? "TEXT" : $"VARCHAR({column.MaxLength})") 
                    : "VARCHAR(255)",
                _ => underlyingType.IsEnum ? "INTEGER" : "VARCHAR(255)"
            };
        }

        public override bool ValidateIndexType(IndexType indexType)
        {
            return indexType switch
            {
                IndexType.BTree => true,
                IndexType.Hash => true,
                IndexType.GIN => true,
                IndexType.GiST => true,
                IndexType.BRIN => true,
                IndexType.SPGiST => true,
                IndexType.Spatial => true,
                _ => false
            };
        }

        protected override string GenerateIdentityClause()
        {
            return " GENERATED ALWAYS AS IDENTITY";
        }

        protected override string FormatDefaultValue(object value)
        {
            return value switch
            {
                string s => $"'{s.Replace("'", "''")}'",
                bool b => b.ToString().ToLower(),
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'::timestamp",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.fff zzz}'::timestamptz",
                Guid g => $"'{g}'::uuid",
                null => "NULL",
                _ => value.ToString() ?? "NULL"
            };
        }
    }
}