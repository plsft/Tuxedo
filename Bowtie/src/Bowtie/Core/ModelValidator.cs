using System.Reflection;
using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.DDL;
using Microsoft.Extensions.Logging;

namespace Bowtie.Core
{
    public class ModelValidator
    {
        private readonly ModelAnalyzer _modelAnalyzer;
        private readonly IEnumerable<IDdlGenerator> _ddlGenerators;
        private readonly ILogger<ModelValidator> _logger;

        public ModelValidator(
            ModelAnalyzer modelAnalyzer,
            IEnumerable<IDdlGenerator> ddlGenerators,
            ILogger<ModelValidator> logger)
        {
            _modelAnalyzer = modelAnalyzer;
            _ddlGenerators = ddlGenerators;
            _logger = logger;
        }

        public bool Validate(string assemblyPath, DatabaseProvider provider)
        {
            var generator = GetDdlGenerator(provider);
            bool isValid = true;
            
            _logger.LogInformation("Loading assembly: {AssemblyPath}", assemblyPath);
            var assembly = Assembly.LoadFrom(assemblyPath);
            
            _logger.LogInformation("Analyzing models in assembly...");
            var tables = _modelAnalyzer.AnalyzeAssembly(assembly, provider.GetDefaultSchema());
            
            _logger.LogInformation("Found {TableCount} table models", tables.Count);
            
            foreach (var table in tables)
            {
                _logger.LogDebug("Validating table: {TableName}", table.FullName);
                
                if (!ValidateTable(table, provider, generator))
                {
                    isValid = false;
                }
            }
            
            return isValid;
        }

        private bool ValidateTable(Models.TableModel table, DatabaseProvider provider, IDdlGenerator generator)
        {
            bool isValid = true;
            
            // Validate indexes
            foreach (var index in table.Indexes)
            {
                if (!generator.ValidateIndexType(index.IndexType))
                {
                    _logger.LogError("Index type {IndexType} is not supported by {Provider} for index {IndexName} on table {TableName}",
                        index.IndexType, provider, index.Name, table.FullName);
                    isValid = false;
                }
            }
            
            // Validate column data types
            foreach (var column in table.Columns)
            {
                try
                {
                    var dbType = generator.MapNetTypeToDbType(column.PropertyType, column);
                    _logger.LogDebug("Column {ColumnName} ({NetType}) mapped to {DbType}",
                        column.Name, column.PropertyType.Name, dbType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to map .NET type {NetType} to database type for column {ColumnName} in table {TableName}",
                        column.PropertyType.Name, column.Name, table.FullName);
                    isValid = false;
                }
            }
            
            // Validate provider-specific features
            if (!ValidateProviderSpecificFeatures(table, provider))
            {
                isValid = false;
            }
            
            return isValid;
        }

        private bool ValidateProviderSpecificFeatures(Models.TableModel table, DatabaseProvider provider)
        {
            bool isValid = true;
            
            // Check schema support
            if (!string.IsNullOrEmpty(table.Schema) && !provider.SupportsSchemas())
            {
                _logger.LogWarning("Schema '{Schema}' specified for table {TableName}, but {Provider} does not support schemas",
                    table.Schema, table.Name, provider);
                // This is a warning, not an error
            }
            
            // Check for SQLite-specific limitations
            if (provider == DatabaseProvider.SQLite)
            {
                // Check for unsupported constraint types
                var unsupportedConstraints = table.Constraints
                    .Where(c => c.Type == Models.ConstraintType.ForeignKey && 
                               (c.OnDelete != ReferentialAction.NoAction || c.OnUpdate != ReferentialAction.NoAction))
                    .ToList();
                
                foreach (var constraint in unsupportedConstraints)
                {
                    _logger.LogWarning("Foreign key constraint {ConstraintName} uses referential actions which have limited support in SQLite",
                        constraint.Name);
                }
            }
            
            return isValid;
        }

        private IDdlGenerator GetDdlGenerator(DatabaseProvider provider)
        {
            var generator = _ddlGenerators.FirstOrDefault(g => g.Provider == provider);
            if (generator == null)
            {
                throw new NotSupportedException($"No DDL generator found for provider: {provider}");
            }
            return generator;
        }
    }
}