using Tuxedo.Contrib;
using Bowtie.Attributes;

namespace Bowtie.Tests;

[Table("Users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Users_Username")]
    [Unique("UQ_Users_Username")]
    public string Username { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    [Index("IX_Users_Email")]
    public string Email { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_Products_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("Price > 0")]
    public decimal Price { get; set; }

    [ForeignKey("Categories")]
    public int CategoryId { get; set; }

    [Column(MaxLength = 100)]
    public string Category { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Categories")]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Unique("UQ_Categories_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(MaxLength = 20)]
    [Unique("UQ_Categories_Code")]
    public string Code { get; set; } = string.Empty;

    [DefaultValue(true)]
    public bool IsActive { get; set; }
}

[Table("Documents")]
public class Document
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
    public string Content { get; set; } = "{}";

    [Index("IX_Documents_CreatedDate")]
    public DateTime CreatedDate { get; set; }
}