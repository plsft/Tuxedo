using System.CommandLine;
using System.Reflection;
using Bowtie.Analysis;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Introspection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bowtie.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Bowtie - Database schema synchronization tool for Tuxedo ORM");

        var syncCommand = CreateSyncCommand();
        var generateCommand = CreateGenerateCommand();
        var validateCommand = CreateValidateCommand();

        rootCommand.AddCommand(syncCommand);
        rootCommand.AddCommand(generateCommand);
        rootCommand.AddCommand(validateCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateSyncCommand()
    {
        var assemblyOption = new Option<string>(
            name: "--assembly",
            description: "Path to the assembly containing the models")
        { IsRequired = true };

        var connectionStringOption = new Option<string>(
            name: "--connection-string",
            description: "Database connection string")
        { IsRequired = true };

        var providerOption = new Option<DatabaseProvider>(
            name: "--provider",
            description: "Database provider")
        { IsRequired = true };

        var schemaOption = new Option<string?>(
            name: "--schema",
            description: "Default schema name");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Generate SQL without executing");

        var outputOption = new Option<string?>(
            name: "--output",
            description: "Output file for generated SQL");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose logging");

        var forceOption = new Option<bool>(
            name: "--force",
            description: "Force migration even if data loss risks are detected (DANGEROUS)");

        var command = new Command("sync", "Synchronize database schema with models");
        command.AddOption(assemblyOption);
        command.AddOption(connectionStringOption);
        command.AddOption(providerOption);
        command.AddOption(schemaOption);
        command.AddOption(dryRunOption);
        command.AddOption(outputOption);
        command.AddOption(verboseOption);
        command.AddOption(forceOption);

        command.SetHandler(async (string assemblyPath, string connectionString, DatabaseProvider provider, 
            string? schema, bool dryRun, string? output, bool verbose, bool force) =>
        {
            var services = ConfigureServices(verbose);
            var serviceProvider = services.BuildServiceProvider();
            
            var synchronizer = serviceProvider.GetRequiredService<DatabaseSynchronizer>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Starting database synchronization...");
                await synchronizer.SynchronizeAsync(assemblyPath, connectionString, provider, schema, dryRun, output, force);
                logger.LogInformation("Database synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database synchronization failed");
                Environment.Exit(1);
            }
        }, assemblyOption, connectionStringOption, providerOption, schemaOption, dryRunOption, outputOption, verboseOption, forceOption);

        return command;
    }

    private static Command CreateGenerateCommand()
    {
        var assemblyOption = new Option<string>(
            name: "--assembly",
            description: "Path to the assembly containing the models")
        { IsRequired = true };

        var providerOption = new Option<DatabaseProvider>(
            name: "--provider",
            description: "Database provider")
        { IsRequired = true };

        var schemaOption = new Option<string?>(
            name: "--schema",
            description: "Default schema name");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output file for generated SQL")
        { IsRequired = true };

        var command = new Command("generate", "Generate DDL scripts from models");
        command.AddOption(assemblyOption);
        command.AddOption(providerOption);
        command.AddOption(schemaOption);
        command.AddOption(outputOption);

        command.SetHandler(async (string assemblyPath, DatabaseProvider provider, string? schema, string output) =>
        {
            var services = ConfigureServices(false);
            var serviceProvider = services.BuildServiceProvider();
            
            var generator = serviceProvider.GetRequiredService<ScriptGenerator>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Generating DDL scripts...");
                await generator.GenerateAsync(assemblyPath, provider, schema, output);
                logger.LogInformation($"DDL scripts generated successfully to {output}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DDL generation failed");
                Environment.Exit(1);
            }
        }, assemblyOption, providerOption, schemaOption, outputOption);

        return command;
    }

    private static Command CreateValidateCommand()
    {
        var assemblyOption = new Option<string>(
            name: "--assembly",
            description: "Path to the assembly containing the models")
        { IsRequired = true };

        var providerOption = new Option<DatabaseProvider>(
            name: "--provider",
            description: "Database provider")
        { IsRequired = true };

        var command = new Command("validate", "Validate model attributes for the specified provider");
        command.AddOption(assemblyOption);
        command.AddOption(providerOption);

        command.SetHandler((string assemblyPath, DatabaseProvider provider) =>
        {
            var services = ConfigureServices(false);
            var serviceProvider = services.BuildServiceProvider();
            
            var validator = serviceProvider.GetRequiredService<ModelValidator>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Validating models...");
                var isValid = validator.Validate(assemblyPath, provider);
                
                if (isValid)
                {
                    logger.LogInformation("All models are valid for the specified provider.");
                }
                else
                {
                    logger.LogError("Model validation failed.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Model validation failed");
                Environment.Exit(1);
            }
        }, assemblyOption, providerOption);

        return command;
    }

    private static ServiceCollection ConfigureServices(bool verbose)
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
        });

        services.AddTransient<ModelAnalyzer>();
        services.AddTransient<DataLossAnalyzer>();
        services.AddTransient<DatabaseSynchronizer>();
        services.AddTransient<ScriptGenerator>();
        services.AddTransient<ModelValidator>();
        
        services.AddTransient<IDdlGenerator, SqlServerDdlGenerator>();
        services.AddTransient<IDdlGenerator, PostgreSqlDdlGenerator>();
        services.AddTransient<IDdlGenerator, MySqlDdlGenerator>();
        services.AddTransient<IDdlGenerator, SqliteDdlGenerator>();
        
        services.AddTransient<IDatabaseIntrospector, SqlServerIntrospector>();
        services.AddTransient<IDatabaseIntrospector, PostgreSqlIntrospector>();

        return services;
    }
}