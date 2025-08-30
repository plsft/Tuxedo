using System.Text;
using Bowtie.Models;

namespace Bowtie.DDL
{
    public class MySqlDdlGenerator : BaseDdlGenerator
    {
        public override DatabaseProvider Provider => DatabaseProvider.MySQL;

        public override string GenerateCreateIndex(IndexModel index, string tableName)
        {
            var sb = new StringBuilder();
            
            sb.Append("CREATE ");
            
            if (index.IsUnique)
            {
                sb.Append("UNIQUE ");
            }
            
            sb.Append($"INDEX {QuoteIdentifier(index.Name)} ON {QuoteIdentifier(tableName)}");
            
            var columns = index.Columns
                .OrderBy(c => c.Order)
                .Select(c => $"{QuoteIdentifier(c.ColumnName)}{(c.IsDescending ? " DESC" : " ASC")}")
                .ToList();
            
            sb.Append($" ({string.Join(", ", columns)})");
            
            // MySQL supports index types
            if (index.IndexType == IndexType.Hash)
            {
                sb.Append(" USING HASH");
            }
            else if (index.IndexType == IndexType.BTree)
            {
                sb.Append(" USING BTREE");
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
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {GenerateColumnDefinition(column)};";
        }

        public override string GenerateDropColumn(string columnName, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} DROP COLUMN {QuoteIdentifier(columnName)};";
        }

        public override string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} MODIFY COLUMN {GenerateColumnDefinition(targetColumn)};";
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
                nameof(Boolean) => "TINYINT(1)",
                nameof(Byte) => "TINYINT UNSIGNED",
                nameof(SByte) => "TINYINT",
                nameof(Int16) => "SMALLINT",
                nameof(UInt16) => "SMALLINT UNSIGNED",
                nameof(Int32) => "INT",
                nameof(UInt32) => "INT UNSIGNED",
                nameof(Int64) => "BIGINT",
                nameof(UInt64) => "BIGINT UNSIGNED",
                nameof(Single) => "FLOAT",
                nameof(Double) => "DOUBLE",
                nameof(Decimal) => column.Precision.HasValue 
                    ? $"DECIMAL({column.Precision},{column.Scale ?? 0})" 
                    : "DECIMAL(18,2)",
                nameof(DateTime) => "DATETIME",
                nameof(DateTimeOffset) => "TIMESTAMP",
                nameof(TimeSpan) => "TIME",
                nameof(Guid) => "CHAR(36)",
                nameof(String) => column.MaxLength.HasValue 
                    ? (column.MaxLength.Value == -1 ? "LONGTEXT" : $"VARCHAR({column.MaxLength})") 
                    : "VARCHAR(255)",
                _ => underlyingType.IsEnum ? "INT" : "VARCHAR(255)"
            };
        }

        public override bool ValidateIndexType(IndexType indexType)
        {
            return indexType switch
            {
                IndexType.BTree => true,
                IndexType.Hash => true,
                IndexType.FullText => true,
                IndexType.Spatial => true,
                _ => false
            };
        }

        protected override string GenerateIdentityClause()
        {
            return " AUTO_INCREMENT";
        }

        protected override string FormatDefaultValue(object value)
        {
            return value switch
            {
                string s => $"'{s.Replace("'", "''")}'",
                bool b => b ? "1" : "0",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
                Guid g => $"'{g}'",
                null => "NULL",
                _ => value.ToString() ?? "NULL"
            };
        }
    }
}