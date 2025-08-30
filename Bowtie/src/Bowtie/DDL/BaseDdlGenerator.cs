using System.Text;
using Bowtie.Models;

namespace Bowtie.DDL
{
    public abstract class BaseDdlGenerator : IDdlGenerator
    {
        public abstract DatabaseProvider Provider { get; }

        public virtual string GenerateCreateTable(TableModel table)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {QuoteIdentifier(table.FullName)} (");
            
            var columnDefinitions = table.Columns.Select(GenerateColumnDefinition).ToList();
            sb.AppendLine("  " + string.Join(",\n  ", columnDefinitions));
            
            // Add primary key constraint if not auto-generated
            var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).OrderBy(c => c.Name).ToList();
            if (pkColumns.Any())
            {
                var pkConstraint = GeneratePrimaryKeyConstraint(pkColumns, table.Name);
                if (!string.IsNullOrEmpty(pkConstraint))
                {
                    sb.AppendLine($",  {pkConstraint}");
                }
            }
            
            sb.AppendLine(");");
            
            // Generate indexes
            foreach (var index in table.Indexes)
            {
                sb.AppendLine();
                sb.AppendLine(GenerateCreateIndex(index, table.FullName));
            }
            
            // Generate constraints
            foreach (var constraint in table.Constraints.Where(c => c.Type != ConstraintType.PrimaryKey))
            {
                sb.AppendLine();
                sb.AppendLine(GenerateAddConstraint(constraint, table.FullName));
            }
            
            return sb.ToString();
        }

        public virtual string GenerateDropTable(TableModel table)
        {
            return $"DROP TABLE {QuoteIdentifier(table.FullName)};";
        }

        public virtual string GenerateAlterTable(TableModel currentTable, TableModel targetTable)
        {
            var statements = new List<string>();
            
            // Handle column changes
            var currentColumns = currentTable.Columns.ToDictionary(c => c.Name, c => c);
            var targetColumns = targetTable.Columns.ToDictionary(c => c.Name, c => c);
            
            // Add new columns
            foreach (var targetColumn in targetColumns.Values)
            {
                if (!currentColumns.ContainsKey(targetColumn.Name))
                {
                    statements.Add(GenerateAddColumn(targetColumn, currentTable.FullName));
                }
            }
            
            // Modify existing columns
            foreach (var targetColumn in targetColumns.Values)
            {
                if (currentColumns.TryGetValue(targetColumn.Name, out var currentColumn))
                {
                    if (!AreColumnsEqual(currentColumn, targetColumn))
                    {
                        statements.Add(GenerateAlterColumn(currentColumn, targetColumn, currentTable.FullName));
                    }
                }
            }
            
            // Drop removed columns
            foreach (var currentColumn in currentColumns.Values)
            {
                if (!targetColumns.ContainsKey(currentColumn.Name))
                {
                    statements.Add(GenerateDropColumn(currentColumn.Name, currentTable.FullName));
                }
            }
            
            return string.Join("\n\n", statements);
        }

        public abstract string GenerateCreateIndex(IndexModel index, string tableName);
        public abstract string GenerateDropIndex(IndexModel index, string tableName);
        public abstract string GenerateAddColumn(ColumnModel column, string tableName);
        public abstract string GenerateDropColumn(string columnName, string tableName);
        public abstract string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName);
        
        public virtual string GenerateAddConstraint(ConstraintModel constraint, string tableName)
        {
            return constraint.Type switch
            {
                ConstraintType.ForeignKey => GenerateForeignKeyConstraint(constraint, tableName),
                ConstraintType.Unique => GenerateUniqueConstraint(constraint, tableName),
                ConstraintType.Check => GenerateCheckConstraint(constraint, tableName),
                _ => string.Empty
            };
        }

        public virtual string GenerateDropConstraint(string constraintName, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} DROP CONSTRAINT {QuoteIdentifier(constraintName)};";
        }

        public virtual List<string> GenerateMigrationScript(List<TableModel> currentTables, List<TableModel> targetTables)
        {
            var statements = new List<string>();
            
            var currentTableDict = currentTables.ToDictionary(t => t.FullName, t => t);
            var targetTableDict = targetTables.ToDictionary(t => t.FullName, t => t);
            
            // Create new tables
            foreach (var targetTable in targetTables)
            {
                if (!currentTableDict.ContainsKey(targetTable.FullName))
                {
                    statements.Add(GenerateCreateTable(targetTable));
                }
            }
            
            // Alter existing tables
            foreach (var targetTable in targetTables)
            {
                if (currentTableDict.TryGetValue(targetTable.FullName, out var currentTable))
                {
                    var alterScript = GenerateAlterTable(currentTable, targetTable);
                    if (!string.IsNullOrWhiteSpace(alterScript))
                    {
                        statements.Add(alterScript);
                    }
                }
            }
            
            // Drop removed tables
            foreach (var currentTable in currentTables)
            {
                if (!targetTableDict.ContainsKey(currentTable.FullName))
                {
                    statements.Add(GenerateDropTable(currentTable));
                }
            }
            
            return statements;
        }

        public abstract string MapNetTypeToDbType(Type netType, ColumnModel column);
        public abstract bool ValidateIndexType(IndexType indexType);

        protected virtual string QuoteIdentifier(string identifier)
        {
            return Provider.GetQuotedIdentifier(identifier);
        }

        protected virtual string GenerateColumnDefinition(ColumnModel column)
        {
            var sb = new StringBuilder();
            sb.Append(QuoteIdentifier(column.Name));
            sb.Append($" {MapNetTypeToDbType(column.PropertyType, column)}");
            
            if (!column.IsNullable)
            {
                sb.Append(" NOT NULL");
            }
            
            if (column.IsIdentity)
            {
                sb.Append(GenerateIdentityClause());
            }
            
            if (column.DefaultValue != null)
            {
                var defaultValue = column.IsDefaultRawSql 
                    ? column.DefaultValue.ToString() 
                    : FormatDefaultValue(column.DefaultValue);
                sb.Append($" DEFAULT {defaultValue}");
            }
            
            return sb.ToString();
        }

        protected virtual string GeneratePrimaryKeyConstraint(List<ColumnModel> pkColumns, string tableName)
        {
            if (!pkColumns.Any()) return string.Empty;
            
            var columnNames = pkColumns.Select(c => QuoteIdentifier(c.Name));
            return $"CONSTRAINT {QuoteIdentifier($"PK_{tableName}")} PRIMARY KEY ({string.Join(", ", columnNames)})";
        }

        protected virtual string GenerateForeignKeyConstraint(ConstraintModel constraint, string tableName)
        {
            var sb = new StringBuilder();
            sb.Append($"ALTER TABLE {QuoteIdentifier(tableName)} ADD CONSTRAINT {QuoteIdentifier(constraint.Name)} ");
            sb.Append($"FOREIGN KEY ({string.Join(", ", constraint.Columns.Select(QuoteIdentifier))}) ");
            sb.Append($"REFERENCES {QuoteIdentifier(constraint.ReferencedTable!)} ({QuoteIdentifier(constraint.ReferencedColumn!)})");
            
            if (constraint.OnDelete != ReferentialAction.NoAction)
            {
                sb.Append($" ON DELETE {GetReferentialActionSql(constraint.OnDelete)}");
            }
            
            if (constraint.OnUpdate != ReferentialAction.NoAction)
            {
                sb.Append($" ON UPDATE {GetReferentialActionSql(constraint.OnUpdate)}");
            }
            
            sb.Append(";");
            return sb.ToString();
        }

        protected virtual string GenerateUniqueConstraint(ConstraintModel constraint, string tableName)
        {
            var columnNames = constraint.Columns.Select(QuoteIdentifier);
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD CONSTRAINT {QuoteIdentifier(constraint.Name)} " +
                   $"UNIQUE ({string.Join(", ", columnNames)});";
        }

        protected virtual string GenerateCheckConstraint(ConstraintModel constraint, string tableName)
        {
            return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD CONSTRAINT {QuoteIdentifier(constraint.Name)} " +
                   $"CHECK ({constraint.CheckExpression});";
        }

        protected abstract string GenerateIdentityClause();
        protected abstract string FormatDefaultValue(object value);
        
        protected virtual string GetReferentialActionSql(ReferentialAction action)
        {
            return action switch
            {
                ReferentialAction.Cascade => "CASCADE",
                ReferentialAction.SetNull => "SET NULL",
                ReferentialAction.SetDefault => "SET DEFAULT",
                ReferentialAction.Restrict => "RESTRICT",
                _ => "NO ACTION"
            };
        }

        protected virtual bool AreColumnsEqual(ColumnModel current, ColumnModel target)
        {
            return current.DataType == target.DataType &&
                   current.MaxLength == target.MaxLength &&
                   current.Precision == target.Precision &&
                   current.Scale == target.Scale &&
                   current.IsNullable == target.IsNullable &&
                   current.IsIdentity == target.IsIdentity &&
                   Equals(current.DefaultValue, target.DefaultValue);
        }
    }
}