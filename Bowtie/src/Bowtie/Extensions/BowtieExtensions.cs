using System.Reflection;
using Bowtie.Analysis;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Introspection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bowtie.Extensions
{
    public static class BowtieExtensions
    {
        /// <summary>
        /// Synchronize database schema with models from the current assembly
        /// </summary>
        public static async Task SynchronizeDatabaseAsync(
            this IServiceProvider serviceProvider,
            string connectionString,
            DatabaseProvider provider,
            string? defaultSchema = null,
            bool dryRun = false,
            string? outputFile = null)
        {
            var synchronizer = serviceProvider.GetRequiredService<DatabaseSynchronizer>();
            var assembly = Assembly.GetCallingAssembly();
            
            await synchronizer.SynchronizeAsync(
                assembly.Location,
                connectionString,
                provider,
                defaultSchema,
                dryRun,
                outputFile);
        }

        /// <summary>
        /// Synchronize database schema with models from a specific assembly
        /// </summary>
        public static async Task SynchronizeDatabaseAsync(
            this IServiceProvider serviceProvider,
            Assembly assembly,
            string connectionString,
            DatabaseProvider provider,
            string? defaultSchema = null,
            bool dryRun = false,
            string? outputFile = null)
        {
            var synchronizer = serviceProvider.GetRequiredService<DatabaseSynchronizer>();
            
            await synchronizer.SynchronizeAsync(
                assembly.Location,
                connectionString,
                provider,
                defaultSchema,
                dryRun,
                outputFile);
        }

        /// <summary>
        /// Synchronize database schema with specific model types
        /// </summary>
        public static async Task SynchronizeDatabaseAsync(
            this IServiceProvider serviceProvider,
            IEnumerable<Type> modelTypes,
            string connectionString,
            DatabaseProvider provider,
            string? defaultSchema = null,
            bool dryRun = false,
            string? outputFile = null)
        {
            var modelAnalyzer = serviceProvider.GetRequiredService<ModelAnalyzer>();
            var ddlGenerators = serviceProvider.GetServices<IDdlGenerator>();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabaseSynchronizer>>();
            
            var introspectors = serviceProvider.GetServices<IDatabaseIntrospector>();
            var synchronizer = new DatabaseSynchronizer(modelAnalyzer, ddlGenerators, introspectors, logger);
            var generator = ddlGenerators.FirstOrDefault(g => g.Provider == provider);
            
            if (generator == null)
            {
                throw new NotSupportedException($"No DDL generator found for provider: {provider}");
            }

            var targetTables = modelAnalyzer.AnalyzeTypes(modelTypes, defaultSchema ?? provider.GetDefaultSchema());
            
            if (dryRun)
            {
                var sql = string.Join("\n\n", generator.GenerateMigrationScript(new List<Models.TableModel>(), targetTables));
                
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, sql);
                }
                else
                {
                    Console.WriteLine(sql);
                }
                return;
            }

            // For production usage, you would implement the actual database synchronization logic here
            throw new NotImplementedException("Direct type synchronization is not yet implemented for production use. Use the CLI tool instead.");
        }

        /// <summary>
        /// Generate DDL scripts from models in the current assembly
        /// </summary>
        public static async Task GenerateDdlScriptsAsync(
            this IServiceProvider serviceProvider,
            DatabaseProvider provider,
            string outputPath,
            string? defaultSchema = null)
        {
            var generator = serviceProvider.GetRequiredService<ScriptGenerator>();
            var assembly = Assembly.GetCallingAssembly();
            
            await generator.GenerateAsync(assembly.Location, provider, defaultSchema, outputPath);
        }

        /// <summary>
        /// Generate DDL scripts from models in a specific assembly
        /// </summary>
        public static async Task GenerateDdlScriptsAsync(
            this IServiceProvider serviceProvider,
            Assembly assembly,
            DatabaseProvider provider,
            string outputPath,
            string? defaultSchema = null)
        {
            var generator = serviceProvider.GetRequiredService<ScriptGenerator>();
            
            await generator.GenerateAsync(assembly.Location, provider, defaultSchema, outputPath);
        }

        /// <summary>
        /// Validate models in the current assembly for the specified provider
        /// </summary>
        public static bool ValidateModels(
            this IServiceProvider serviceProvider,
            DatabaseProvider provider)
        {
            var validator = serviceProvider.GetRequiredService<ModelValidator>();
            var assembly = Assembly.GetCallingAssembly();
            
            return validator.Validate(assembly.Location, provider);
        }

        /// <summary>
        /// Validate models in a specific assembly for the specified provider
        /// </summary>
        public static bool ValidateModels(
            this IServiceProvider serviceProvider,
            Assembly assembly,
            DatabaseProvider provider)
        {
            var validator = serviceProvider.GetRequiredService<ModelValidator>();
            
            return validator.Validate(assembly.Location, provider);
        }
    }
}