using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Extensions;
using Bowtie.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tuxedo.Contrib;

namespace Bowtie.Validation;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddBowtie();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("🎭 Bowtie Validation Tool");
        logger.LogInformation("========================");
        
        await ValidateModelAnalysis(serviceProvider, logger);
        await ValidateDdlGeneration(serviceProvider, logger);
        await ValidateProviderSupport(serviceProvider, logger);
        await GenerateSampleScripts(serviceProvider, logger);
        
        logger.LogInformation("✅ All validations completed successfully!");
    }
    
    static async Task ValidateModelAnalysis(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("\n📊 Model Analysis Validation");
        logger.LogInformation("============================");
        
        var analyzer = services.GetRequiredService<ModelAnalyzer>();
        
        var modelTypes = new[]
        {
            typeof(ValidationUser),
            typeof(ValidationProduct),
            typeof(ValidationDocument)
        };
        
        var tables = analyzer.AnalyzeTypes(modelTypes);
        
        logger.LogInformation($"✅ Analyzed {tables.Count} models successfully");
        
        foreach (var table in tables)
        {
            logger.LogInformation($"  📋 {table.Name}: {table.Columns.Count} columns, {table.Indexes.Count} indexes, {table.Constraints.Count} constraints");
        }
    }
    
    static async Task ValidateDdlGeneration(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("\n🔧 DDL Generation Validation");
        logger.LogInformation("============================");
        
        var analyzer = services.GetRequiredService<ModelAnalyzer>();
        var generators = new IDdlGenerator[]
        {
            new SqlServerDdlGenerator(),
            new PostgreSqlDdlGenerator(),
            new MySqlDdlGenerator(),
            new SqliteDdlGenerator()
        };
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(ValidationProduct) });
        var table = tables[0];
        
        foreach (var generator in generators)
        {
            try
            {
                var ddl = generator.GenerateCreateTable(table);
                logger.LogInformation($"✅ {generator.Provider}: DDL generated ({ddl.Length} chars)");
                
                // Show first line of DDL
                var firstLine = ddl.Split('\n')[0].Trim();
                logger.LogInformation($"   {firstLine}...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"❌ {generator.Provider}: Failed to generate DDL");
            }
        }
    }
    
    static async Task ValidateProviderSupport(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("\n🎯 Provider Feature Support");
        logger.LogInformation("===========================");
        
        var providers = new[] 
        { 
            DatabaseProvider.SqlServer, 
            DatabaseProvider.PostgreSQL, 
            DatabaseProvider.MySQL, 
            DatabaseProvider.SQLite 
        };
        
        var indexTypes = new[]
        {
            IndexType.BTree,
            IndexType.Hash,
            IndexType.GIN,
            IndexType.Clustered,
            IndexType.FullText,
            IndexType.Spatial
        };
        
        foreach (var provider in providers)
        {
            logger.LogInformation($"\n  📊 {provider}:");
            logger.LogInformation($"     Schemas: {provider.SupportsSchemas()}");
            
            foreach (var indexType in indexTypes)
            {
                var supported = provider.SupportsIndexType(indexType);
                var icon = supported ? "✅" : "❌";
                logger.LogInformation($"     {icon} {indexType}");
            }
        }
    }
    
    static async Task GenerateSampleScripts(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("\n📄 Sample Script Generation");
        logger.LogInformation("============================");
        
        var analyzer = services.GetRequiredService<ModelAnalyzer>();
        var generators = new[]
        {
            new SqlServerDdlGenerator(),
            new PostgreSqlDdlGenerator(),
            new MySqlDdlGenerator(),
            new SqliteDdlGenerator()
        };
        
        var modelTypes = new[]
        {
            typeof(ValidationUser),
            typeof(ValidationProduct),
            typeof(ValidationDocument)
        };
        
        var tables = analyzer.AnalyzeTypes(modelTypes);
        
        foreach (var generator in generators)
        {
            try
            {
                var scripts = generator.GenerateMigrationScript(new List<TableModel>(), tables);
                var fullScript = string.Join("\n\n", scripts);
                
                var fileName = $"sample_schema_{generator.Provider.ToString().ToLower()}.sql";
                await File.WriteAllTextAsync(fileName, fullScript);
                
                logger.LogInformation($"✅ {generator.Provider}: Generated {fileName} ({fullScript.Length} chars)");
                
                // Show some stats
                var lineCount = fullScript.Split('\n').Length;
                var tableCount = scripts.Count(s => s.Contains("CREATE TABLE"));
                var indexCount = scripts.Count(s => s.Contains("CREATE INDEX"));
                
                logger.LogInformation($"   📊 {lineCount} lines, {tableCount} tables, {indexCount} indexes");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"❌ {generator.Provider}: Failed to generate script");
            }
        }
    }
}

[Table("Users")]
public class ValidationUser
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Users_Username")]
    [Unique("UQ_Users_Username")]
    public string Username { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    [Index("IX_Users_Email")]
    public string Email { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Products")]
public class ValidationProduct
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_Products_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("Price > 0")]
    public decimal Price { get; set; }

    [ForeignKey("Categories")]
    public int CategoryId { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }
}

[Table("Documents")]
public class ValidationDocument
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
    public string Content { get; set; } = "{}";

    [Index("IX_Documents_CreatedDate_Clustered", IndexType = IndexType.Clustered)]
    public DateTime CreatedDate { get; set; }
}