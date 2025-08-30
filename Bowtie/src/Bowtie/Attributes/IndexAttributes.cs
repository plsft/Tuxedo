using System;

namespace Bowtie.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class IndexAttribute : Attribute
    {
        public string? Name { get; set; }
        public bool IsUnique { get; set; }
        public int Order { get; set; }
        public string? Group { get; set; }
        public IndexType IndexType { get; set; } = IndexType.BTree;
        public string? Include { get; set; }
        public string? Where { get; set; }
        public bool IsDescending { get; set; }

        public IndexAttribute() { }
        
        public IndexAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class UniqueAttribute : Attribute
    {
        public string? Name { get; set; }
        public string? Group { get; set; }
        public int Order { get; set; }

        public UniqueAttribute() { }
        
        public UniqueAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
        public int Order { get; set; }
        public bool IsIdentity { get; set; } = true;

        public PrimaryKeyAttribute() { }
        
        public PrimaryKeyAttribute(int order)
        {
            Order = order;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ForeignKeyAttribute : Attribute
    {
        public string ReferencedTable { get; }
        public string? ReferencedColumn { get; set; }
        public string? Name { get; set; }
        public ReferentialAction OnDelete { get; set; } = ReferentialAction.NoAction;
        public ReferentialAction OnUpdate { get; set; } = ReferentialAction.NoAction;

        public ForeignKeyAttribute(string referencedTable)
        {
            ReferencedTable = referencedTable;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CheckConstraintAttribute : Attribute
    {
        public string Expression { get; }
        public string? Name { get; set; }

        public CheckConstraintAttribute(string expression)
        {
            Expression = expression;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DefaultValueAttribute : Attribute
    {
        public object Value { get; }
        public bool IsRawSql { get; set; }

        public DefaultValueAttribute(object value)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ColumnAttribute : Attribute
    {
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public int MaxLength { get; set; } = -1;
        public int Precision { get; set; } = -1;
        public int Scale { get; set; } = -1;
        public bool IsNullable { get; set; } = true;
        public string? Collation { get; set; }

        public ColumnAttribute() { }
        
        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }

    public enum IndexType
    {
        BTree,
        Hash,
        GIN,
        GiST,
        BRIN,
        SPGiST,
        Clustered,
        NonClustered,
        ColumnStore,
        Spatial,
        FullText
    }

    public enum ReferentialAction
    {
        NoAction,
        Cascade,
        SetNull,
        SetDefault,
        Restrict
    }
}