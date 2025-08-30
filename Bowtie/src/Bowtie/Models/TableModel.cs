using System.Reflection;

namespace Bowtie.Models
{
    public class TableModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public Type ModelType { get; set; } = null!;
        public List<ColumnModel> Columns { get; set; } = new();
        public List<IndexModel> Indexes { get; set; } = new();
        public List<ConstraintModel> Constraints { get; set; } = new();
        public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    }

    public class ColumnModel
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; } = true;
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
        public object? DefaultValue { get; set; }
        public bool IsDefaultRawSql { get; set; }
        public string? Collation { get; set; }
        public PropertyInfo PropertyInfo { get; set; } = null!;
        public Type PropertyType { get; set; } = null!;
    }

    public class IndexModel
    {
        public string Name { get; set; } = string.Empty;
        public List<IndexColumnModel> Columns { get; set; } = new();
        public bool IsUnique { get; set; }
        public IndexType IndexType { get; set; } = IndexType.BTree;
        public string? IncludeColumns { get; set; }
        public string? WhereClause { get; set; }
        public bool IsClustered { get; set; }
    }

    public class IndexColumnModel
    {
        public string ColumnName { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsDescending { get; set; }
    }

    public class ConstraintModel
    {
        public string Name { get; set; } = string.Empty;
        public ConstraintType Type { get; set; }
        public List<string> Columns { get; set; } = new();
        public string? ReferencedTable { get; set; }
        public string? ReferencedColumn { get; set; }
        public ReferentialAction OnDelete { get; set; } = ReferentialAction.NoAction;
        public ReferentialAction OnUpdate { get; set; } = ReferentialAction.NoAction;
        public string? CheckExpression { get; set; }
    }

    public enum ConstraintType
    {
        PrimaryKey,
        ForeignKey,
        Unique,
        Check
    }
}