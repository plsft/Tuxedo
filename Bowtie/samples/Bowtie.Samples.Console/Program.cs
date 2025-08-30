using Bowtie.Extensions;
using Bowtie.Samples.Console.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using Bowtie.Core;
using Bowtie.Attributes;
using IoFile = System.IO.File;

namespace Bowtie.Samples.Console;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        
        // Configure services
        builder.Services.AddBowtie();
        
        var app = builder.Build();
        
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Bowtie Sample Application Starting...");
        
        try
        {
            // Example 1: Generate DDL scripts for different providers
            await GenerateDdlScriptsAsync(app.Services, logger);
            
            // Example 2: Validate models for different providers
            await ValidateModelsAsync(app.Services, logger);
            
            // Example 3: Synchronize with SQLite database (in-memory)
            await SynchronizeWithSqliteAsync(app.Services, logger);
            
            // Example 4: Show model analysis
            await AnalyzeModelsAsync(app.Services, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred");
        }
        
        logger.LogInformation("Sample application completed. Press any key to exit.");
        System.Console.ReadKey();
    }
    
    static async Task GenerateDdlScriptsAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Generating DDL Scripts ===");
        
        var providers = new[]
        {
            DatabaseProvider.SqlServer,
            DatabaseProvider.PostgreSQL,
            DatabaseProvider.MySQL,
            DatabaseProvider.SQLite
        };
        
        foreach (var provider in providers)
        {
            try
            {
                var outputPath = $"schema_{provider.ToString().ToLower()}.sql";
                
                await services.GenerateDdlScriptsAsync(
                    provider: provider,
                    outputPath: outputPath
                );
                
                logger.LogInformation("Generated DDL script for {Provider}: {OutputPath}", provider, outputPath);
                
                // Show first few lines of the generated script
                if (IoFile.Exists(outputPath))
                {
                    var lines = await IoFile.ReadAllLinesAsync(outputPath);
                    logger.LogInformation("Preview of {Provider} script:", provider);
                    foreach (var line in lines.Take(10))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            logger.LogInformation("  {Line}", line);
                    }
                    if (lines.Length > 10)
                        logger.LogInformation("  ... ({TotalLines} total lines)", lines.Length);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate DDL for {Provider}", provider);
            }
        }
    }
    
    static async Task ValidateModelsAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Validating Models ===");
        
        var providers = new[]
        {
            DatabaseProvider.SqlServer,
            DatabaseProvider.PostgreSQL,
            DatabaseProvider.MySQL,
            DatabaseProvider.SQLite
        };
        
        foreach (var provider in providers)
        {
            try
            {
                var isValid = services.ValidateModels(provider);
                
                logger.LogInformation("Model validation for {Provider}: {Result}", 
                    provider, isValid ? "✓ PASSED" : "✗ FAILED");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate models for {Provider}", provider);
            }
        }
    }
    
    static async Task SynchronizeWithSqliteAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== SQLite Synchronization Demo ===");
        
        try
        {
            var connectionString = "Data Source=:memory:";
            
            // First, let's generate the script to see what would be created
            logger.LogInformation("Generating migration script (dry run)...");
            await services.SynchronizeDatabaseAsync(
                connectionString: connectionString,
                provider: DatabaseProvider.SQLite,
                dryRun: true,
                outputFile: "sqlite_migration.sql"
            );
            
            if (IoFile.Exists("sqlite_migration.sql"))
            {
                var script = await IoFile.ReadAllTextAsync("sqlite_migration.sql");
                logger.LogInformation("Generated migration script:");
                logger.LogInformation(script);
            }
            
            // Note: In-memory SQLite database would be destroyed after connection closes
            // For a real example, you'd use a file-based database
            logger.LogInformation("SQLite synchronization demo completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize with SQLite");
        }
    }
    
    static async Task AnalyzeModelsAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("=== Model Analysis ===");
        
        try
        {
            var modelAnalyzer = services.GetRequiredService<Bowtie.Analysis.ModelAnalyzer>();
            
            var modelTypes = new[]
            {
                typeof(User),
                typeof(Blog),
                typeof(Post),
                typeof(Tag),
                typeof(PostTag),
                typeof(Analytics),
                typeof(Configuration),
                typeof(FileModel),
                typeof(AuditLog)
            };
            
            var tables = modelAnalyzer.AnalyzeTypes(modelTypes);
            
            logger.LogInformation("Analyzed {Count} model types:", tables.Count);
            
            foreach (var table in tables)
            {
                logger.LogInformation("Table: {TableName}", table.FullName);
                logger.LogInformation("  Columns: {ColumnCount}", table.Columns.Count);
                logger.LogInformation("  Indexes: {IndexCount}", table.Indexes.Count);
                logger.LogInformation("  Constraints: {ConstraintCount}", table.Constraints.Count);
                
                // Show some interesting attributes
                var specialColumns = table.Columns.Where(c => 
                    c.IsPrimaryKey || c.IsIdentity || c.DefaultValue != null).ToList();
                
                if (specialColumns.Any())
                {
                    logger.LogInformation("  Special columns:");
                    foreach (var col in specialColumns)
                    {
                        var attributes = new List<string>();
                        if (col.IsPrimaryKey) attributes.Add("PK");
                        if (col.IsIdentity) attributes.Add("Identity");
                        if (col.DefaultValue != null) attributes.Add($"Default: {col.DefaultValue}");
                        
                        logger.LogInformation("    {ColumnName}: {Attributes}", 
                            col.Name, string.Join(", ", attributes));
                    }
                }
                
                // Show indexes with special types
                var specialIndexes = table.Indexes.Where(i => 
                    i.IndexType != IndexType.BTree || i.IsUnique).ToList();
                
                if (specialIndexes.Any())
                {
                    logger.LogInformation("  Special indexes:");
                    foreach (var idx in specialIndexes)
                    {
                        var attributes = new List<string>();
                        if (idx.IsUnique) attributes.Add("Unique");
                        if (idx.IndexType != IndexType.BTree) attributes.Add(idx.IndexType.ToString());
                        
                        logger.LogInformation("    {IndexName}: {Columns} ({Attributes})", 
                            idx.Name, 
                            string.Join(", ", idx.Columns.Select(c => c.ColumnName)),
                            string.Join(", ", attributes));
                    }
                }
                
                logger.LogInformation("");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze models");
        }
    }
}