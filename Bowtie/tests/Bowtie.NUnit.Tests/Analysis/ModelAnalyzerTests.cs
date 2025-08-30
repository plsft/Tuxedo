using System.Reflection;
using Bowtie.Analysis;
using Bowtie.Attributes;
using Bowtie.Core;
using Bowtie.Models;
using Bowtie.NUnit.Tests.TestModels;
using FluentAssertions;
using NUnit.Framework;
using Tuxedo.Contrib;

namespace Bowtie.NUnit.Tests.Analysis;

[TestFixture]
public class ModelAnalyzerTests
{
    private ModelAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _analyzer = new ModelAnalyzer();
    }

    [Test]
    public void AnalyzeType_WithBasicModel_ShouldExtractTableInformation()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(User));

        // Assert
        table.Should().NotBeNull();
        table!.Name.Should().Be("Users");
        table.ModelType.Should().Be(typeof(User));
        table.Schema.Should().BeNull(); // No schema specified
    }

    [Test]
    public void AnalyzeType_WithBasicModel_ShouldExtractAllColumns()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(User));

        // Assert
        table!.Columns.Should().HaveCount(5); // Id, Username, Email, IsActive, CreatedDate
        
        var idColumn = table.Columns.Should().ContainSingle(c => c.Name == "Id").Subject;
        idColumn.IsPrimaryKey.Should().BeTrue();
        idColumn.IsIdentity.Should().BeTrue();
        idColumn.PropertyType.Should().Be(typeof(int));

        var usernameColumn = table.Columns.Should().ContainSingle(c => c.Name == "Username").Subject;
        usernameColumn.MaxLength.Should().Be(100);
        usernameColumn.PropertyType.Should().Be(typeof(string));

        var isActiveColumn = table.Columns.Should().ContainSingle(c => c.Name == "IsActive").Subject;
        isActiveColumn.DefaultValue.Should().Be(true);
        isActiveColumn.PropertyType.Should().Be(typeof(bool));

        var createdDateColumn = table.Columns.Should().ContainSingle(c => c.Name == "CreatedDate").Subject;
        createdDateColumn.DefaultValue.Should().Be("GETUTCDATE()");
        createdDateColumn.IsDefaultRawSql.Should().BeTrue();
    }

    [Test]
    public void AnalyzeType_WithComputedProperty_ShouldIgnoreComputedColumns()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(User));

        // Assert
        table!.Columns.Should().NotContain(c => c.Name == "DisplayName");
    }

    [Test]
    public void AnalyzeType_WithIndexes_ShouldExtractIndexInformation()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(User));

        // Assert
        table!.Indexes.Should().HaveCount(2);

        var usernameIndex = table.Indexes.Should().ContainSingle(i => i.Name == "IX_Users_Username").Subject;
        usernameIndex.IsUnique.Should().BeFalse();
        usernameIndex.Columns.Should().ContainSingle(c => c.ColumnName == "Username");

        var uniqueIndex = table.Indexes.Should().ContainSingle(i => i.Name == "UQ_Users_Username").Subject;
        uniqueIndex.IsUnique.Should().BeTrue();
        uniqueIndex.Columns.Should().ContainSingle(c => c.ColumnName == "Username");
    }

    [Test]
    public void AnalyzeType_WithCompositeIndex_ShouldGroupColumnsByIndexName()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(Product));

        // Assert
        var compositeIndex = table!.Indexes.Should().ContainSingle(i => i.Name == "IX_Products_Category_Price").Subject;
        compositeIndex.Columns.Should().HaveCount(2);
        
        var categoryColumn = compositeIndex.Columns.Should().ContainSingle(c => c.ColumnName == "Category").Subject;
        categoryColumn.Order.Should().Be(1);
        
        var priceColumn = compositeIndex.Columns.Should().ContainSingle(c => c.ColumnName == "ListPrice").Subject;
        priceColumn.Order.Should().Be(2);
    }

    [Test]
    public void AnalyzeType_WithForeignKey_ShouldExtractForeignKeyConstraints()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(Product));

        // Assert
        var fkConstraint = table!.Constraints.Should().ContainSingle(c => c.Type == ConstraintType.ForeignKey).Subject;
        fkConstraint.Name.Should().Be("FK_Products_CategoryId");
        fkConstraint.Columns.Should().ContainSingle().Which.Should().Be("CategoryId");
        fkConstraint.ReferencedTable.Should().Be("Categories");
        fkConstraint.ReferencedColumn.Should().Be("Id");
        fkConstraint.OnDelete.Should().Be(ReferentialAction.Cascade);
    }

    [Test]
    public void AnalyzeType_WithCheckConstraints_ShouldExtractCheckConstraints()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(Product));

        // Assert
        var checkConstraints = table!.Constraints.Where(c => c.Type == ConstraintType.Check).ToList();
        checkConstraints.Should().HaveCount(2);

        checkConstraints.Should().Contain(c => c.CheckExpression == "Price > 0");
        checkConstraints.Should().Contain(c => c.CheckExpression == "StockQuantity >= 0");
    }

    [Test]
    public void AnalyzeType_WithCompositePrimaryKey_ShouldExtractCompositePrimaryKey()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(OrderItem));

        // Assert
        var pkConstraint = table!.Constraints.Should().ContainSingle(c => c.Type == ConstraintType.PrimaryKey).Subject;
        pkConstraint.Name.Should().Be("PK_OrderItems");
        pkConstraint.Columns.Should().HaveCount(2);
        pkConstraint.Columns.Should().Contain("OrderId");
        pkConstraint.Columns.Should().Contain("ProductId");
    }

    [Test]
    public void AnalyzeTypes_WithMultipleTypes_ShouldAnalyzeAllTypes()
    {
        // Arrange
        var types = new[] { typeof(User), typeof(Product), typeof(Category) };

        // Act
        var tables = _analyzer.AnalyzeTypes(types);

        // Assert
        tables.Should().HaveCount(3);
        tables.Should().Contain(t => t.Name == "Users");
        tables.Should().Contain(t => t.Name == "Products");
        tables.Should().Contain(t => t.Name == "Categories");
    }

    [Test]
    public void AnalyzeAssembly_WithTestAssembly_ShouldFindAllTableModels()
    {
        // Arrange
        var assembly = typeof(User).Assembly;

        // Act
        var tables = _analyzer.AnalyzeAssembly(assembly);

        // Assert
        tables.Should().NotBeEmpty();
        tables.Should().Contain(t => t.Name == "Users");
    }

    [Test]
    public void AnalyzeType_WithTableSchemaInName_ShouldExtractSchemaAndTableName()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(SchemaTestModel), "default_schema");

        // Assert
        table!.Name.Should().Be("TestTable");
        table.Schema.Should().Be("custom");
    }

    [Test]
    public void AnalyzeType_WithExplicitKey_ShouldRecognizeExplicitKey()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(ExplicitKeyModel));

        // Assert
        var keyColumn = table!.Columns.Should().ContainSingle(c => c.IsPrimaryKey).Subject;
        keyColumn.Name.Should().Be("Code");
        keyColumn.IsIdentity.Should().BeFalse(); // ExplicitKey is not auto-increment
    }

    [Test]
    public void AnalyzeType_WithWriteAttributeFalse_ShouldIgnoreColumn()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(WriteAttributeTestModel));

        // Assert
        table!.Columns.Should().NotContain(c => c.Name == "IgnoredColumn");
        table.Columns.Should().Contain(c => c.Name == "IncludedColumn");
    }

    [Test]
    public void AnalyzeType_WithCustomColumnName_ShouldUseCustomName()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(CustomColumnNameModel));

        // Assert
        var column = table!.Columns.Should().ContainSingle(c => c.Name == "custom_name").Subject;
        column.PropertyInfo.Name.Should().Be("PropertyName");
    }

    [Test]
    public void AnalyzeType_WithGinIndexOnJsonb_ShouldCreateGinIndex()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(Document));

        // Assert
        var ginIndex = table!.Indexes.Should().ContainSingle(i => i.IndexType == IndexType.GIN && i.Name.Contains("Content")).Subject;
        ginIndex.Name.Should().Be("IX_Documents_Content_GIN");
        ginIndex.Columns.Should().ContainSingle(c => c.ColumnName == "Content");
    }

    [Test]
    public void AnalyzeType_WithMultipleCheckConstraints_ShouldExtractAllConstraints()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(OrderItem));

        // Assert
        var checkConstraints = table!.Constraints.Where(c => c.Type == ConstraintType.Check).ToList();
        checkConstraints.Should().HaveCount(3);
        
        checkConstraints.Should().Contain(c => c.CheckExpression == "Quantity > 0");
        checkConstraints.Should().Contain(c => c.CheckExpression == "UnitPrice >= 0");
        checkConstraints.Should().Contain(c => c.CheckExpression == "DiscountRate >= 0 AND DiscountRate <= 1");
    }

    [Test]
    public void AnalyzeType_WithPrecisionAndScale_ShouldExtractNumericPrecision()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(Product));

        // Assert
        var priceColumn = table!.Columns.Should().ContainSingle(c => c.Name == "Price").Subject;
        priceColumn.Precision.Should().Be(18);
        priceColumn.Scale.Should().Be(2);
        priceColumn.PropertyType.Should().Be(typeof(decimal));
    }

    [Test]
    public void AnalyzeType_WithMultipleForeignKeys_ShouldExtractAllForeignKeys()
    {
        // Act
        var table = _analyzer.AnalyzeType(typeof(OrderItem));

        // Assert
        var fkConstraints = table!.Constraints.Where(c => c.Type == ConstraintType.ForeignKey).ToList();
        fkConstraints.Should().HaveCount(2);

        fkConstraints.Should().Contain(c => c.ReferencedTable == "Orders" && c.Columns.Contains("OrderId"));
        fkConstraints.Should().Contain(c => c.ReferencedTable == "Products" && c.Columns.Contains("ProductId"));
    }
}

// Test models for specific analyzer scenarios
[Table("custom.TestTable")]
public class SchemaTestModel
{
    [Key]
    public int Id { get; set; }
}

public class ExplicitKeyModel
{
    [ExplicitKey]
    public string Code { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
}

public class WriteAttributeTestModel
{
    [Key]
    public int Id { get; set; }
    
    [Write(false)]
    public string IgnoredColumn { get; set; } = string.Empty;
    
    [Write(true)]
    public string IncludedColumn { get; set; } = string.Empty;
}

public class CustomColumnNameModel
{
    [Key]
    public int Id { get; set; }
    
    [Column(Name = "custom_name")]
    public string PropertyName { get; set; } = string.Empty;
}