using Bowtie.Models;
using Microsoft.Extensions.Logging;

namespace Bowtie.Analysis
{
    public class DataLossAnalyzer
    {
        private readonly ILogger<DataLossAnalyzer> _logger;

        public DataLossAnalyzer(ILogger<DataLossAnalyzer> logger)
        {
            _logger = logger;
        }

        public DataLossRisk AnalyzeMigrationRisks(List<TableModel> currentTables, List<TableModel> targetTables)
        {
            var risks = new List<DataLossWarning>();
            var currentTableDict = currentTables.ToDictionary(t => t.FullName, t => t);
            var targetTableDict = targetTables.ToDictionary(t => t.FullName, t => t);

            // Check for dropped tables
            foreach (var currentTable in currentTables)
            {
                if (!targetTableDict.ContainsKey(currentTable.FullName))
                {
                    risks.Add(new DataLossWarning
                    {
                        Type = DataLossType.TableDrop,
                        Severity = DataLossSeverity.High,
                        Message = $"Table '{currentTable.FullName}' will be DROPPED. All data will be lost.",
                        TableName = currentTable.FullName,
                        Details = $"Table has {currentTable.Columns.Count} columns and may contain data."
                    });
                }
            }

            // Check for column changes within existing tables
            foreach (var targetTable in targetTables)
            {
                if (currentTableDict.TryGetValue(targetTable.FullName, out var currentTable))
                {
                    risks.AddRange(AnalyzeTableChanges(currentTable, targetTable));
                }
            }

            return new DataLossRisk
            {
                Warnings = risks,
                HasHighRiskOperations = risks.Any(r => r.Severity == DataLossSeverity.High),
                HasMediumRiskOperations = risks.Any(r => r.Severity == DataLossSeverity.Medium),
                RequiresConfirmation = risks.Any(r => r.Severity >= DataLossSeverity.Medium)
            };
        }

        private List<DataLossWarning> AnalyzeTableChanges(TableModel currentTable, TableModel targetTable)
        {
            var warnings = new List<DataLossWarning>();
            var currentColumns = currentTable.Columns.ToDictionary(c => c.Name, c => c);
            var targetColumns = targetTable.Columns.ToDictionary(c => c.Name, c => c);

            // Check for dropped columns
            foreach (var currentColumn in currentColumns.Values)
            {
                if (!targetColumns.ContainsKey(currentColumn.Name))
                {
                    warnings.Add(new DataLossWarning
                    {
                        Type = DataLossType.ColumnDrop,
                        Severity = DataLossSeverity.High,
                        Message = $"Column '{currentTable.FullName}.{currentColumn.Name}' will be DROPPED. All data in this column will be lost.",
                        TableName = currentTable.FullName,
                        ColumnName = currentColumn.Name,
                        Details = $"Column type: {currentColumn.DataType}"
                    });
                }
            }

            // Check for column modifications that could cause data loss
            foreach (var targetColumn in targetColumns.Values)
            {
                if (currentColumns.TryGetValue(targetColumn.Name, out var currentColumn))
                {
                    warnings.AddRange(AnalyzeColumnChanges(currentTable.FullName, currentColumn, targetColumn));
                }
            }

            return warnings;
        }

        private List<DataLossWarning> AnalyzeColumnChanges(string tableName, ColumnModel currentColumn, ColumnModel targetColumn)
        {
            var warnings = new List<DataLossWarning>();

            // Check for data type changes that could cause truncation or loss
            if (currentColumn.DataType != targetColumn.DataType)
            {
                var severity = DetermineDataTypeSeverity(currentColumn, targetColumn);
                if (severity > DataLossSeverity.None)
                {
                    warnings.Add(new DataLossWarning
                    {
                        Type = DataLossType.DataTypeChange,
                        Severity = severity,
                        Message = $"Column '{tableName}.{currentColumn.Name}' data type changing from '{currentColumn.DataType}' to '{targetColumn.DataType}'. Possible data loss or truncation.",
                        TableName = tableName,
                        ColumnName = currentColumn.Name,
                        Details = $"Current: {currentColumn.DataType}, Target: {targetColumn.DataType}"
                    });
                }
            }

            // Check for length reductions
            if (currentColumn.MaxLength.HasValue && targetColumn.MaxLength.HasValue)
            {
                if (targetColumn.MaxLength < currentColumn.MaxLength)
                {
                    warnings.Add(new DataLossWarning
                    {
                        Type = DataLossType.LengthReduction,
                        Severity = DataLossSeverity.High,
                        Message = $"Column '{tableName}.{currentColumn.Name}' max length reducing from {currentColumn.MaxLength} to {targetColumn.MaxLength}. Data may be truncated.",
                        TableName = tableName,
                        ColumnName = currentColumn.Name,
                        Details = $"Length change: {currentColumn.MaxLength} → {targetColumn.MaxLength}"
                    });
                }
            }

            // Check for precision/scale reductions
            if (currentColumn.Precision.HasValue && targetColumn.Precision.HasValue)
            {
                if (targetColumn.Precision < currentColumn.Precision ||
                    (targetColumn.Scale.HasValue && currentColumn.Scale.HasValue && targetColumn.Scale < currentColumn.Scale))
                {
                    warnings.Add(new DataLossWarning
                    {
                        Type = DataLossType.PrecisionReduction,
                        Severity = DataLossSeverity.High,
                        Message = $"Column '{tableName}.{currentColumn.Name}' precision/scale reducing. Numeric data may be truncated.",
                        TableName = tableName,
                        ColumnName = currentColumn.Name,
                        Details = $"Precision: {currentColumn.Precision},{currentColumn.Scale} → {targetColumn.Precision},{targetColumn.Scale}"
                    });
                }
            }

            // Check for nullability changes (nullable to non-nullable)
            if (currentColumn.IsNullable && !targetColumn.IsNullable)
            {
                warnings.Add(new DataLossWarning
                {
                    Type = DataLossType.NullabilityChange,
                    Severity = DataLossSeverity.Medium,
                    Message = $"Column '{tableName}.{currentColumn.Name}' changing from nullable to non-nullable. Rows with NULL values may cause errors.",
                    TableName = tableName,
                    ColumnName = currentColumn.Name,
                    Details = "Consider adding a default value or updating NULL values before migration."
                });
            }

            return warnings;
        }

        private DataLossSeverity DetermineDataTypeSeverity(ColumnModel currentColumn, ColumnModel targetColumn)
        {
            // High risk type changes
            var highriskChanges = new[]
            {
                ("NVARCHAR", "INT"), ("VARCHAR", "INT"), ("TEXT", "INTEGER"),
                ("DECIMAL", "INT"), ("NUMERIC", "INTEGER"), 
                ("DATETIME", "VARCHAR"), ("TIMESTAMP", "TEXT"),
                ("UNIQUEIDENTIFIER", "VARCHAR"), ("UUID", "TEXT"),
                ("BIGINT", "INT"), ("BIGINT", "INTEGER")
            };

            foreach (var (from, to) in highriskChanges)
            {
                if (currentColumn.DataType.Contains(from, StringComparison.OrdinalIgnoreCase) &&
                    targetColumn.DataType.Contains(to, StringComparison.OrdinalIgnoreCase))
                {
                    return DataLossSeverity.High;
                }
            }

            // Medium risk type changes
            var mediumRiskChanges = new[]
            {
                ("NVARCHAR", "VARCHAR"), ("TEXT", "VARCHAR"),
                ("DATETIME2", "DATETIME"), ("TIMESTAMPTZ", "TIMESTAMP"),
                ("FLOAT", "REAL"), ("DOUBLE", "FLOAT")
            };

            foreach (var (from, to) in mediumRiskChanges)
            {
                if (currentColumn.DataType.Contains(from, StringComparison.OrdinalIgnoreCase) &&
                    targetColumn.DataType.Contains(to, StringComparison.OrdinalIgnoreCase))
                {
                    return DataLossSeverity.Medium;
                }
            }

            // If types are different but not in known risk categories, assume medium risk
            if (!string.Equals(currentColumn.DataType, targetColumn.DataType, StringComparison.OrdinalIgnoreCase))
            {
                return DataLossSeverity.Medium;
            }

            return DataLossSeverity.None;
        }

        public void LogDataLossWarnings(DataLossRisk risk)
        {
            if (!risk.Warnings.Any())
            {
                _logger.LogInformation("✅ No data loss risks detected in migration.");
                return;
            }

            _logger.LogWarning("⚠️  DATA LOSS WARNINGS DETECTED:");
            _logger.LogWarning("=" + new string('=', 50));

            foreach (var warning in risk.Warnings.OrderByDescending(w => w.Severity))
            {
                var icon = warning.Severity switch
                {
                    DataLossSeverity.High => "🔴 HIGH RISK",
                    DataLossSeverity.Medium => "🟡 MEDIUM RISK",
                    _ => "🟢 LOW RISK"
                };

                _logger.LogWarning("{Icon}: {Message}", icon, warning.Message);
                if (!string.IsNullOrEmpty(warning.Details))
                {
                    _logger.LogWarning("   Details: {Details}", warning.Details);
                }
            }

            _logger.LogWarning("=" + new string('=', 50));

            if (risk.HasHighRiskOperations)
            {
                _logger.LogError("🚨 HIGH RISK OPERATIONS DETECTED - Data loss is likely!");
            }
            if (risk.HasMediumRiskOperations)
            {
                _logger.LogWarning("⚠️  MEDIUM RISK OPERATIONS - Review carefully before proceeding!");
            }
        }
    }

    public class DataLossRisk
    {
        public List<DataLossWarning> Warnings { get; set; } = new();
        public bool HasHighRiskOperations { get; set; }
        public bool HasMediumRiskOperations { get; set; }
        public bool RequiresConfirmation { get; set; }
    }

    public class DataLossWarning
    {
        public DataLossType Type { get; set; }
        public DataLossSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string? ColumnName { get; set; }
        public string? Details { get; set; }
    }

    public enum DataLossType
    {
        TableDrop,
        ColumnDrop,
        DataTypeChange,
        LengthReduction,
        PrecisionReduction,
        NullabilityChange
    }

    public enum DataLossSeverity
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }
}