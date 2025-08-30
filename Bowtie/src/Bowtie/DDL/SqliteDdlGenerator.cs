using System.Text;
using Bowtie.Models;

namespace Bowtie.DDL
{
    public class SqliteDdlGenerator : BaseDdlGenerator
    {
        public override DatabaseProvider Provider => DatabaseProvider.SQLite;

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
            
            if (!string.IsNullOrEmpty(index.WhereClause))
            {
                sb.Append($" WHERE {index.WhereClause}");
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
            // SQLite doesn't support DROP COLUMN directly - requires table recreation
            throw new NotSupportedException("SQLite does not support DROP COLUMN. Table recreation is required.");
        }

        public override string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName)
        {
            // SQLite doesn't support ALTER COLUMN directly - requires table recreation
            throw new NotSupportedException("SQLite does not support ALTER COLUMN. Table recreation is required.");
        }

        public override string GenerateAlterTable(TableModel currentTable, TableModel targetTable)
        {
            // For SQLite, we need to check if any unsupported operations are required
            var currentColumns = currentTable.Columns.ToDictionary(c => c.Name, c => c);
            var targetColumns = targetTable.Columns.ToDictionary(c => c.Name, c => c);
            
            bool needsRecreation = false;
            var statements = new List<string>();
            
            // Check for column modifications or deletions
            foreach (var currentColumn in currentColumns.Values)
            {
                if (!targetColumns.ContainsKey(currentColumn.Name))
                {
                    needsRecreation = true;
                    break;
                }
                
                if (targetColumns.TryGetValue(currentColumn.Name, out var targetColumn))
                {
                    if (!AreColumnsEqual(currentColumn, targetColumn))
                    {
                        needsRecreation = true;
                        break;
                    }
                }
            }
            
            if (needsRecreation)
            {
                return GenerateTableRecreation(currentTable, targetTable);
            }
            
            // Only add new columns (supported operation)
            foreach (var targetColumn in targetColumns.Values)
            {
                if (!currentColumns.ContainsKey(targetColumn.Name))
                {
                    statements.Add(GenerateAddColumn(targetColumn, currentTable.FullName));
                }
            }
            
            return string.Join("\n\n", statements);
        }

        private string GenerateTableRecreation(TableModel currentTable, TableModel targetTable)
        {
            var sb = new StringBuilder();
            var tempTableName = $"{targetTable.Name}_temp_{DateTime.Now:yyyyMMddHHmmss}";
            
            // Step 1: Create new table with temp name
            var tempTable = new TableModel
            {
                Name = tempTableName,
                Schema = targetTable.Schema,
                ModelType = targetTable.ModelType,
                Columns = targetTable.Columns,
                Indexes = new List<IndexModel>(), // Don't create indexes yet
                Constraints = targetTable.Constraints
            };
            
            sb.AppendLine(GenerateCreateTable(tempTable));
            sb.AppendLine();
            
            // Step 2: Copy data from old table to new table
            var commonColumns = currentTable.Columns
                .Where(c => targetTable.Columns.Any(tc => tc.Name == c.Name))
                .Select(c => QuoteIdentifier(c.Name))
                .ToList();
            
            if (commonColumns.Any())
            {
                sb.AppendLine($"INSERT INTO {QuoteIdentifier(tempTableName)} ({string.Join(", ", commonColumns)})");
                sb.AppendLine($"SELECT {string.Join(", ", commonColumns)} FROM {QuoteIdentifier(currentTable.FullName)};");
                sb.AppendLine();
            }
            
            // Step 3: Drop old table
            sb.AppendLine($"DROP TABLE {QuoteIdentifier(currentTable.FullName)};");
            sb.AppendLine();
            
            // Step 4: Rename temp table
            sb.AppendLine($"ALTER TABLE {QuoteIdentifier(tempTableName)} RENAME TO {QuoteIdentifier(targetTable.Name)};");
            sb.AppendLine();
            
            // Step 5: Create indexes
            foreach (var index in targetTable.Indexes)
            {
                sb.AppendLine(GenerateCreateIndex(index, targetTable.FullName));
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        public override string MapNetTypeToDbType(Type netType, ColumnModel column)
        {
            if (!string.IsNullOrEmpty(column.DataType))
            {
                return column.DataType;
            }
            
            var underlyingType = Nullable.GetUnderlyingType(netType) ?? netType;
            
            // SQLite has dynamic typing with storage classes: INTEGER, REAL, TEXT, BLOB
            return underlyingType.Name switch
            {
                nameof(Boolean) => "INTEGER",
                nameof(Byte) => "INTEGER",
                nameof(SByte) => "INTEGER",
                nameof(Int16) => "INTEGER",
                nameof(UInt16) => "INTEGER",
                nameof(Int32) => "INTEGER",
                nameof(UInt32) => "INTEGER",
                nameof(Int64) => "INTEGER",
                nameof(UInt64) => "INTEGER",
                nameof(Single) => "REAL",
                nameof(Double) => "REAL",
                nameof(Decimal) => "REAL",
                nameof(DateTime) => "TEXT",
                nameof(DateTimeOffset) => "TEXT",
                nameof(TimeSpan) => "TEXT",
                nameof(Guid) => "TEXT",
                nameof(String) => "TEXT",
                _ => underlyingType.IsEnum ? "INTEGER" : "TEXT"
            };
        }

        public override bool ValidateIndexType(IndexType indexType)
        {
            // SQLite only supports B-Tree indexes
            return indexType == IndexType.BTree;
        }

        protected override string GenerateIdentityClause()
        {
            return " AUTOINCREMENT";
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

        public override string GenerateDropConstraint(string constraintName, string tableName)
        {
            // SQLite doesn't support dropping constraints - requires table recreation
            throw new NotSupportedException("SQLite does not support dropping constraints. Table recreation is required.");
        }
    }
}