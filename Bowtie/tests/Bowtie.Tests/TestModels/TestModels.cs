using Tuxedo.Contrib;
using Bowtie.Attributes;

namespace Bowtie.Tests.TestModels;

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

    [DefaultValue("GETUTCDATE()", IsRawSql = true)]
    public DateTime CreatedDate { get; set; }

    [Computed]
    public string DisplayName => $"User: {Username}";
}

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_Products_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string? Description { get; set; }

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("Price > 0")]
    public decimal Price { get; set; }

    [ForeignKey("Categories", OnDelete = ReferentialAction.Cascade)]
    [Index("IX_Products_Category")]
    public int CategoryId { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Products_Category_Price", Group = "CategoryPrice", Order = 1)]
    public string Category { get; set; } = string.Empty;

    [Index("IX_Products_Category_Price", Group = "CategoryPrice", Order = 2)]
    public decimal ListPrice { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    [DefaultValue(0)]
    [CheckConstraint("StockQuantity >= 0")]
    public int StockQuantity { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

[Table("Categories")]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Categories_Name")]
    [Unique("UQ_Categories_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(MaxLength = 20)]
    [Unique("UQ_Categories_Code")]
    public string Code { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string? Description { get; set; }

    [ForeignKey("Categories")]
    public int? ParentCategoryId { get; set; }

    [DefaultValue(0)]
    [CheckConstraint("SortOrder >= 0")]
    public int SortOrder { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Documents")]
public class Document
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 300)]
    [Index("IX_Documents_Title")]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
    public string Content { get; set; } = "{}";

    [Column(TypeName = "text[]")]
    [Index("IX_Documents_Tags_GIN", IndexType = IndexType.GIN)]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [ForeignKey("Users")]
    [Index("IX_Documents_Author")]
    public int AuthorId { get; set; }

    [Index("IX_Documents_CreatedDate")]
    public DateTime CreatedDate { get; set; }

    [Column(MaxLength = 50)]
    [DefaultValue("Draft")]
    public string Status { get; set; } = "Draft";
}

[Table("OrderItems")]
public class OrderItem
{
    [PrimaryKey(Order = 1)]
    [ForeignKey("Orders")]
    public int OrderId { get; set; }

    [PrimaryKey(Order = 2)]
    [ForeignKey("Products")]
    public int ProductId { get; set; }

    [CheckConstraint("Quantity > 0")]
    public int Quantity { get; set; }

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("UnitPrice >= 0")]
    public decimal UnitPrice { get; set; }

    [Column(Precision = 5, Scale = 4)]
    [DefaultValue(0)]
    [CheckConstraint("DiscountRate >= 0 AND DiscountRate <= 1")]
    public decimal DiscountRate { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Analytics")]
public class Analytics
{
    [Key]
    public long Id { get; set; }

    [Index("IX_Analytics_EventDate_Clustered", IndexType = IndexType.Clustered)]
    public DateTime EventDate { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Analytics_EventType")]
    public string EventType { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    [ForeignKey("Users")]
    [Index("IX_Analytics_User")]
    public int? UserId { get; set; }

    [Column(MaxLength = 45)]
    public string? IpAddress { get; set; }

    [CheckConstraint("Duration >= 0")]
    public int Duration { get; set; }

    [DefaultValue(1)]
    public int Count { get; set; }
}

[Table("SearchIndex")]
public class SearchIndex
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_SearchIndex_Title_FullText", IndexType = IndexType.FullText)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")]
    [Index("IX_SearchIndex_Content_FullText", IndexType = IndexType.FullText)]
    public string Content { get; set; } = string.Empty;

    [Column(MaxLength = 100)]
    public string Language { get; set; } = "en";

    [Index("IX_SearchIndex_LastUpdated")]
    public DateTime LastUpdated { get; set; }
}

[Table("Locations")]
public class Location
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "geography")]
    [Index("IX_Locations_Coordinates_Spatial", IndexType = IndexType.Spatial)]
    public string? Coordinates { get; set; }

    [Column(Precision = 10, Scale = 7)]
    public decimal? Latitude { get; set; }

    [Column(Precision = 10, Scale = 7)]
    public decimal? Longitude { get; set; }

    [Column(MaxLength = 100)]
    public string? City { get; set; }

    [Column(MaxLength = 100)]
    public string? Country { get; set; }
}