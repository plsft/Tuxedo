using System;

namespace Tuxedo.Contrib
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TableAttribute : Attribute
    {
        public string Name { get; }
        public TableAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class KeyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class ExplicitKeyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class ComputedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class WriteAttribute : Attribute
    {
        public bool Write { get; }
        public WriteAttribute(bool write) => Write = write;
    }
}

