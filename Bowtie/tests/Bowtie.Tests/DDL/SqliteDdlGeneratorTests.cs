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
public class SqliteDdlGeneratorTests
{
    private SqliteDdlGenerator _generator = null!;
    private ModelAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new SqliteDdlGenerator();
        _analyzer = new ModelAnalyzer();
    }

    [Test]
    public void Provider_ShouldBeSQLite()
    {
        _generator.Provider.Should().Be(DatabaseProvider.SQLite);
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
        ddl.Should().Contain("[Id] INTEGER NOT NULL AUTOINCREMENT");
        ddl.Should().Contain("[Username] TEXT");
        ddl.Should().Contain("[Email] TEXT");
        ddl.Should().Contain("[IsActive] INTEGER NOT NULL DEFAULT 1");
        ddl.Should().Contain("CONSTRAINT [PK_Users] PRIMARY KEY ([Id])");
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
        ddl.Should().Contain("[Price] REAL");
        ddl.Should().Contain("CHECK (Price > 0)");
        ddl.Should().Contain("CHECK (StockQuantity >= 0)");
        ddl.Should().Contain("FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])");
    }

    [Test]
    public void GenerateCreateIndex_WithBasicIndex_ShouldGenerateValidSql()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test",
            IndexType = IndexType.BTree,
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "Name", Order = 1, IsDescending = false }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Products");

        // Assert
        ddl.Should().Be("CREATE INDEX [IX_Test] ON [Products] ([Name] ASC);");
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
        ddl.Should().Be("CREATE UNIQUE INDEX [UQ_Test_Email] ON [Users] ([Email] ASC);");
    }

    [Test]
    public void GenerateCreateIndex_WithWhereClause_ShouldGeneratePartialIndex()
    {
        // Arrange
        var index = new IndexModel
        {
            Name = "IX_Test_Partial",
            IndexType = IndexType.BTree,
            WhereClause = "IsActive = 1",
            Columns = new List<IndexColumnModel>
            {
                new() { ColumnName = "CreatedDate", Order = 1, IsDescending = true }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateIndex(index, "Users");

        // Assert
        ddl.Should().Contain("CREATE INDEX [IX_Test_Partial] ON [Users] ([CreatedDate] DESC) WHERE IsActive = 1;");
    }

    [Test]
    public void MapNetTypeToDbType_WithVariousTypes_ShouldMapToSqliteTypes()
    {
        var column = new ColumnModel();

        // SQLite uses storage classes: INTEGER, REAL, TEXT, BLOB
        _generator.MapNetTypeToDbType(typeof(bool), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(byte), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(short), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(int), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(long), column).Should().Be("INTEGER");
        _generator.MapNetTypeToDbType(typeof(float), column).Should().Be("REAL");
        _generator.MapNetTypeToDbType(typeof(double), column).Should().Be("REAL");
        _generator.MapNetTypeToDbType(typeof(decimal), column).Should().Be("REAL");
        _generator.MapNetTypeToDbType(typeof(DateTime), column).Should().Be("TEXT");
        _generator.MapNetTypeToDbType(typeof(DateTimeOffset), column).Should().Be("TEXT");
        _generator.MapNetTypeToDbType(typeof(TimeSpan), column).Should().Be("TEXT");
        _generator.MapNetTypeToDbType(typeof(Guid), column).Should().Be("TEXT");
        _generator.MapNetTypeToDbType(typeof(string), column).Should().Be("TEXT");
    }

    [Test]
    public void ValidateIndexType_WithSqliteTypes_ShouldOnlySupportBTree()
    {
        // Only B-Tree supported
        _generator.ValidateIndexType(IndexType.BTree).Should().BeTrue();

        // All others unsupported
        _generator.ValidateIndexType(IndexType.Hash).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.GIN).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.GiST).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.BRIN).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.SPGiST).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.Clustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.NonClustered).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.ColumnStore).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.FullText).Should().BeFalse();
        _generator.ValidateIndexType(IndexType.Spatial).Should().BeFalse();
    }

    [Test]
    public void GenerateDropColumn_ShouldThrowNotSupportedException()
    {
        // Act & Assert
        var action = () => _generator.GenerateDropColumn("TestColumn", "TestTable");
        action.Should().Throw<NotSupportedException>()
            .WithMessage("SQLite does not support DROP COLUMN. Table recreation is required.");
    }

    [Test]
    public void GenerateAlterColumn_ShouldThrowNotSupportedException()
    {
        // Arrange
        var currentColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(string) };
        var targetColumn = new ColumnModel { Name = "TestColumn", PropertyType = typeof(int) };

        // Act & Assert
        var action = () => _generator.GenerateAlterColumn(currentColumn, targetColumn, "TestTable");
        action.Should().Throw<NotSupportedException>()
            .WithMessage("SQLite does not support ALTER COLUMN. Table recreation is required.");
    }

    [Test]
    public void GenerateDropConstraint_ShouldThrowNotSupportedException()
    {
        // Act & Assert
        var action = () => _generator.GenerateDropConstraint("TestConstraint", "TestTable");
        action.Should().Throw<NotSupportedException>()
            .WithMessage("SQLite does not support dropping constraints. Table recreation is required.");
    }

    [Test]
    public void GenerateAlterTable_WithColumnDropOrModification_ShouldGenerateTableRecreation()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "OldColumn", PropertyType = typeof(string), MaxLength = 50 },
                new() { Name = "ModifyColumn", PropertyType = typeof(string), MaxLength = 50 }
            }
        };

        var targetTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "ModifyColumn", PropertyType = typeof(string), MaxLength = 100 }, // Modified
                new() { Name = "NewColumn", PropertyType = typeof(int) } // Added
                // OldColumn removed
            }
        };

        // Act
        var ddl = _generator.GenerateAlterTable(currentTable, targetTable);

        // Assert
        ddl.Should().Contain("CREATE TABLE [TestTable_temp_");
        ddl.Should().Contain("INSERT INTO [TestTable_temp_");
        ddl.Should().Contain("DROP TABLE [TestTable]");
        ddl.Should().Contain("ALTER TABLE [TestTable_temp_");
        ddl.Should().Contain("RENAME TO [TestTable]");
    }

    [Test]
    public void GenerateAlterTable_WithOnlyNewColumns_ShouldGenerateAddColumnStatements()
    {
        // Arrange
        var currentTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "ExistingColumn", PropertyType = typeof(string) }
            }
        };

        var targetTable = new TableModel
        {
            Name = "TestTable",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true },
                new() { Name = "ExistingColumn", PropertyType = typeof(string) },
                new() { Name = "NewColumn1", PropertyType = typeof(string) },
                new() { Name = "NewColumn2", PropertyType = typeof(int) }
            }
        };

        // Act
        var ddl = _generator.GenerateAlterTable(currentTable, targetTable);

        // Assert
        ddl.Should().Contain("ALTER TABLE [TestTable] ADD COLUMN [NewColumn1] TEXT;");
        ddl.Should().Contain("ALTER TABLE [TestTable] ADD COLUMN [NewColumn2] INTEGER;");
        ddl.Should().NotContain("CREATE TABLE [TestTable_temp_"); // No recreation needed
    }

    [Test]
    public void GenerateCreateTable_WithNullableAndNonNullableColumns_ShouldHandleNullability()
    {
        // Arrange
        var table = new TableModel
        {
            Name = "TestNullability",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", PropertyType = typeof(int), IsPrimaryKey = true, IsNullable = false },
                new() { Name = "RequiredField", PropertyType = typeof(string), IsNullable = false },
                new() { Name = "OptionalField", PropertyType = typeof(string), IsNullable = true }
            }
        };

        // Act
        var ddl = _generator.GenerateCreateTable(table);

        // Assert
        ddl.Should().Contain("[Id] INTEGER NOT NULL");
        ddl.Should().Contain("[RequiredField] TEXT NOT NULL");
        ddl.Should().Contain("[OptionalField] TEXT"); // No NOT NULL for nullable
    }
}