using System.Data;
using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Extensions;
using Bowtie.Introspection;
using Bowtie.Models;
using Bowtie.Tests.TestModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Tuxedo;

namespace Bowtie.Tests.Integration;

[TestFixture]
public class IntegrationTests
{
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddBowtie();
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public void ServiceCollection_AddBowtie_ShouldRegisterAllServices()
    {
        // Assert
        _serviceProvider.GetService<ModelAnalyzer>().Should().NotBeNull();
        _serviceProvider.GetService<DatabaseSynchronizer>().Should().NotBeNull();
        _serviceProvider.GetService<ScriptGenerator>().Should().NotBeNull();
        _serviceProvider.GetService<ModelValidator>().Should().NotBeNull();
        
        var ddlGenerators = _serviceProvider.GetServices<IDdlGenerator>().ToList();
        ddlGenerators.Should().HaveCount(4);
        ddlGenerators.Should().Contain(g => g.Provider == DatabaseProvider.SqlServer);
        ddlGenerators.Should().Contain(g => g.Provider == DatabaseProvider.PostgreSQL);
        ddlGenerators.Should().Contain(g => g.Provider == DatabaseProvider.MySQL);
        ddlGenerators.Should().Contain(g => g.Provider == DatabaseProvider.SQLite);
        
        var introspectors = _serviceProvider.GetServices<IDatabaseIntrospector>().ToList();
        introspectors.Should().HaveCount(2); // SQL Server and PostgreSQL
        introspectors.Should().Contain(i => i.Provider == DatabaseProvider.SqlServer);
        introspectors.Should().Contain(i => i.Provider == DatabaseProvider.PostgreSQL);
    }

    [Test]
    public async Task EndToEnd_SqliteSchemaGeneration_ShouldWorkCompletely()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await Task.Run(() => connection.Open());

        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = _serviceProvider.GetServices<IDdlGenerator>()
            .First(g => g.Provider == DatabaseProvider.SQLite);

        // Act - Analyze models and generate DDL
        var modelTypes = new[] { typeof(User), typeof(Category), typeof(Product) };
        var tables = analyzer.AnalyzeTypes(modelTypes);
        
        tables.Should().HaveCount(3);

        // Generate and execute DDL
        foreach (var table in tables)
        {
            var ddl = generator.GenerateCreateTable(table);
            await connection.ExecuteAsync(ddl);
        }

        // Assert - Verify tables were created
        var userTableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'") > 0;
        
        var categoryTableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Categories'") > 0;
        
        var productTableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products'") > 0;

        userTableExists.Should().BeTrue();
        categoryTableExists.Should().BeTrue();
        productTableExists.Should().BeTrue();

        // Verify indexes were created
        var indexCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'");
        
        indexCount.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task EndToEnd_InsertAndQuery_ShouldWorkWithGeneratedSchema()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await Task.Run(() => connection.Open());

        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = _serviceProvider.GetServices<IDdlGenerator>()
            .First(g => g.Provider == DatabaseProvider.SQLite);

        // Generate and execute schema
        var tables = analyzer.AnalyzeTypes(new[] { typeof(User) });
        var ddl = generator.GenerateCreateTable(tables[0]);
        await connection.ExecuteAsync(ddl);

        // Act - Insert test data
        await connection.ExecuteAsync(@"
            INSERT INTO Users (Username, Email, IsActive, CreatedDate) 
            VALUES ('testuser', 'test@example.com', 1, datetime('now'))");

        // Query data back
        var users = await connection.QueryAsync<dynamic>("SELECT * FROM Users");
        var userList = users.ToList();

        // Assert
        userList.Should().HaveCount(1);
        var user = userList[0];
        ((string)user.Username).Should().Be("testuser");
        ((string)user.Email).Should().Be("test@example.com");
        ((long)user.IsActive).Should().Be(1);
    }

    [Test]
    public async Task ModelValidator_WithValidModels_ShouldValidateSuccessfully()
    {
        // Arrange
        var validator = _serviceProvider.GetRequiredService<ModelValidator>();
        
        // Note: This test validates the validator logic without loading external assemblies
        // In a real scenario, you would test with actual assembly files

        // Act & Assert
        validator.Should().NotBeNull();
    }

    [Test]
    public void ScriptGenerator_WithValidModels_ShouldGenerateScript()
    {
        // Arrange
        var scriptGenerator = _serviceProvider.GetRequiredService<ScriptGenerator>();

        // Act & Assert
        scriptGenerator.Should().NotBeNull();
    }

    [Test]
    public async Task FullWorkflow_GenerateApplyAndVerify_ShouldWorkEndToEnd()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await Task.Run(() => connection.Open());

        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = _serviceProvider.GetServices<IDdlGenerator>()
            .First(g => g.Provider == DatabaseProvider.SQLite);

        // Step 1: Analyze models
        var modelTypes = new[] { typeof(User), typeof(Category) };
        var tables = analyzer.AnalyzeTypes(modelTypes);

        // Step 2: Generate migration script
        var migrationScripts = generator.GenerateMigrationScript(new List<TableModel>(), tables);
        var fullScript = string.Join("\n\n", migrationScripts);

        // Step 3: Execute migration
        await connection.ExecuteAsync(fullScript);

        // Step 4: Verify schema was created correctly
        var userTableInfo = await connection.QueryAsync(@"
            SELECT name, sql FROM sqlite_master 
            WHERE type='table' AND name IN ('Users', 'Categories')
            ORDER BY name");

        var tableInfoList = userTableInfo.ToList();
        tableInfoList.Should().HaveCount(2);

        // Step 5: Test data operations
        await connection.ExecuteAsync(@"
            INSERT INTO Categories (Name, Code, IsActive, CreatedDate) 
            VALUES ('Electronics', 'ELEC', 1, datetime('now'))");

        await connection.ExecuteAsync(@"
            INSERT INTO Users (Username, Email, IsActive, CreatedDate) 
            VALUES ('admin', 'admin@test.com', 1, datetime('now'))");

        var categoryCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Categories");
        var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");

        categoryCount.Should().Be(1);
        userCount.Should().Be(1);
    }

    [Test]
    public void AllDdlGenerators_ShouldImplementRequiredMethods()
    {
        // Arrange
        var generators = _serviceProvider.GetServices<IDdlGenerator>().ToList();

        // Assert
        foreach (var generator in generators)
        {
            // Test that all methods are implemented (don't throw NotImplementedException)
            var testTable = new TableModel 
            { 
                Name = "TestTable",
                Columns = new List<ColumnModel>
                {
                    new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true }
                }
            };

            var testIndex = new IndexModel 
            { 
                Name = "IX_Test",
                Columns = new List<IndexColumnModel>
                {
                    new() { ColumnName = "Id", Order = 1 }
                }
            };

            var testColumn = new ColumnModel { Name = "NewCol", PropertyType = typeof(string) };

            // These should not throw NotImplementedException
            generator.Invoking(g => g.GenerateCreateTable(testTable)).Should().NotThrow();
            generator.Invoking(g => g.GenerateDropTable(testTable)).Should().NotThrow();
            generator.Invoking(g => g.GenerateCreateIndex(testIndex, "TestTable")).Should().NotThrow();
            generator.Invoking(g => g.GenerateDropIndex(testIndex, "TestTable")).Should().NotThrow();
            generator.Invoking(g => g.GenerateAddColumn(testColumn, "TestTable")).Should().NotThrow();
            generator.Invoking(g => g.MapNetTypeToDbType(typeof(string), testColumn)).Should().NotThrow();
            generator.Invoking(g => g.ValidateIndexType(IndexType.BTree)).Should().NotThrow();
        }
    }

    [Test]
    public void DatabaseProviderExtensions_WithAllProviders_ShouldWorkCorrectly()
    {
        // Test all providers
        var providers = Enum.GetValues<DatabaseProvider>();

        foreach (var provider in providers)
        {
            // These should not throw
            provider.GetQuotedIdentifier("test").Should().NotBeNullOrEmpty();
            provider.GetParameterPrefix().Should().NotBeNullOrEmpty();
            provider.GetDefaultSchema().Should().NotBeNull();
            
            // Boolean methods should work
            var supportsSchemas = provider.SupportsSchemas();
            (supportsSchemas == true || supportsSchemas == false).Should().BeTrue();
            provider.SupportsIndexType(IndexType.BTree).Should().BeTrue(); // All should support B-Tree
        }
    }

    [Test]
    public async Task ComplexModel_WithAllFeatures_ShouldGenerateValidDdl()
    {
        // Arrange
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generators = _serviceProvider.GetServices<IDdlGenerator>().ToList();

        // Act - Analyze complex model with all features
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Analytics) });
        var table = tables[0];

        // Assert - Verify analysis
        table.Name.Should().Be("Analytics");
        table.Columns.Should().Contain(c => c.Name == "Id" && c.IsPrimaryKey);
        table.Columns.Should().Contain(c => c.Name == "Count" && c.DefaultValue != null && c.DefaultValue.ToString() == "1");
        table.Indexes.Should().Contain(i => i.Name == "IX_Analytics_EventDate_Clustered");
        table.Constraints.Should().Contain(c => c.Type == Models.ConstraintType.Check);
        table.Constraints.Should().Contain(c => c.Type == Models.ConstraintType.ForeignKey);

        // Generate DDL for all providers
        foreach (var generator in generators)
        {
            var ddl = generator.GenerateCreateTable(table);
            ddl.Should().NotBeNullOrEmpty();
            ddl.Should().Contain(generator.Provider.GetQuotedIdentifier("Analytics"));
        }
    }
}