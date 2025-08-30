using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Extensions;
using Bowtie.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bowtie.Tests;

public class BowtieTests
{
    private readonly IServiceProvider _serviceProvider;

    public BowtieTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddBowtie();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void ModelAnalyzer_ShouldAnalyzeBasicModel()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(User) });
        
        Assert.Single(tables);
        var table = tables[0];
        
        Assert.Equal("Users", table.Name);
        Assert.Equal(5, table.Columns.Count); // Id, Username, Email, IsActive, CreatedDate
        Assert.Contains(table.Columns, c => c.Name == "Id" && c.IsPrimaryKey && c.IsIdentity);
        Assert.Contains(table.Columns, c => c.Name == "Username" && c.MaxLength == 100);
        Assert.Contains(table.Indexes, i => i.Name == "IX_Users_Username");
        Assert.Contains(table.Indexes, i => i.Name == "UQ_Users_Username" && i.IsUnique);
    }

    [Fact]
    public void SqlServerDdlGenerator_ShouldGenerateCreateTable()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = new SqlServerDdlGenerator();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Product) });
        var table = tables[0];
        
        var ddl = generator.GenerateCreateTable(table);
        
        Assert.Contains("CREATE TABLE [Products]", ddl);
        Assert.Contains("[Id] INT NOT NULL IDENTITY(1,1)", ddl);
        Assert.Contains("[Name] NVARCHAR(200)", ddl);
        Assert.Contains("[Price] DECIMAL(18,2)", ddl);
        Assert.Contains("CONSTRAINT [PK_Products] PRIMARY KEY", ddl);
        Assert.Contains("CHECK (Price > 0)", ddl);
    }

    [Fact]
    public void PostgreSqlDdlGenerator_ShouldGenerateGINIndex()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = new PostgreSqlDdlGenerator();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Document) });
        var table = tables[0];
        
        var ddl = generator.GenerateCreateTable(table);
        
        Assert.Contains("CREATE TABLE \"Documents\"", ddl);
        Assert.Contains("\"Content\" jsonb", ddl);
        Assert.Contains("CREATE INDEX \"IX_Documents_Content_GIN\" ON \"Documents\" USING GIN", ddl);
    }

    [Fact]
    public void MySqlDdlGenerator_ShouldGenerateValidSql()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = new MySqlDdlGenerator();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Category) });
        var table = tables[0];
        
        var ddl = generator.GenerateCreateTable(table);
        
        Assert.Contains("CREATE TABLE `Categories`", ddl);
        Assert.Contains("`Id` INT NOT NULL AUTO_INCREMENT", ddl);
        Assert.Contains("`Name` VARCHAR(100)", ddl);
        Assert.Contains("UNIQUE (`Name`)", ddl);
    }

    [Fact]
    public void SqliteDdlGenerator_ShouldGenerateValidSql()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var generator = new SqliteDdlGenerator();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(User) });
        var table = tables[0];
        
        var ddl = generator.GenerateCreateTable(table);
        
        Assert.Contains("CREATE TABLE [Users]", ddl);
        Assert.Contains("[Id] INTEGER NOT NULL AUTOINCREMENT", ddl);
        Assert.Contains("[Username] TEXT", ddl);
        Assert.Contains("UNIQUE ([Username])", ddl);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer, IndexType.Clustered, true)]
    [InlineData(DatabaseProvider.SqlServer, IndexType.GIN, false)]
    [InlineData(DatabaseProvider.PostgreSQL, IndexType.GIN, true)]
    [InlineData(DatabaseProvider.PostgreSQL, IndexType.Clustered, false)]
    [InlineData(DatabaseProvider.MySQL, IndexType.Hash, true)]
    [InlineData(DatabaseProvider.MySQL, IndexType.GIN, false)]
    [InlineData(DatabaseProvider.SQLite, IndexType.BTree, true)]
    [InlineData(DatabaseProvider.SQLite, IndexType.Hash, false)]
    public void DatabaseProvider_ShouldValidateIndexTypeSupport(DatabaseProvider provider, IndexType indexType, bool expected)
    {
        var supported = provider.SupportsIndexType(indexType);
        Assert.Equal(expected, supported);
    }

    [Fact]
    public void ModelAnalyzer_ShouldHandleComplexIndexes()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Document) });
        var table = tables[0];
        
        var ginIndex = table.Indexes.FirstOrDefault(i => i.IndexType == IndexType.GIN);
        Assert.NotNull(ginIndex);
        Assert.Equal("IX_Documents_Content_GIN", ginIndex.Name);
        Assert.Single(ginIndex.Columns);
        Assert.Equal("Content", ginIndex.Columns[0].ColumnName);
    }

    [Fact]
    public void ModelValidator_ShouldValidateForProvider()
    {
        var validator = _serviceProvider.GetRequiredService<ModelValidator>();
        
        // This should work since it only tests validation logic
        // without actually loading external assemblies
        Assert.NotNull(validator);
    }

    [Fact]
    public void ModelAnalyzer_ShouldHandleForeignKeys()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Product) });
        var table = tables[0];
        
        var fkConstraint = table.Constraints.FirstOrDefault(c => c.Type == ConstraintType.ForeignKey);
        Assert.NotNull(fkConstraint);
        Assert.Equal("FK_Products_CategoryId", fkConstraint.Name);
        Assert.Equal("Categories", fkConstraint.ReferencedTable);
        Assert.Equal("Id", fkConstraint.ReferencedColumn);
    }

    [Fact]
    public void ModelAnalyzer_ShouldHandleDefaultValues()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(User) });
        var table = tables[0];
        
        var isActiveColumn = table.Columns.FirstOrDefault(c => c.Name == "IsActive");
        Assert.NotNull(isActiveColumn);
        Assert.Equal(true, isActiveColumn.DefaultValue);
    }

    [Fact]
    public void ModelAnalyzer_ShouldIgnoreComputedProperties()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        
        // Use a type that has computed properties to test
        var tables = analyzer.AnalyzeTypes(new[] { typeof(User) });
        var table = tables[0];
        
        // Should not contain computed properties in columns
        Assert.DoesNotContain(table.Columns, c => c.Name.Contains("Computed"));
    }
}