using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Models;
using Bowtie.Tests.TestModels;
using FluentAssertions;
using NUnit.Framework;

namespace Bowtie.Tests.DDL;

[TestFixture]
public class PostgreSqlDdlGeneratorTests
{
    private PostgreSqlDdlGenerator _generator = null!;
    private ModelAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new PostgreSqlDdlGenerator();
        _analyzer = new ModelAnalyzer();
    }

    [Test]
    public void Provider_ShouldBePostgreSQL()
    {
        _generator.Provider.Should().Be(DatabaseProvider.PostgreSQL);
    }

    [Test]
    public void GenerateCreateTable_WithBasicModel_ShouldGenerateValidSql()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(User) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE \"Users\"");
        ddl.Should().Contain("\"Id\" INTEGER NOT NULL GENERATED ALWAYS AS IDENTITY");
        ddl.Should().Contain("\"Username\" VARCHAR(100)");
        ddl.Should().Contain("\"Email\" VARCHAR(200)");
        ddl.Should().Contain("\"IsActive\" BOOLEAN NOT NULL DEFAULT true");
        ddl.Should().Contain("CONSTRAINT \"PK_Users\" PRIMARY KEY (\"Id\")");
    }

    [Test]
    public void GenerateCreateTable_WithJsonbAndGinIndex_ShouldGenerateGinIndex()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Document) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE \"Documents\"");
        ddl.Should().Contain("\"Content\" jsonb");
        ddl.Should().Contain("\"Tags\" text[]");
        ddl.Should().Contain("CREATE INDEX \"IX_Documents_Content_GIN\" ON \"Documents\" USING GIN");
        ddl.Should().Contain("CREATE INDEX \"IX_Documents_Tags_GIN\" ON \"Documents\" USING GIN");
    }

    [Test]
    public void GenerateCreateIndex_WithGinType_ShouldSpecifyGinMethod()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_GIN",
            IndexType = IndexType.GIN,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Content", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Documents");

        // Assert
        ddl.Should().Contain("CREATE INDEX \"IX_Test_GIN\" ON \"Documents\" USING GIN (\"Content\" ASC);");
    }

    [Test]
    public void GenerateCreateIndex_WithGistType_ShouldSpecifyGistMethod()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_GIST",
            IndexType = IndexType.GiST,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Geometry", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Locations");

        // Assert
        ddl.Should().Contain("CREATE INDEX \"IX_Test_GIST\" ON \"Locations\" USING GIST (\"Geometry\" ASC);");
    }

    [Test]
    public void GenerateCreateIndex_WithHashType_ShouldSpecifyHashMethod()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_HASH",
            IndexType = IndexType.Hash,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "UserId", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Sessions");

        // Assert
        ddl.Should().Contain("CREATE INDEX \"IX_Test_HASH\" ON \"Sessions\" USING HASH (\"UserId\" ASC);");
    }

    [Test]
    public void GenerateCreateIndex_WithWhereClause_ShouldGeneratePartialIndex()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Partial",
            IndexType = IndexType.BTree,
            WhereClause = "is_active = true",
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "CreatedDate", Order = 1, IsDescending = true }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Users");

        // Assert
        ddl.Should().Contain("CREATE INDEX \"IX_Test_Partial\" ON \"Users\" (\"CreatedDate\" DESC)");
        ddl.Should().Contain("WHERE is_active = true");
    }

    [Test]
    public void MapNetTypeToDbType_WithVariousTypes_ShouldMapCorrectly()
    {
        var column = new ColumnModel();

        // Test basic types
        _generator.MapNetTypeToDbType(typeof(int), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(long), column).Should().Be("BIGINT");
        _generator.MapNetTypeToDbType(typeof(bool), column).Should().Be("BOOLEAN");
        _generator.MapNetTypeToDbType(typeof(DateTime), column).Should().Be("TIMESTAMP");
        _generator.MapNetTypeToDbType(typeof(DateTimeOffset), column).Should().Be("TIMESTAMPTZ");
        _generator.MapNetTypeToDbType(typeof(Guid), column).Should().Be("UUID");
        _generator.MapNetTypeToDbType(typeof(TimeSpan), column).Should().Be("INTERVAL");

        // Test decimal with precision
        column.Precision = 10;
        column.Scale = 2;
        _generator.MapNetTypeToDbType(typeof(decimal), column).Should().Be("NUMERIC(10,2)");

        // Test string with length
        column.MaxLength = 255;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("VARCHAR(255)");

        // Test string without length (max)
        column.MaxLength = -1;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("TEXT");
    }

    [Test]
    public void ValidateIndexType_WithPostgreSqlTypes_ShouldReturnCorrectSupport()
    {
        // Supported types
        _generator.ValidateIndexType(IndexType.BTree).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Hash).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.GIN).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.GiST).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.BRIN).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.SPGiST).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Spatial).Should().BeTrue();

        // Unsupported types  
        _generator.ValidateIndexType(IndexType.Clustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.NonClustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.ColumnStore).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.FullText).Should().BeFalse();
    }

    [Test]
    public void GenerateAlterColumn_WithTypeChange_ShouldGenerateTypeChangeStatement()
    {
        // Arrange
        var currentColumn = new ColumnModel 
        { 
            Name = "TestColumn", 
            PropertyType = typeof(string), 
            MaxLength = 50,
            IsNullable = true,
            DefaultValue = null
        };
        
        var targetColumn = new ColumnModel 
        { 
            Name = "TestColumn", 
            PropertyType = typeof(string), 
            MaxLength = 100,
            IsNullable = false,
            DefaultValue = "NewDefault"
        };

        // Act
        var ddl = _generator.GenerateAlterColumn(currentColumn, targetColumn, "TestTable");

        // Assert
        ddl.Should().Contain("ALTER TABLE \"TestTable\" ALTER COLUMN \"TestColumn\" TYPE VARCHAR(100)");
        ddl.Should().Contain("ALTER TABLE \"TestTable\" ALTER COLUMN \"TestColumn\" SET NOT NULL");
        ddl.Should().Contain("ALTER TABLE \"TestTable\" ALTER COLUMN \"TestColumn\" SET DEFAULT 'NewDefault'");
    }

    [Test]
    public void GenerateDropIndex_ShouldNotRequireTableName()
    {
        // Arrange
        var index = new IndexModel { Name = "IX_Test" };

        // Act
        var ddl = _generator.GenerateDropIndex(index, "TestTable");

        // Assert
        ddl.Should().Be("DROP INDEX \"IX_Test\";");
    }

    [Test]
    public void GenerateAddColumn_ShouldUseAddColumnSyntax()
    {
        // Arrange
        var column = new ColumnModel
        {
            Name = "NewColumn",
            PropertyType = typeof(string),
            MaxLength = 100,
            IsNullable = false
        };

        // Act
        var ddl = _generator.GenerateAddColumn(column, "TestTable");

        // Assert
        ddl.Should().Be("ALTER TABLE \"TestTable\" ADD COLUMN \"NewColumn\" VARCHAR(100) NOT NULL;");
    }

    [Test]
    public void GenerateCreateTable_WithArrayTypes_ShouldHandlePostgreSqlArrays()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestArrays",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true, IsIdentity = true },
                new() { Name = "Tags", PropertyType = typeof(string[]), DataType = "text[]" }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("\"Tags\" text[]");
    }

    [Test]
    public void GenerateCreateTable_WithTimestampTypes_ShouldMapCorrectly()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestTime",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "Created", PropertyType = typeof(DateTime) },
                new() { Name = "CreatedUtc", PropertyType = typeof(DateTimeOffset) },
                new() { Name = "Duration", PropertyType = typeof(TimeSpan) }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("\"Created\" TIMESTAMP");
        ddl.Should().Contain("\"CreatedUtc\" TIMESTAMPTZ");
        ddl.Should().Contain("\"Duration\" INTERVAL");
    }
}