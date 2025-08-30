using Tuxedo.Contrib;
using Bowtie.Attributes;

namespace Bowtie.Examples
{
    /// <summary>
    /// Basic product model demonstrating common attributes
    /// </summary>
    [Table("Products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }
        
        [Column(MaxLength = 100)]
        [Index("IX_Products_Name")]
        public string Name { get; set; } = string.Empty;
        
        [Column(Precision = 18, Scale = 2)]
        [CheckConstraint("Price > 0")]
        public decimal Price { get; set; }
        
        [ForeignKey("Categories")]
        public int CategoryId { get; set; }
        
        [Index("IX_Products_Category", Order = 1)]
        [Column(MaxLength = 50)]
        public string Category { get; set; } = string.Empty;
        
        [Column(MaxLength = 500)]
        public string? Description { get; set; }
        
        [DefaultValue(true)]
        public bool IsActive { get; set; }
        
        [Index("IX_Products_CreatedDate")]
        [DefaultValue("GETDATE()", IsRawSql = true)]
        public DateTime CreatedDate { get; set; }
        
        [Computed]
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Category model with unique constraints
    /// </summary>
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int Id { get; set; }
        
        [Column(MaxLength = 100)]
        [Unique("UQ_Categories_Name")]
        public string Name { get; set; } = string.Empty;
        
        [Column(MaxLength = 50)]
        [Unique("UQ_Categories_Code")]
        public string Code { get; set; } = string.Empty;
        
        [Column(MaxLength = 500)]
        public string? Description { get; set; }
        
        [ForeignKey("Categories")]
        public int? ParentCategoryId { get; set; }
        
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Order model with composite primary key
    /// </summary>
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }
        
        [Column(MaxLength = 20)]
        [Index("IX_Orders_OrderNumber")]
        public string OrderNumber { get; set; } = string.Empty;
        
        [ForeignKey("Customers")]
        [Index("IX_Orders_Customer", Order = 1)]
        public int CustomerId { get; set; }
        
        [Index("IX_Orders_OrderDate")]
        public DateTime OrderDate { get; set; }
        
        [Column(Precision = 18, Scale = 2)]
        public decimal TotalAmount { get; set; }
        
        [Column(MaxLength = 20)]
        public string Status { get; set; } = "Pending";
        
        [Column(MaxLength = 500)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Order items with composite primary key
    /// </summary>
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
        public decimal UnitPrice { get; set; }
        
        [Column(Precision = 5, Scale = 4)]
        [DefaultValue(0)]
        public decimal DiscountRate { get; set; }
        
        [Column(MaxLength = 200)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Customer model with various indexes
    /// </summary>
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
        
        [Column(MaxLength = 200)]
        public string? Address { get; set; }
        
        [Column(MaxLength = 100)]
        public string? City { get; set; }
        
        [Column(MaxLength = 20)]
        public string? PostalCode { get; set; }
        
        [Column(MaxLength = 100)]
        public string? Country { get; set; }
        
        [DefaultValue(true)]
        public bool IsActive { get; set; }
        
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// PostgreSQL-specific model with GIN indexes for JSONB
    /// </summary>
    [Table("Documents")]
    public class Document
    {
        [Key]
        public int Id { get; set; }
        
        [Column(MaxLength = 200)]
        [Index("IX_Documents_Title")]
        public string Title { get; set; } = string.Empty;
        
        [Column(TypeName = "jsonb")]
        [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
        public string Content { get; set; } = "{}";
        
        [Column(TypeName = "text[]")]
        [Index("IX_Documents_Tags_GIN", IndexType = IndexType.GIN)]
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        [Column(MaxLength = 100)]
        public string DocumentType { get; set; } = string.Empty;
        
        [Index("IX_Documents_CreatedBy")]
        [ForeignKey("Users")]
        public int CreatedBy { get; set; }
        
        public DateTime CreatedDate { get; set; }
        
        public DateTime? ModifiedDate { get; set; }
    }

    /// <summary>
    /// User model with unique constraints and indexes
    /// </summary>
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Column(MaxLength = 100)]
        [Unique("UQ_Users_Username")]
        [Index("IX_Users_Username")]
        public string Username { get; set; } = string.Empty;
        
        [Column(MaxLength = 200)]
        [Unique("UQ_Users_Email")]
        public string Email { get; set; } = string.Empty;
        
        [Column(MaxLength = 255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Column(MaxLength = 100)]
        public string? FirstName { get; set; }
        
        [Column(MaxLength = 100)]
        public string? LastName { get; set; }
        
        [Column(MaxLength = 50)]
        public string Role { get; set; } = "User";
        
        [DefaultValue(true)]
        public bool IsActive { get; set; }
        
        public DateTime CreatedDate { get; set; }
        
        public DateTime? LastLoginDate { get; set; }
    }

    /// <summary>
    /// Audit log model demonstrating various column types and constraints
    /// </summary>
    [Table("AuditLog")]
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }
        
        [Column(MaxLength = 50)]
        [Index("IX_AuditLog_TableName")]
        public string TableName { get; set; } = string.Empty;
        
        [Column(MaxLength = 20)]
        [Index("IX_AuditLog_Action")]
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
        
        [Column(MaxLength = 50)]
        public string PrimaryKeyValue { get; set; } = string.Empty;
        
        [Column(TypeName = "nvarchar(max)")]
        public string? OldValues { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? NewValues { get; set; }
        
        [Index("IX_AuditLog_UserId")]
        [ForeignKey("Users")]
        public int? UserId { get; set; }
        
        [Index("IX_AuditLog_Timestamp")]
        [DefaultValue("GETUTCDATE()", IsRawSql = true)]
        public DateTime Timestamp { get; set; }
        
        [Column(MaxLength = 100)]
        public string? UserAgent { get; set; }
        
        [Column(MaxLength = 45)] // IPv6 length
        public string? IpAddress { get; set; }
    }

    /// <summary>
    /// SQL Server-specific model with clustered and non-clustered indexes
    /// </summary>
    [Table("Analytics")]
    public class Analytics
    {
        [Key]
        public long Id { get; set; }
        
        [Index("IX_Analytics_EventDate_Clustered", IndexType = IndexType.Clustered)]
        public DateTime EventDate { get; set; }
        
        [Column(MaxLength = 100)]
        [Index("IX_Analytics_EventType", IndexType = IndexType.NonClustered)]
        public string EventType { get; set; } = string.Empty;
        
        [Column(MaxLength = 200)]
        public string? EventData { get; set; }
        
        [Index("IX_Analytics_UserId")]
        public int? UserId { get; set; }
        
        [Column(MaxLength = 100)]
        public string? SessionId { get; set; }
        
        public int Count { get; set; } = 1;
        
        [Column(Precision = 18, Scale = 6)]
        public decimal? Duration { get; set; }
    }

    /// <summary>
    /// MySQL-specific model with various MySQL features
    /// </summary>
    [Table("Products_Search")]
    public class ProductSearch
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey("Products")]
        public int ProductId { get; set; }
        
        [Column(MaxLength = 200)]
        [Index("IX_ProductSearch_Title_FullText", IndexType = IndexType.FullText)]
        public string Title { get; set; } = string.Empty;
        
        [Column(TypeName = "TEXT")]
        [Index("IX_ProductSearch_Description_FullText", IndexType = IndexType.FullText)]
        public string Description { get; set; } = string.Empty;
        
        [Column(TypeName = "TEXT")]
        public string Keywords { get; set; } = string.Empty;
        
        [Column(MaxLength = 50)]
        public string Language { get; set; } = "en";
        
        public DateTime LastUpdated { get; set; }
    }
}