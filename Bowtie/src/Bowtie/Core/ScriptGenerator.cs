using System.Reflection;
using System.Text;
using Bowtie.Analysis;
using Bowtie.DDL;
using Microsoft.Extensions.Logging;

namespace Bowtie.Core
{
    public class ScriptGenerator
    {
        private readonly ModelAnalyzer _modelAnalyzer;
        private readonly IEnumerable<IDdlGenerator> _ddlGenerators;
        private readonly ILogger<ScriptGenerator> _logger;

        public ScriptGenerator(
            ModelAnalyzer modelAnalyzer,
            IEnumerable<IDdlGenerator> ddlGenerators,
            ILogger<ScriptGenerator> logger)
        {
            _modelAnalyzer = modelAnalyzer;
            _ddlGenerators = ddlGenerators;
            _logger = logger;
        }

        public async Task GenerateAsync(string assemblyPath, DatabaseProvider provider, string? defaultSchema, string outputPath)
        {
            var generator = GetDdlGenerator(provider);
            
            _logger.LogInformation("Loading assembly: {AssemblyPath}", assemblyPath);
            var assembly = Assembly.LoadFrom(assemblyPath);
            
            _logger.LogInformation("Analyzing models in assembly...");
            var tables = _modelAnalyzer.AnalyzeAssembly(assembly, defaultSchema ?? provider.GetDefaultSchema());
            
            _logger.LogInformation("Found {TableCount} table models", tables.Count);
            
            var sb = new StringBuilder();
            sb.AppendLine($"-- Generated DDL for {provider}");
            sb.AppendLine($"-- Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Assembly: {assemblyPath}");
            sb.AppendLine();
            
            foreach (var table in tables)
            {
                _logger.LogDebug("Generating DDL for table: {TableName}", table.FullName);
                
                sb.AppendLine($"-- Table: {table.FullName}");
                sb.AppendLine(generator.GenerateCreateTable(table));
                sb.AppendLine();
            }
            
            var sql = sb.ToString();
            await File.WriteAllTextAsync(outputPath, sql);
            
            _logger.LogInformation("DDL script written to: {OutputPath}", outputPath);
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