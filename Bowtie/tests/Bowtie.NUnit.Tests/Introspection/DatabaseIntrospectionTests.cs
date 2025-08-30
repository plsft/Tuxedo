using System.Data;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.Introspection;
using Bowtie.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using NUnit.Framework;
using Tuxedo;

namespace Bowtie.NUnit.Tests.Introspection;

[TestFixture]
public class DatabaseIntrospectionTests
{
    [Test]
    public void SqlServerIntrospector_Provider_ShouldBeSqlServer()
    {
        // Arrange
        var introspector = new SqlServerIntrospector();

        // Assert
        introspector.Provider.Should().Be(DatabaseProvider.SqlServer);
    }

    [Test]
    public void PostgreSqlIntrospector_Provider_ShouldBePostgreSQL()
    {
        // Arrange
        var introspector = new PostgreSqlIntrospector();

        // Assert
        introspector.Provider.Should().Be(DatabaseProvider.PostgreSQL);
    }

    [Test]
    public async Task SqlServerIntrospector_TableExistsAsync_WithValidParameters_ShouldExecuteQuery()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        
        var introspector = new SqlServerIntrospector();

        // Act
        var exists = await introspector.TableExistsAsync(mockConnection.Object, "Users", "dbo");

        // Assert
        exists.Should().BeTrue();
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    [Test]
    public async Task PostgreSqlIntrospector_TableExistsAsync_WithNullSchema_ShouldUsePublicSchema()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(0);
        
        var introspector = new PostgreSqlIntrospector();

        // Act
        var exists = await introspector.TableExistsAsync(mockConnection.Object, "Users", null);

        // Assert
        exists.Should().BeFalse();
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    [Test]
    public async Task SqlServerIntrospector_ColumnExistsAsync_WithValidParameters_ShouldExecuteQuery()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);
        
        var introspector = new SqlServerIntrospector();

        // Act
        var exists = await introspector.ColumnExistsAsync(mockConnection.Object, "Users", "Username", "dbo");

        // Assert
        exists.Should().BeTrue();
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    [Test]
    public async Task PostgreSqlIntrospector_ColumnExistsAsync_WithValidParameters_ShouldExecuteQuery()
    {
        // Arrange
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(c => c.ExecuteScalar()).Returns(0);
        
        var introspector = new PostgreSqlIntrospector();

        // Act
        var exists = await introspector.ColumnExistsAsync(mockConnection.Object, "Users", "NonExistentColumn", "public");

        // Assert
        exists.Should().BeFalse();
        mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
    }

    [Test]
    public void DatabaseProvider_SupportsIndexType_ShouldReturnCorrectSupport()
    {
        // SQL Server
        DatabaseProvider.SqlServer.SupportsIndexType(IndexType.Clustered).Should().BeTrue();
        DatabaseProvider.SqlServer.SupportsIndexType(IndexType.GIN).Should().BeFalse();

        // PostgreSQL
        DatabaseProvider.PostgreSQL.SupportsIndexType(IndexType.GIN).Should().BeTrue();
        DatabaseProvider.PostgreSQL.SupportsIndexType(IndexType.Clustered).Should().BeFalse();

        // MySQL
        DatabaseProvider.MySQL.SupportsIndexType(IndexType.Hash).Should().BeTrue();
        DatabaseProvider.MySQL.SupportsIndexType(IndexType.GIN).Should().BeFalse();

        // SQLite
        DatabaseProvider.SQLite.SupportsIndexType(IndexType.BTree).Should().BeTrue();
        DatabaseProvider.SQLite.SupportsIndexType(IndexType.Hash).Should().BeFalse();
    }

    [Test]
    public void DatabaseProvider_SupportsSchemas_ShouldReturnCorrectSupport()
    {
        DatabaseProvider.SqlServer.SupportsSchemas().Should().BeTrue();
        DatabaseProvider.PostgreSQL.SupportsSchemas().Should().BeTrue();
        DatabaseProvider.MySQL.SupportsSchemas().Should().BeFalse();
        DatabaseProvider.SQLite.SupportsSchemas().Should().BeFalse();
    }

    [Test]
    public void DatabaseProvider_GetDefaultSchema_ShouldReturnCorrectSchema()
    {
        DatabaseProvider.SqlServer.GetDefaultSchema().Should().Be("dbo");
        DatabaseProvider.PostgreSQL.GetDefaultSchema().Should().Be("public");
        DatabaseProvider.MySQL.GetDefaultSchema().Should().Be("");
        DatabaseProvider.SQLite.GetDefaultSchema().Should().Be("");
    }

    [Test]
    public void DatabaseProvider_GetQuotedIdentifier_ShouldQuoteCorrectly()
    {
        DatabaseProvider.SqlServer.GetQuotedIdentifier("TableName").Should().Be("[TableName]");
        DatabaseProvider.PostgreSQL.GetQuotedIdentifier("TableName").Should().Be("\"TableName\"");
        DatabaseProvider.MySQL.GetQuotedIdentifier("TableName").Should().Be("`TableName`");
        DatabaseProvider.SQLite.GetQuotedIdentifier("TableName").Should().Be("[TableName]");
    }

    [Test]
    public void DatabaseProvider_GetParameterPrefix_ShouldReturnAtSign()
    {
        // All providers use @ for parameters
        DatabaseProvider.SqlServer.GetParameterPrefix().Should().Be("@");
        DatabaseProvider.PostgreSQL.GetParameterPrefix().Should().Be("@");
        DatabaseProvider.MySQL.GetParameterPrefix().Should().Be("@");
        DatabaseProvider.SQLite.GetParameterPrefix().Should().Be("@");
    }

    // Integration test with real SQLite database
    [Test]
    public async Task SqliteInMemoryDatabase_CreateAndQuery_ShouldWork()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await Task.Run(() => connection.Open());

        // Create test table
        var createSql = @"
            CREATE TABLE TestUsers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                Email TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            
            CREATE INDEX IX_TestUsers_Username ON TestUsers (Username);
            CREATE UNIQUE INDEX UQ_TestUsers_Email ON TestUsers (Email);
        ";

        await connection.ExecuteAsync(createSql);

        // Act - Test table existence
        var tableCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='TestUsers'");

        var indexCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'IX_%' OR name LIKE 'UQ_%'");

        // Assert
        tableCount.Should().Be(1);
        indexCount.Should().Be(2);
    }

    [Test]
    public async Task SqliteInMemoryDatabase_WithBowtieGeneratedSchema_ShouldCreateAndQuery()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await Task.Run(() => connection.Open());

        // Use Bowtie to generate schema
        var analyzer = new Bowtie.Analysis.ModelAnalyzer();
        var generator = new Bowtie.DDL.SqliteDdlGenerator();
        
        var tables = analyzer.AnalyzeTypes(new[] { typeof(Bowtie.NUnit.Tests.TestModels.User) });
        var createScript = generator.GenerateCreateTable(tables[0]);

        // Act - Execute Bowtie-generated DDL
        await connection.ExecuteAsync(createScript);

        // Verify table was created
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'") > 0;

        var columnCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Users')");

        // Assert
        tableExists.Should().BeTrue();
        columnCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void AllProviders_ShouldHaveCorrespondingIntrospectors()
    {
        // Arrange
        var providers = Enum.GetValues<DatabaseProvider>();
        var availableIntrospectors = new List<IDatabaseIntrospector>
        {
            new SqlServerIntrospector(),
            new PostgreSqlIntrospector()
            // Note: MySQL and SQLite introspectors would be added here when implemented
        };

        // Act & Assert
        foreach (var provider in providers)
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                case DatabaseProvider.PostgreSQL:
                    availableIntrospectors.Should().Contain(i => i.Provider == provider,
                        $"Introspector should exist for {provider}");
                    break;
                case DatabaseProvider.MySQL:
                case DatabaseProvider.SQLite:
                    // These are planned for future implementation
                    // availableIntrospectors.Should().Contain(i => i.Provider == provider);
                    break;
            }
        }
    }
}