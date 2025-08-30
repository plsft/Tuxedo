using Bowtie.Attributes;
using Bowtie.Core;
using FluentAssertions;
using NUnit.Framework;
using Tuxedo.Contrib;

namespace Bowtie.NUnit.Tests.Attributes;

[TestFixture]
public class AttributeTests
{
    [Test]
    public void IndexAttribute_WithDefaultValues_ShouldHaveCorrectDefaults()
    {
        // Act
        var attr = new IndexAttribute();

        // Assert
        attr.Name.Should().BeNull();
        attr.IsUnique.Should().BeFalse();
        attr.Order.Should().Be(0);
        attr.Group.Should().BeNull();
        attr.IndexType.Should().Be(IndexType.BTree);
        attr.Include.Should().BeNull();
        attr.Where.Should().BeNull();
        attr.IsDescending.Should().BeFalse();
    }

    [Test]
    public void IndexAttribute_WithNameConstructor_ShouldSetName()
    {
        // Act
        var attr = new IndexAttribute("IX_Custom");

        // Assert
        attr.Name.Should().Be("IX_Custom");
    }

    [Test]
    public void IndexAttribute_WithProperties_ShouldSetAllProperties()
    {
        // Act
        var attr = new IndexAttribute("IX_Test")
        {
            IsUnique = true,
            Order = 2,
            Group = "TestGroup",
            IndexType = IndexType.GIN,
            Include = "Col1, Col2",
            Where = "IsActive = 1",
            IsDescending = true
        };

        // Assert
        attr.Name.Should().Be("IX_Test");
        attr.IsUnique.Should().BeTrue();
        attr.Order.Should().Be(2);
        attr.Group.Should().Be("TestGroup");
        attr.IndexType.Should().Be(IndexType.GIN);
        attr.Include.Should().Be("Col1, Col2");
        attr.Where.Should().Be("IsActive = 1");
        attr.IsDescending.Should().BeTrue();
    }

    [Test]
    public void UniqueAttribute_WithDefaultValues_ShouldHaveCorrectDefaults()
    {
        // Act
        var attr = new UniqueAttribute();

        // Assert
        attr.Name.Should().BeNull();
        attr.Group.Should().BeNull();
        attr.Order.Should().Be(0);
    }

    [Test]
    public void UniqueAttribute_WithNameConstructor_ShouldSetName()
    {
        // Act
        var attr = new UniqueAttribute("UQ_Email");

        // Assert
        attr.Name.Should().Be("UQ_Email");
    }

    [Test]
    public void PrimaryKeyAttribute_WithDefaultValues_ShouldHaveCorrectDefaults()
    {
        // Act
        var attr = new PrimaryKeyAttribute();

        // Assert
        attr.Order.Should().Be(0);
        attr.IsIdentity.Should().BeTrue();
    }

    [Test]
    public void PrimaryKeyAttribute_WithOrderConstructor_ShouldSetOrder()
    {
        // Act
        var attr = new PrimaryKeyAttribute(5);

        // Assert
        attr.Order.Should().Be(5);
        attr.IsIdentity.Should().BeTrue();
    }

    [Test]
    public void ForeignKeyAttribute_WithReferencedTable_ShouldSetProperties()
    {
        // Act
        var attr = new ForeignKeyAttribute("Categories")
        {
            ReferencedColumn = "CategoryId",
            Name = "FK_Custom",
            OnDelete = ReferentialAction.Cascade,
            OnUpdate = ReferentialAction.SetNull
        };

        // Assert
        attr.ReferencedTable.Should().Be("Categories");
        attr.ReferencedColumn.Should().Be("CategoryId");
        attr.Name.Should().Be("FK_Custom");
        attr.OnDelete.Should().Be(ReferentialAction.Cascade);
        attr.OnUpdate.Should().Be(ReferentialAction.SetNull);
    }

    [Test]
    public void ForeignKeyAttribute_WithDefaults_ShouldHaveCorrectDefaults()
    {
        // Act
        var attr = new ForeignKeyAttribute("TestTable");

        // Assert
        attr.ReferencedTable.Should().Be("TestTable");
        attr.ReferencedColumn.Should().BeNull();
        attr.Name.Should().BeNull();
        attr.OnDelete.Should().Be(ReferentialAction.NoAction);
        attr.OnUpdate.Should().Be(ReferentialAction.NoAction);
    }

    [Test]
    public void CheckConstraintAttribute_WithExpression_ShouldSetExpression()
    {
        // Act
        var attr = new CheckConstraintAttribute("Price > 0")
        {
            Name = "CK_Price_Positive"
        };

        // Assert
        attr.Expression.Should().Be("Price > 0");
        attr.Name.Should().Be("CK_Price_Positive");
    }

    [Test]
    public void DefaultValueAttribute_WithValue_ShouldSetValue()
    {
        // Act
        var attr = new DefaultValueAttribute("DefaultValue")
        {
            IsRawSql = true
        };

        // Assert
        attr.Value.Should().Be("DefaultValue");
        attr.IsRawSql.Should().BeTrue();
    }

    [Test]
    public void DefaultValueAttribute_WithNonStringValue_ShouldAcceptAnyType()
    {
        // Act
        var intAttr = new DefaultValueAttribute(42);
        var boolAttr = new DefaultValueAttribute(true);
        var dateAttr = new DefaultValueAttribute(DateTime.Now);

        // Assert
        intAttr.Value.Should().Be(42);
        boolAttr.Value.Should().Be(true);
        dateAttr.Value.Should().BeOfType<DateTime>();
    }

    [Test]
    public void ColumnAttribute_WithAllProperties_ShouldSetAllProperties()
    {
        // Act
        var attr = new ColumnAttribute("custom_name")
        {
            TypeName = "varchar(255)",
            MaxLength = 255,
            Precision = 18,
            Scale = 2,
            IsNullable = false,
            Collation = "utf8_general_ci"
        };

        // Assert
        attr.Name.Should().Be("custom_name");
        attr.TypeName.Should().Be("varchar(255)");
        attr.MaxLength.Should().Be(255);
        attr.Precision.Should().Be(18);
        attr.Scale.Should().Be(2);
        attr.IsNullable.Should().BeFalse();
        attr.Collation.Should().Be("utf8_general_ci");
    }

    [Test]
    public void ColumnAttribute_WithDefaults_ShouldHaveCorrectDefaults()
    {
        // Act
        var attr = new ColumnAttribute();

        // Assert
        attr.Name.Should().BeNull();
        attr.TypeName.Should().BeNull();
        attr.MaxLength.Should().Be(-1); // Indicates not set
        attr.Precision.Should().Be(-1); // Indicates not set
        attr.Scale.Should().Be(-1); // Indicates not set
        attr.IsNullable.Should().BeTrue();
        attr.Collation.Should().BeNull();
    }

    [Test]
    public void IndexType_ShouldHaveAllExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<IndexType>();
        values.Should().Contain(IndexType.BTree);
        values.Should().Contain(IndexType.Hash);
        values.Should().Contain(IndexType.GIN);
        values.Should().Contain(IndexType.GiST);
        values.Should().Contain(IndexType.BRIN);
        values.Should().Contain(IndexType.SPGiST);
        values.Should().Contain(IndexType.Clustered);
        values.Should().Contain(IndexType.NonClustered);
        values.Should().Contain(IndexType.ColumnStore);
        values.Should().Contain(IndexType.Spatial);
        values.Should().Contain(IndexType.FullText);
    }

    [Test]
    public void ReferentialAction_ShouldHaveAllExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<ReferentialAction>();
        values.Should().Contain(ReferentialAction.NoAction);
        values.Should().Contain(ReferentialAction.Cascade);
        values.Should().Contain(ReferentialAction.SetNull);
        values.Should().Contain(ReferentialAction.SetDefault);
        values.Should().Contain(ReferentialAction.Restrict);
    }

    [Test]
    public void AttributeUsage_ShouldAllowMultipleIndexAttributes()
    {
        // This test validates that IndexAttribute can be applied multiple times
        var type = typeof(MultipleIndexModel);
        var property = type.GetProperty(nameof(MultipleIndexModel.TestProperty))!;
        
        var indexAttributes = property.GetCustomAttributes(typeof(IndexAttribute), false).Cast<IndexAttribute>().ToList();
        
        indexAttributes.Should().HaveCount(2);
        indexAttributes.Should().Contain(a => a.Name == "IX_Test_1");
        indexAttributes.Should().Contain(a => a.Name == "IX_Test_2");
    }
}

// Test models for attribute testing
public class MultipleIndexModel
{
    [Key]
    public int Id { get; set; }
    
    [Index("IX_Test_1")]
    [Index("IX_Test_2", IsUnique = true)]
    public string TestProperty { get; set; } = string.Empty;
}