using System.Data;
using System.Reflection;
using Bowtie.Analysis;
using Bowtie.DDL;
using Bowtie.Introspection;
using Bowtie.Models;
using Microsoft.Extensions.Logging;

namespace Bowtie.Core
{
    public class DatabaseSynchronizer
    {
        private readonly ModelAnalyzer _modelAnalyzer;
        private readonly DataLossAnalyzer _dataLossAnalyzer;
        private readonly IEnumerable<IDdlGenerator> _ddlGenerators;
        private readonly IEnumerable<IDatabaseIntrospector> _introspectors;
        private readonly ILogger<DatabaseSynchronizer> _logger;

        public DatabaseSynchronizer(
            ModelAnalyzer modelAnalyzer,
            DataLossAnalyzer dataLossAnalyzer,
            IEnumerable<IDdlGenerator> ddlGenerators,
            IEnumerable<IDatabaseIntrospector> introspectors,
            ILogger<DatabaseSynchronizer> logger)
        {
            _modelAnalyzer = modelAnalyzer;
            _dataLossAnalyzer = dataLossAnalyzer;
            _ddlGenerators = ddlGenerators;
            _introspectors = introspectors;
            _logger = logger;
        }

        public async Task SynchronizeAsync(
            string assemblyPath, 
            string connectionString, 
            DatabaseProvider provider, 
            string? defaultSchema, 
            bool dryRun, 
            string? outputFile,
            bool force = false)
        {
            var generator = GetDdlGenerator(provider);
            
            _logger.LogInformation("Loading assembly: {AssemblyPath}", assemblyPath);
            var assembly = Assembly.LoadFrom(assemblyPath);
            
            _logger.LogInformation("Analyzing models in assembly...");
            var targetTables = _modelAnalyzer.AnalyzeAssembly(assembly, defaultSchema ?? provider.GetDefaultSchema());
            
            _logger.LogInformation("Found {TableCount} table models", targetTables.Count);
            
            if (dryRun)
            {
                _logger.LogInformation("Dry run mode - generating SQL without execution");
                var sql = await GenerateMigrationSqlAsync(new List<TableModel>(), targetTables, generator);
                
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, sql);
                    _logger.LogInformation("SQL written to: {OutputFile}", outputFile);
                }
                else
                {
                    Console.WriteLine("Generated SQL:");
                    Console.WriteLine("=" + new string('=', 50));
                    Console.WriteLine(sql);
                }
                
                return;
            }
            
            _logger.LogInformation("Connecting to database...");
            using var connection = CreateConnection(provider, connectionString);
            await Task.Run(() => connection.Open());
            
            _logger.LogInformation("Analyzing existing database schema...");
            var currentTables = await GetCurrentTablesAsync(connection, provider, defaultSchema);
            
            _logger.LogInformation("Analyzing potential data loss risks...");
            var dataLossRisk = _dataLossAnalyzer.AnalyzeMigrationRisks(currentTables, targetTables);
            _dataLossAnalyzer.LogDataLossWarnings(dataLossRisk);
            
            // Check for data loss risks and require confirmation
            if (dataLossRisk.RequiresConfirmation && !force && !dryRun)
            {
                _logger.LogError("🚨 MIGRATION STOPPED: High risk operations detected!");
                _logger.LogError("Use --force flag to override this safety check, or --dry-run to generate script only.");
                _logger.LogError("STRONGLY RECOMMENDED: Backup your database before proceeding with --force.");
                throw new InvalidOperationException("Migration aborted due to data loss risks. Use --force to override or --dry-run to generate script.");
            }
            
            if (dataLossRisk.HasHighRiskOperations && force)
            {
                _logger.LogWarning("🚨 PROCEEDING WITH HIGH RISK MIGRATION due to --force flag!");
                _logger.LogWarning("Ensure you have backed up your database!");
            }
            
            _logger.LogInformation("Generating migration script...");
            var migrationSql = await GenerateMigrationSqlAsync(currentTables, targetTables, generator);
            
            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, migrationSql);
                _logger.LogInformation("Migration SQL written to: {OutputFile}", outputFile);
            }
            
            if (!string.IsNullOrWhiteSpace(migrationSql))
            {
                _logger.LogInformation("Executing migration script...");
                await ExecuteMigrationAsync(connection, migrationSql);
                _logger.LogInformation("Migration completed successfully");
            }
            else
            {
                _logger.LogInformation("No changes detected - database is already in sync");
            }
        }

        private async Task<string> GenerateMigrationSqlAsync(
            List<TableModel> currentTables, 
            List<TableModel> targetTables, 
            IDdlGenerator generator)
        {
            return await Task.Run(() =>
            {
                var statements = generator.GenerateMigrationScript(currentTables, targetTables);
                return string.Join("\n\n", statements.Where(s => !string.IsNullOrWhiteSpace(s)));
            });
        }

        private async Task<List<TableModel>> GetCurrentTablesAsync(IDbConnection connection, DatabaseProvider provider, string? schema)
        {
            var introspector = GetIntrospector(provider);
            return await introspector.GetTablesAsync(connection, schema);
        }

        private async Task ExecuteMigrationAsync(IDbConnection connection, string sql)
        {
            var statements = sql.Split(new[] { "GO", ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            using var command = connection.CreateCommand();
            
            foreach (var statement in statements)
            {
                if (string.IsNullOrWhiteSpace(statement)) continue;
                
                _logger.LogDebug("Executing: {Statement}", statement);
                command.CommandText = statement;
                await Task.Run(() => command.ExecuteNonQuery());
            }
        }

        private IDbConnection CreateConnection(DatabaseProvider provider, string connectionString)
        {
#pragma warning disable CS8603
            return provider switch
            {
#if NET6_0
                DatabaseProvider.SqlServer => new System.Data.SqlClient.SqlConnection(connectionString),
#else
                DatabaseProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
#endif
                DatabaseProvider.PostgreSQL => new Npgsql.NpgsqlConnection(connectionString),
                DatabaseProvider.MySQL => new MySqlConnector.MySqlConnection(connectionString),
                DatabaseProvider.SQLite => new Microsoft.Data.Sqlite.SqliteConnection(connectionString),
                _ => throw new NotSupportedException($"Provider {provider} is not supported")
            };
#pragma warning restore CS8603
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

        private IDatabaseIntrospector GetIntrospector(DatabaseProvider provider)
        {
            var introspector = _introspectors.FirstOrDefault(i => i.Provider == provider);
            if (introspector == null)
            {
                throw new NotSupportedException($"No introspector found for provider: {provider}");
            }
            return introspector;
        }
    }
}