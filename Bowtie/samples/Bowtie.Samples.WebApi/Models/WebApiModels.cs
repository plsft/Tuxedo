using Tuxedo.Contrib;
using Bowtie.Attributes;

namespace Bowtie.Samples.WebApi.Models;

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

    [Column(MaxLength = 100)]
    [Index("IX_Products_Category")]
    public string Category { get; set; } = string.Empty;

    [Column(MaxLength = 50)]
    public string? Brand { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Products_Sku")]
    [Unique("UQ_Products_Sku")]
    public string Sku { get; set; } = string.Empty;

    [CheckConstraint("StockQuantity >= 0")]
    [DefaultValue(0)]
    public int StockQuantity { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    [DefaultValue("GETUTCDATE()", IsRawSql = true)]
    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalInfo { get; set; }
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

    [Column(MaxLength = 500)]
    public string? Description { get; set; }

    [Column(MaxLength = 20)]
    [Unique("UQ_Categories_Code")]
    public string Code { get; set; } = string.Empty;

    [ForeignKey("Categories")]
    public int? ParentCategoryId { get; set; }

    [CheckConstraint("SortOrder >= 0")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Orders")]
public class Order
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Orders_OrderNumber")]
    [Unique("UQ_Orders_OrderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [ForeignKey("Customers")]
    [Index("IX_Orders_Customer")]
    public int CustomerId { get; set; }

    [Index("IX_Orders_OrderDate")]
    public DateTime OrderDate { get; set; }

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("TotalAmount >= 0")]
    public decimal TotalAmount { get; set; }

    [Column(MaxLength = 30)]
    [Index("IX_Orders_Status")]
    [DefaultValue("Pending")]
    public string Status { get; set; } = "Pending";

    [Column(MaxLength = 1000)]
    public string? Notes { get; set; }

    [Column(MaxLength = 200)]
    public string? ShippingAddress { get; set; }

    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

[Table("Customers")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_Customers_FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Column(MaxLength = 100)]
    [Index("IX_Customers_LastName")]
    public string LastName { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    [Index("IX_Customers_Email")]
    [Unique("UQ_Customers_Email")]
    public string Email { get; set; } = string.Empty;

    [Column(MaxLength = 20)]
    public string? Phone { get; set; }

    [Column(MaxLength = 300)]
    public string? Address { get; set; }

    [Column(MaxLength = 100)]
    public string? City { get; set; }

    [Column(MaxLength = 100)]
    public string? State { get; set; }

    [Column(MaxLength = 20)]
    public string? PostalCode { get; set; }

    [Column(MaxLength = 100)]
    public string? Country { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    [Computed]
    public string FullName => $"{FirstName} {LastName}".Trim();
}