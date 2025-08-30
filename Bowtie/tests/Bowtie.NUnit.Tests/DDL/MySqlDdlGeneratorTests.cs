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
public class MySqlDdlGeneratorTests
{
    private MySqlDdlGenerator _generator = null!;
    private ModelAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new MySqlDdlGenerator();
        _analyzer = new ModelAnalyzer();
    }

    [Test]
    public void Provider_ShouldBeMySQL()
    {
        _generator.Provider.Should().Be(DatabaseProvider.MySQL);
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
        ddl.Should().Contain("CREATE TABLE `Users`");
        ddl.Should().Contain("`Id` INT NOT NULL AUTO_INCREMENT");
        ddl.Should().Contain("`Username` VARCHAR(100)");
        ddl.Should().Contain("`Email` VARCHAR(200)");
        ddl.Should().Contain("`IsActive` TINYINT(1) NOT NULL DEFAULT 1");
        ddl.Should().Contain("CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)");
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
        ddl.Should().Contain("CREATE TABLE `Products`");
        ddl.Should().Contain("`Price` DECIMAL(18,2)");
        ddl.Should().Contain("CHECK (Price > 0)");
        ddl.Should().Contain("CHECK (StockQuantity >= 0)");
        ddl.Should().Contain("FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`)");
        ddl.Should().Contain("ON DELETE CASCADE");
    }

    [Test]
    public void GenerateCreateIndex_WithBTreeType_ShouldSpecifyBTreeMethod()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_BTree",
            IndexType = IndexType.BTree,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Name", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Products");

        // Assert
        ddl.Should().Contain("CREATE INDEX `IX_Test_BTree` ON `Products` (`Name` ASC) USING BTREE;");
    }

    [Test]
    public void GenerateCreateIndex_WithHashType_ShouldSpecifyHashMethod()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Hash",
            IndexType = IndexType.Hash,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "UserId", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Sessions");

        // Assert
        ddl.Should().Contain("CREATE INDEX `IX_Test_Hash` ON `Sessions` (`UserId` ASC) USING HASH;");
    }

    [Test]
    public void GenerateCreateIndex_WithUniqueConstraint_ShouldGenerateUniqueIndex()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "UQ_Test_Email",
            IsUnique = true,
            IndexType = IndexType.BTree,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Email", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Users");

        // Assert
        ddl.Should().Contain("CREATE UNIQUE INDEX `UQ_Test_Email` ON `Users` (`Email` ASC) USING BTREE;");
    }

    [Test]
    public void GenerateCreateTable_WithFullTextIndex_ShouldGenerateFullTextIndex()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(SearchIndex) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE TABLE `SearchIndex`");
        ddl.Should().Contain("CREATE INDEX `IX_SearchIndex_Title_FullText`");
        ddl.Should().Contain("CREATE INDEX `IX_SearchIndex_Content_FullText`");
    }

    [Test]
    public void MapNetTypeToDbType_WithVariousTypes_ShouldMapCorrectly()
    {
        var column = new ColumnModel();

        // Test basic types
        _generator.MapNetTypeToDbType(typeof(bool), column).Should().Be("TINYINT(1)");
        _generator.MapNetTypeToDbType(typeof(byte), column).Should().Be("TINYINT UNSIGNED");
        _generator.MapNetTypeToDbType(typeof(sbyte), column).Should().Be("TINYINT");
        _generator.MapNetTypeToDbType(typeof(short), column).Should().Be("SMALLINT");
        _generator.MapNetTypeToDbType(typeof(int), column).Should().Be("INT");
        _generator.MapNetTypeToDbType(typeof(long), column).Should().Be("BIGINT");
        _generator.MapNetTypeToDbType(typeof(float), column).Should().Be("FLOAT");
        _generator.MapNetTypeToDbType(typeof(double), column).Should().Be("DOUBLE");
        _generator.MapNetTypeToDbType(typeof(DateTime), column).Should().Be("DATETIME");
        _generator.MapNetTypeToDbType(typeof(DateTimeOffset), column).Should().Be("TIMESTAMP");
        _generator.MapNetTypeToDbType(typeof(TimeSpan), column).Should().Be("TIME");
        _generator.MapNetTypeToDbType(typeof(Guid), column).Should().Be("CHAR(36)");

        // Test decimal with precision
        column.Precision = 10;
        column.Scale = 2;
        _generator.MapNetTypeToDbType(typeof(decimal), column).Should().Be("DECIMAL(10,2)");

        // Test string with length
        column.MaxLength = 255;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("VARCHAR(255)");

        // Test string without length (max)
        column.MaxLength = -1;
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("LONGTEXT");
    }

    [Test]
    public void ValidateIndexType_WithMySqlTypes_ShouldReturnCorrectSupport()
    {
        // Supported types
        _generator.ValidateIndexType(IndexType.BTree).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Hash).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.FullText).Should().BeTrue();
        _generator.ValidateIndexType(IndexType.Spatial).Should().BeTrue();

        // Unsupported types
        _generator.ValidateIndexType(IndexType.GIN).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.GiST).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.BRIN).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.SPGiST).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.Clustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.NonClustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.ColumnStore).Should().BeFalse();
    }

    [Test]
    public void GenerateDropIndex_ShouldRequireTableName()
    {
        // Arrange
        var index = new IndexModel { Name = "IX_Test" };

        // Act
        var ddl = _generator.GenerateDropIndex(index, "TestTable");

        // Assert
        ddl.Should().Be("DROP INDEX `IX_Test` ON `TestTable`;");
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
            IsNullable = true,
            DefaultValue = "Default"
        };

        // Act
        var ddl = _generator.GenerateAddColumn(column, "TestTable");

        // Assert
        ddl.Should().Be("ALTER TABLE `TestTable` ADD COLUMN `NewColumn` VARCHAR(100) DEFAULT 'Default';");
    }

    [Test]
    public void GenerateAlterColumn_ShouldUseModifyColumnSyntax()
    {
        // Arrange
        var currentColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(string), MaxLength = 50 };
        var targetColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(string), MaxLength = 100, IsNullable = false };

        // Act
        var ddl = _generator.GenerateAlterColumn(currentColumn, targetColumn, "TestTable");

        // Assert
        ddl.Should().Be("ALTER TABLE `TestTable` MODIFY COLUMN `TestColumn` VARCHAR(100) NOT NULL;");
    }

    [Test]
    public void GenerateCreateTable_WithUnsignedTypes_ShouldUseUnsignedKeyword()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestUnsigned",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(uint), IsPrimaryKey = true },
                new() { Name = "ByteValue", PropertyType = typeof(byte) },
                new() { Name = "UShortValue", PropertyType = typeof(ushort) },
                new() { Name = "UIntValue", PropertyType = typeof(uint) },
                new() { Name = "ULongValue", PropertyType = typeof(ulong) }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("`Id` INT UNSIGNED NOT NULL");
        ddl.Should().Contain("`ByteValue` TINYINT UNSIGNED");
        ddl.Should().Contain("`UShortValue` SMALLINT UNSIGNED");
        ddl.Should().Contain("`UIntValue` INT UNSIGNED");
        ddl.Should().Contain("`ULongValue` BIGINT UNSIGNED");
    }

    [Test]
    public void GenerateCreateTable_WithEnumType_ShouldMapToInt()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestEnum",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "Status", PropertyType = typeof(TestEnum) }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("`Status` INT");
    }

    [Test]
    public void GenerateCreateTable_WithMultiColumnIndex_ShouldOrderColumnsByOrder()
    {
        // Arrange
        var tables = _analyzer.AnalyzeTypes(new[] { typeof(Product) });
        var table = tables[0];

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("CREATE INDEX `IX_Products_Category_Price`");
        ddl.Should().Contain("(`Category` ASC, `ListPrice` ASC)");
    }
}

public enum TestEnum
{
    Active = 1,
    Inactive = 2,
    Pending = 3
}