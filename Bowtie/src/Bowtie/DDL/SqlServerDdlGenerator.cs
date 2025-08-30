using System.Text;
using Bowtie.Models;

namespace Bowtie.DDL
{
    public class SqlServerDdlGenerator : BaseDdlGenerator
    {
        public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

        public override string GenerateCreateIndex(IndexModel index, string tableName)
        {
            var sb = new StringBuilder();
            
            if (index.IsUnique)
            {
                sb.Append("CREATE UNIQUE ");
            }
            else
            {
                sb.Append("CREATE ");
            }
            
            if (index.IsClustered)
            {
                sb.Append("CLUSTERED ");
            }
            else
            {
                sb.Append("NONCLUSTERED ");
            }
            
            sb.AppendLine($"INDEX {QuoteIdentifier(index.Name)} ON {QuoteIdentifier(tableName)}");
            
            var columns = index.Columns
                .OrderBy(c => c.Order)
                .Select(c => $"{QuoteIdentifier(c.ColumnName)}{(c.IsDescending ? " DESC" : " ASC")}")
                .ToList();
            
            sb.AppendLine($"({string.Join(", ", columns)})");
            
            if (!string.IsNullOrEmpty(index.IncludeColumns))
            {
                sb.AppendLine($"INCLUDE ({index.IncludeColumns})");
            }
            
            if (!string.IsNullOrEmpty(index.WhereClause))
            {
                sb.AppendLine($"WHERE {index.WhereClause}");
            }
            
            sb.Append(";");
            return sb.ToString();
        }

        public override string GenerateDropIndex(IndexModel index, string tableName)
        {
            return $"DROP INDEX {QuoteIdentifier(index.Name)} ON {QuoteIdentifier(tableName)};";
        }

        public override string GenerateAddColumn(ColumnModel column, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD {GenerateColumnDefinition(column)};";
        }

        public override string GenerateDropColumn(string columnName, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} DROP COLUMN {QuoteIdentifier(columnName)};";
        }

        public override string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ALTER COLUMN {GenerateColumnDefinition(targetColumn)};";
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
                nameof(Boolean) => "BIT",
                nameof(Byte) => "TINYINT",
                nameof(SByte) => "SMALLINT",
                nameof(Int16) => "SMALLINT",
                nameof(UInt16) => "INT",
                nameof(Int32) => "INT",
                nameof(UInt32) => "BIGINT",
                nameof(Int64) => "BIGINT",
                nameof(UInt64) => "DECIMAL(20,0)",
                nameof(Single) => "REAL",
                nameof(Double) => "FLOAT",
                nameof(Decimal) => column.Precision.HasValue 
                    ? $"DECIMAL({column.Precision},{column.Scale ?? 0})" 
                    : "DECIMAL(18,2)",
                nameof(DateTime) => "DATETIME2",
                nameof(DateTimeOffset) => "DATETIMEOFFSET",
                nameof(TimeSpan) => "TIME",
                nameof(Guid) => "UNIQUEIDENTIFIER",
                nameof(String) => column.MaxLength.HasValue 
                    ? (column.MaxLength.Value == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({column.MaxLength})") 
                    : "NVARCHAR(255)",
                _ => underlyingType.IsEnum ? "INT" : "NVARCHAR(255)"
            };
        }

        public override bool ValidateIndexType(IndexType indexType)
        {
            return indexType switch
            {
                IndexType.BTree => true,
                IndexType.Clustered => true,
                IndexType.NonClustered => true,
                IndexType.ColumnStore => true,
                IndexType.Spatial => true,
                IndexType.FullText => true,
                _ => false
            };
        }

        protected override string GenerateIdentityClause()
        {
            return " IDENTITY(1,1)";
        }

        protected override string FormatDefaultValue(object value)
        {
            return value switch
            {
                string s => $"'{s.Replace("'", "''")}'",
                bool b => b ? "1" : "0",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.fff zzz}'",
                Guid g => $"'{g}'",
                null => "NULL",
                _ => value.ToString() ?? "NULL"
            };
        }
    }
}