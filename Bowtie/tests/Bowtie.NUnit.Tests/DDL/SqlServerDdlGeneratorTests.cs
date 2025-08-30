using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Models;
using Bowtie.NUnit.Tests.TestModels;
using FluentAssertions;
using NUnit.Framework;

namespace Bowtie.NUnit.Tests.DDL;

[TestFixture]
public class SqlServerDdlGeneratorTests
{
    private SqlServerDdlGenerator _generator = null!;
    private ModelAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new SqlServerDdlGenerator();
        _analyzer = new ModelAnalyzer();
    }

    [Test]
    public void Provider_ShouldBeSqlServer()
    {
        _generator.Provider.Should().Be(DatabaseProvider.SqlServer);
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
        ddl.Should().Contain("CREATE TABLE [Users]");
        ddl.Should().Contain("[Id] INT NOT NULL IDENTITY(1,1)");
        ddl.Should().Contain("[Username] NVARCHAR(100)");
        ddl.Should().Contain("[Email] NVARCHAR(200)");
        ddl.Should().Contain("[IsActive] BIT NOT NULL DEFAULT 1");
        ddl.Should().Contain("CONSTRAINT [PK_Users] PRIMARY KEY ([Id])");
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Users_Username]");
        ddl.Should().Contain("CREATE UNIQUE NONCLUSTERED INDEX [UQ_Users_Username]");
    }

    [Test]
    public void GenerateCreateTable_WithConstraints_ShouldGenerateCheckConstraints()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Product) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE [Products]");
        ddl.Should().Contain("[Price] DECIMAL(18,2)");
        ddl.Should().Contain("CHECK (Price > 0)");
        ddl.Should().Contain("CHECK (StockQuantity >= 0)");
        ddl.Should().Contain("FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])");
        ddl.Should().Contain("ON DELETE CASCADE");
    }

    [Test]
    public void GenerateCreateTable_WithCompositeIndex_ShouldGenerateMultiColumnIndex()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Product) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Products_Category_Price]");
        ddl.Should().Contain("([Category] ASC, [ListPrice] ASC)");
    }

    [Test]
    public void GenerateCreateTable_WithCompositePrimaryKey_ShouldGenerateCompositePK()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(OrderItem) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE [OrderItems]");
        ddl.Should().Contain("CONSTRAINT [PK_OrderItems] PRIMARY KEY ([OrderId], [ProductId])");
        ddl.Should().Contain("FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])");
        ddl.Should().Contain("FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])");
        ddl.Should().Contain("CHECK (Quantity > 0)");
        ddl.Should().Contain("CHECK (UnitPrice >= 0)");
        ddl.Should().Contain("CHECK (DiscountRate >= 0 AND DiscountRate <= 1)");
    }

    [Test]
    public void GenerateCreateTable_WithClusteredIndex_ShouldGenerateClusteredIndex()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Analytics) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE [Analytics]");
        ddl.Should().Contain("[Id] BIGINT NOT NULL IDENTITY(1,1)");
        ddl.Should().Contain("CREATE CLUSTERED INDEX [IX_Analytics_EventDate_Clustered]");
        ddl.Should().Contain("ON [Analytics] ([EventDate] ASC)");
    }

    [Test]
    public void GenerateCreateIndex_WithClusteredType_ShouldSpecifyClusteredKeyword()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Clustered",
            IndexType = IndexType.Clustered,
            IsClustered = true,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Date", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "TestTable");

        // Assert
        ddl.Should().Contain("CREATE CLUSTERED INDEX [IX_Test_Clustered]");
        ddl.Should().Contain("ON [TestTable] ([Date] ASC)");
    }

    [Test]
    public void GenerateCreateIndex_WithUniqueNonClustered_ShouldSpecifyBothKeywords()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Unique",
            IndexType = IndexType.NonClustered,
            IsUnique = true,
            IsClustered = false,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Email", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Users");

        // Assert
        ddl.Should().Contain("CREATE UNIQUE NONCLUSTERED INDEX [IX_Test_Unique]");
        ddl.Should().Contain("ON [Users] ([Email] ASC)");
    }

    [Test]
    public void GenerateCreateIndex_WithIncludeColumns_ShouldGenerateIncludeClause()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Include",
            IndexType = IndexType.NonClustered,
            IncludeColumns = "Name, Description",
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "CategoryId", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Products");

        // Assert
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Test_Include]");
        ddl.Should().Contain("([CategoryId] ASC)");
        ddl.Should().Contain("INCLUDE (Name, Description)");
    }

    [Test]
    public void GenerateCreateIndex_WithWhereClause_ShouldGenerateWhereClause()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Where",
            IndexType = IndexType.NonClustered,
            WhereClause = "IsActive = 1",
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "CreatedDate", Order = 1, IsDescending = true }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Users");

        // Assert
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Test_Where]");
        ddl.Should().Contain("([CreatedDate] DESC)");
        ddl.Should().Contain("WHERE IsActive = 1");
    }

    [Test]
    public void MapNetTypeToDbType_WithVariousTypes_ShouldMapCorrectly()
    {
        var column = new ColumnModel();

        // Test basic types
        _generator.MapNetTypeToDbType(typeof(int), column).Should().Be("INT");
        _generator.MapNetTypeToDbType(typeof(long), column).Should().Be("BIGINT");
        _generator.MapNetTypeToDbType(typeof(bool), column).Should().Be("BIT");
        _generator.MapNetTypeToDbType(typeof(DateTime), column).Should().Be("DATETIME2");
        _generator.MapNetTypeToDbType(typeof(Guid), column).Should().Be("UNIQUEIDENTIFIER");

        // Test nullable types
        _generator.MapNetTypeToDbType(typeof(int?), column).Should().Be("INT");
        _generator.MapNetTypeToDbType(typeof(DateTime?), column).Should().Be("DATETIME2");

        // Test string with length
        column.MaxLength = 100;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("NVARCHAR(100)");

        // Test string without length
        column.MaxLength = null;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("NVARCHAR(255)");

        // Test decimal with precision
        column.Precision = 18;
        column.Scale = 2;
        _generator.MapNetTypeToDbType(typeof(decimal), column).Should().Be("DECIMAL(18,2)");
    }

    [Test]
    public void ValidateIndexType_WithSqlServerTypes_ShouldReturnCorrectSupport()
    {
        // Supported types
        _generator.ValidateIndexType(IndexType.BTree).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Clustered).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.NonClustered).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.ColumnStore).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Spatial).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.FullText).Should().BeTrue();

        // Unsupported types
        _generator.ValidateIndexType(IndexType.Hash).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.GIN).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.GiST).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.BRIN).Should().BeFalse();
    }

    [Test]
    public void GenerateDropTable_ShouldGenerateCorrectSql()
    {
        // Arrange
        var table = new TableModel { Name = "TestTable", Schema = "dbo" };

        // Act
        var ddl = _generator.GenerateDropTable(table);

        // Assert
        ddl.Should().Be("DROP TABLE [dbo.TestTable];");
    }

    [Test]
    public void GenerateDropIndex_ShouldGenerateCorrectSql()
    {
        // Arrange
        var index = new IndexModel { Name = "IX_Test" };

        // Act
        var ddl = _generator.GenerateDropIndex(index, "TestTable");

        // Assert
        ddl.Should().Be("DROP INDEX [IX_Test] ON [TestTable];");
    }

    [Test]
    public void GenerateAddColumn_ShouldGenerateCorrectSql()
    {
        // Arrange
        var column = new ColumnModel
        {
            Name = "NewColumn",
            PropertyType = typeof(string),
            MaxLength = 50,
            IsNullable = false,
            DefaultValue = "Default"
        };

        // Act
        var ddl = _generator.GenerateAddColumn(column, "TestTable");

        // Assert
        ddl.Should().Be("ALTER TABLE [TestTable] ADD [NewColumn] NVARCHAR(50) NOT NULL DEFAULT 'Default';");
    }

    [Test]
    public void GenerateAlterColumn_ShouldGenerateCorrectSql()
    {
        // Arrange
        var currentColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(string), MaxLength = 50 };
        var targetColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(string), MaxLength = 100 };

        // Act
        var ddl = _generator.GenerateAlterColumn(currentColumn, targetColumn, "TestTable");

        // Assert
        ddl.Should().Be("ALTER TABLE [TestTable] ALTER COLUMN [TestColumn] NVARCHAR(100);");
    }

    [Test]
    public void GenerateCreateTable_WithDefaultValues_ShouldHandleRawSqlAndValues()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() 
                { 
                    Name = "Id", 
                    PropertyType = typeof(int), 
                    IsPrimaryKey = true, 
                    IsIdentity = true 
                },
                new() 
                { 
                    Name = "CreatedDate", 
                    PropertyType = typeof(DateTime), 
                    DefaultValue = "GETUTCDATE()", 
                    IsDefaultRawSql = true 
                },
                new() 
                { 
                    Name = "Status", 
                    PropertyType = typeof(string), 
                    DefaultValue = "Active", 
                    IsDefaultRawSql = false,
                    MaxLength = 20
                }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("[CreatedDate] DATETIME2 DEFAULT GETUTCDATE()");
        ddl.Should().Contain("[Status] NVARCHAR(20) DEFAULT 'Active'");
    }

    [Test]
    public void GenerateMigrationScript_WithMultipleTables_ShouldGenerateCompleteScript()
    {
        // Arrange
        var currentTables = new List<TableModel>();
        var targetTables = _analyzer.AnalyzeTypes(new[] { typeof(User), typeof(Category), typeof(Product) });

        // Act
        var scripts = _generator.GenerateMigrationScript(currentTables, targetTables);

        // Assert
        scripts.Should().HaveCount(3);
        scripts[0].Should().Contain("CREATE TABLE [Users]");
        scripts[1].Should().Contain("CREATE TABLE [Categories]");
        scripts[2].Should().Contain("CREATE TABLE [Products]");
        
        // Verify foreign key relationships
        scripts[2].Should().Contain("REFERENCES [Categories]");
    }

    [Test]
    public void GenerateCreateTable_WithSpatialIndex_ShouldGenerateValidSql()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Location) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE [Locations]");
        ddl.Should().Contain("[Coordinates] geography");
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Locations_Coordinates_Spatial]");
    }

    [Test]
    public void GenerateCreateTable_WithFullTextIndex_ShouldGenerateValidSql()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(SearchIndex) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE [SearchIndex]");
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_SearchIndex_Title_FullText]");
        ddl.Should().Contain("CREATE NONCLUSTERED INDEX [IX_SearchIndex_Content_FullText]");
    }

    [TestCase(typeof(byte), "TINYINT")]
    [TestCase(typeof(short), "SMALLINT")]
    [TestCase(typeof(int), "INT")]
    [TestCase(typeof(long), "BIGINT")]
    [TestCase(typeof(float), "REAL")]
    [TestCase(typeof(double), "FLOAT")]
    [TestCase(typeof(bool), "BIT")]
    [TestCase(typeof(DateTime), "DATETIME2")]
    [TestCase(typeof(DateTimeOffset), "DATETIMEOFFSET")]
    [TestCase(typeof(TimeSpan), "TIME")]
    [TestCase(typeof(Guid), "UNIQUEIDENTIFIER")]
    public void MapNetTypeToDbType_WithPrimitiveTypes_ShouldMapCorrectly(Type netType, string expectedSqlType)
    {
        // Arrange
        var column = new ColumnModel();

        // Act
        var result = _generator.MapNetTypeToDbType(netType, column);

        // Assert
        result.Should().Be(expectedSqlType);
    }

    [Test]
    public void MapNetTypeToDbType_WithNullableTypes_ShouldMapToBaseType()
    {
        // Arrange
        var column = new ColumnModel();

        // Act & Assert
        _generator.MapNetTypeToDbType(typeof(int?), column).Should().Be("INT");
        _generator.MapNetTypeToDbType(typeof(DateTime?), column).Should().Be("DATETIME2");
        _generator.MapNetTypeToDbType(typeof(bool?), column).Should().Be("BIT");
    }

    [Test]
    public void MapNetTypeToDbType_WithStringAndMaxLength_ShouldMapWithLength()
    {
        // Arrange
        var column = new ColumnModel { MaxLength = 500 };

        // Act
        var result = _generator.MapNetTypeToDbType(typeof(string), column);

        // Assert
        result.Should().Be("NVARCHAR(500)");
    }

    [Test]
    public void MapNetTypeToDbType_WithStringAndMaxLengthNegativeOne_ShouldMapToMax()
    {
        // Arrange
        var column = new ColumnModel { MaxLength = -1 };

        // Act
        var result = _generator.MapNetTypeToDbType(typeof(string), column);

        // Assert
        result.Should().Be("NVARCHAR(MAX)");
    }

    [Test]
    public void MapNetTypeToDbType_WithCustomTypeName_ShouldUseCustomType()
    {
        // Arrange
        var column = new ColumnModel { DataType = "nvarchar(max)" };

        // Act
        var result = _generator.MapNetTypeToDbType(typeof(string), column);

        // Assert
        result.Should().Be("nvarchar(max)");
    }

    [Test]
    public void GenerateAlterTable_AddingColumns_ShouldGenerateAddColumnStatements()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true }
            }
        };

        var targetTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "NewColumn", PropertyType = typeof(string), MaxLength = 100 }
            }
        };

        // Act
        var ddl = _generator.GenerateAlterTable(currentTable, targetTable);

        // Assert
        ddl.Should().Contain("ALTER TABLE [TestTable] ADD [NewColumn] NVARCHAR(100);");
    }
}