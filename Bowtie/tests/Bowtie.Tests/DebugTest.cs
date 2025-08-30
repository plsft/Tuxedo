using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Extensions;
using Bowtie.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tuxedo.Contrib;
using Xunit;
using Xunit.Abstractions;

namespace Bowtie.Tests;

public class DebugTest
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public DebugTest(ITestOutputHelper output)
    {
        _output = output;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddBowtie();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShowGeneratedSql()
    {
        var analyzer = _serviceProvider.GetRequiredService<ModelAnalyzer>();
        var sqlGenerator = new SqlServerDdlGenerator();
        var postgresGenerator = new PostgreSqlDdlGenerator();
        var mysqlGenerator = new MySqlDdlGenerator();
        var sqliteGenerator = new SqliteDdlGenerator();

        var tables = analyzer.AnalyzeTypes(new[] { typeof(TestUser) });
        var table = tables[0];

        _output.WriteLine("=== SQL Server ===");
        _output.WriteLine(sqlGenerator.GenerateCreateTable(table));
        _output.WriteLine("");

        _output.WriteLine("=== PostgreSQL ===");
        _output.WriteLine(postgresGenerator.GenerateCreateTable(table));
        _output.WriteLine("");

        _output.WriteLine("=== MySQL ===");
        _output.WriteLine(mysqlGenerator.GenerateCreateTable(table));
        _output.WriteLine("");

        _output.WriteLine("=== SQLite ===");
        _output.WriteLine(sqliteGenerator.GenerateCreateTable(table));
        _output.WriteLine("");

        _output.WriteLine("=== Table Analysis ===");
        _output.WriteLine($"Table: {table.Name}");
        _output.WriteLine($"Columns: {table.Columns.Count}");
        _output.WriteLine($"Indexes: {table.Indexes.Count}");
        _output.WriteLine($"Constraints: {table.Constraints.Count}");

        foreach (var index in table.Indexes)
        {
            _output.WriteLine($"Index: {index.Name}, Unique: {index.IsUnique}, Type: {index.IndexType}");
        }
    }
}

[Table("TestUsers")]
public class TestUser
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_TestUsers_Username")]
    [Unique("UQ_TestUsers_Username")]
    public string Username { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    public string Email { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }
}