using Tuxedo.Contrib;
using Bowtie.Attributes;

namespace Bowtie.Samples.Console.Models;

/// <summary>
/// User model demonstrating basic attributes and relationships
/// </summary>
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
    [Unique("UQ_Users_Email")]
    public string Email { get; set; } = string.Empty;

    [Column(MaxLength = 255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column(MaxLength = 100)]
    public string? FirstName { get; set; }

    [Column(MaxLength = 100)]
    public string? LastName { get; set; }

    [Column(MaxLength = 50)]
    [DefaultValue("User")]
    public string Role { get; set; } = "User";

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    [Index("IX_Users_CreatedDate")]
    [DefaultValue("GETUTCDATE()", IsRawSql = true)]
    public DateTime CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    // Navigation property - not mapped to database
    [Computed]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Blog model with PostgreSQL JSONB support
/// </summary>
[Table("Blogs")]
public class Blog
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_Blogs_Title")]
    public string Title { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string? Description { get; set; }

    [Column(TypeName = "jsonb")] // PostgreSQL specific
    [Index("IX_Blogs_Metadata_GIN", IndexType = IndexType.GIN)]
    public string Metadata { get; set; } = "{}";

    [ForeignKey("Users")]
    [Index("IX_Blogs_Owner")]
    public int OwnerId { get; set; }

    [DefaultValue(true)]
    public bool IsPublic { get; set; }

    [Index("IX_Blogs_CreatedDate")]
    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }
}

/// <summary>
/// Post model with full-text search support
/// </summary>
[Table("Posts")]
public class Post
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 300)]
    [Index("IX_Posts_Title_FullText", IndexType = IndexType.FullText)] // MySQL/SQL Server
    public string Title { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string? Summary { get; set; }

    [Column(TypeName = "nvarchar(max)")] // SQL Server
    [Index("IX_Posts_Content_FullText", IndexType = IndexType.FullText)]
    public string Content { get; set; } = string.Empty;

    [ForeignKey("Blogs")]
    [Index("IX_Posts_Blog")]
    public int BlogId { get; set; }

    [ForeignKey("Users")]
    [Index("IX_Posts_Author")]
    public int AuthorId { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Posts_Status")]
    [DefaultValue("Draft")]
    public string Status { get; set; } = "Draft"; // Draft, Published, Archived

    [Index("IX_Posts_PublishedDate")]
    public DateTime? PublishedDate { get; set; }

    [Index("IX_Posts_CreatedDate")]
    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    [CheckConstraint("ViewCount >= 0")]
    [DefaultValue(0)]
    public int ViewCount { get; set; }
}

/// <summary>
/// Tag model with many-to-many relationship support
/// </summary>
[Table("Tags")]
public class Tag
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Tags_Name")]
    [Unique("UQ_Tags_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    public string? Description { get; set; }

    [Column(MaxLength = 20)]
    [Index("IX_Tags_Color")]
    [DefaultValue("#007bff")]
    public string Color { get; set; } = "#007bff";

    [CheckConstraint("UsageCount >= 0")]
    [DefaultValue(0)]
    public int UsageCount { get; set; }

    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Junction table for many-to-many relationship
/// </summary>
[Table("PostTags")]
public class PostTag
{
    [PrimaryKey(Order = 1)]
    [ForeignKey("Posts")]
    public int PostId { get; set; }

    [PrimaryKey(Order = 2)]
    [ForeignKey("Tags")]
    public int TagId { get; set; }

    [Index("IX_PostTags_CreatedDate")]
    public DateTime CreatedDate { get; set; }

    [ForeignKey("Users")]
    public int? CreatedBy { get; set; }
}

/// <summary>
/// Analytics model with SQL Server columnstore index
/// </summary>
[Table("Analytics")]
public class Analytics
{
    [Key]
    public long Id { get; set; }

    [Index("IX_Analytics_Date_Clustered", IndexType = IndexType.Clustered)] // SQL Server
    public DateTime Date { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Analytics_EventType")]
    public string EventType { get; set; } = string.Empty;

    [Column(MaxLength = 100)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    [ForeignKey("Users")]
    [Index("IX_Analytics_User")]
    public int? UserId { get; set; }

    [Column(MaxLength = 45)] // IPv6 length
    public string? IpAddress { get; set; }

    [Column(MaxLength = 500)]
    public string? UserAgent { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalData { get; set; }

    [CheckConstraint("Duration >= 0")]
    public int Duration { get; set; } // milliseconds

    [DefaultValue(1)]
    public int Count { get; set; }
}

/// <summary>
/// Configuration model with various constraint types
/// </summary>
[Table("Configuration")]
public class Configuration
{
    [Key]
    [Column(MaxLength = 100)]
    public string Key { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string Value { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string? Description { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Configuration_Category")]
    [DefaultValue("General")]
    public string Category { get; set; } = "General";

    [DefaultValue(false)]
    public bool IsEncrypted { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    [Index("IX_Configuration_ModifiedDate")]
    public DateTime ModifiedDate { get; set; }

    [ForeignKey("Users")]
    public int? ModifiedBy { get; set; }

    [CheckConstraint("LEN([Key]) > 0")]
    public bool KeyValidation => true; // For check constraint only
}

/// <summary>
/// File model demonstrating spatial and advanced indexing
/// </summary>
[Table("Files")]
public class FileModel
{
    [Key]
    public Guid Id { get; set; }

    [Column(MaxLength = 255)]
    [Index("IX_Files_FileName")]
    public string FileName { get; set; } = string.Empty;

    [Column(MaxLength = 500)]
    public string FilePath { get; set; } = string.Empty;

    [Column(MaxLength = 10)]
    [Index("IX_Files_Extension")]
    public string Extension { get; set; } = string.Empty;

    [CheckConstraint("FileSize > 0")]
    public long FileSize { get; set; }

    [Column(MaxLength = 100)]
    public string ContentType { get; set; } = string.Empty;

    [Column(MaxLength = 64)] // SHA256 hash length
    [Index("IX_Files_Hash")]
    [Unique("UQ_Files_Hash")]
    public string? Hash { get; set; }

    [ForeignKey("Users")]
    [Index("IX_Files_UploadedBy")]
    public int UploadedBy { get; set; }

    [Index("IX_Files_UploadedDate")]
    public DateTime UploadedDate { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }

    // Spatial data (SQL Server/PostgreSQL/MySQL)
    [Column(TypeName = "geography")] // SQL Server specific
    [Index("IX_Files_Location_Spatial", IndexType = IndexType.Spatial)]
    public string? GeoLocation { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Metadata { get; set; }
}

/// <summary>
/// Audit log for tracking changes
/// </summary>
[Table("AuditLog")]
public class AuditLog
{
    [Key]
    public long Id { get; set; }

    [Column(MaxLength = 100)]
    [Index("IX_AuditLog_TableName")]
    public string TableName { get; set; } = string.Empty;

    [Column(MaxLength = 20)]
    [Index("IX_AuditLog_Action")]
    public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE

    [Column(MaxLength = 50)]
    public string RecordId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? OldValues { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? NewValues { get; set; }

    [ForeignKey("Users")]
    [Index("IX_AuditLog_User")]
    public int? UserId { get; set; }

    [Index("IX_AuditLog_Timestamp")]
    [DefaultValue("GETUTCDATE()", IsRawSql = true)]
    public DateTime Timestamp { get; set; }

    [Column(MaxLength = 45)]
    public string? IpAddress { get; set; }

    [Column(MaxLength = 500)]
    public string? UserAgent { get; set; }

    [Column(MaxLength = 100)]
    public string? SessionId { get; set; }
}