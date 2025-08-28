using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Tuxedo
{
    public static partial class SqlMapper
    {
        [TypeDescriptionProvider(typeof(TuxedoRowTypeDescriptionProvider))]
        private sealed partial class TuxedoRow
        {
            private sealed class TuxedoRowTypeDescriptionProvider : TypeDescriptionProvider
            {
                public override ICustomTypeDescriptor GetExtendedTypeDescriptor(object instance)
                    => new TuxedoRowTypeDescriptor(instance);
                public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
                    => new TuxedoRowTypeDescriptor(instance!);
            }

            //// in theory we could implement this for zero-length results to bind; would require
            //// additional changes, though, to capture a table even when no rows - so not currently provided
            //internal sealed class TuxedoRowList : List<TuxedoRow>, ITypedList
            //{
            //    private readonly TuxedoTable _table;
            //    public TuxedoRowList(TuxedoTable table) { _table = table; }
            //    PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors)
            //    {
            //        if (listAccessors is not null && listAccessors.Length != 0) return PropertyDescriptorCollection.Empty;

            //        return TuxedoRowTypeDescriptor.GetProperties(_table);
            //    }

            //    string ITypedList.GetListName(PropertyDescriptor[] listAccessors) => null;
            //}

            private sealed class TuxedoRowTypeDescriptor : ICustomTypeDescriptor
            {
                private readonly TuxedoRow _row;
                public TuxedoRowTypeDescriptor(object instance)
                    => _row = (TuxedoRow)instance;

                AttributeCollection ICustomTypeDescriptor.GetAttributes()
                    => AttributeCollection.Empty;

                string ICustomTypeDescriptor.GetClassName() => typeof(TuxedoRow).FullName!;

                string ICustomTypeDescriptor.GetComponentName() => null!;

                private static readonly TypeConverter s_converter = new ExpandableObjectConverter();
                TypeConverter ICustomTypeDescriptor.GetConverter() => s_converter;

                EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() => null!;

                PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => null!;

                object ICustomTypeDescriptor.GetEditor(Type editorBaseType) => null!;

                EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => EventDescriptorCollection.Empty;

                EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) => EventDescriptorCollection.Empty;

                internal static PropertyDescriptorCollection GetProperties(TuxedoRow row) => GetProperties(row?.table, row);
                internal static PropertyDescriptorCollection GetProperties(TuxedoTable? table, IDictionary<string,object?>? row = null)
                {
                    string[]? names = table?.FieldNames;
                    if (names is null || names.Length == 0) return PropertyDescriptorCollection.Empty;
                    var arr = new PropertyDescriptor[names.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var type = row is not null && row.TryGetValue(names[i], out var value) && value is not null
                            ? value.GetType() : typeof(object);
                        arr[i] = new RowBoundPropertyDescriptor(type, names[i], i);
                    }
                    return new PropertyDescriptorCollection(arr, true);
                }
                PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => GetProperties(_row);

                PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes) => GetProperties(_row);

                object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => _row;
            }

            private sealed class RowBoundPropertyDescriptor : PropertyDescriptor
            {
                private readonly Type _type;
                private readonly int _index;
                public RowBoundPropertyDescriptor(Type type, string name, int index) : base(name, null)
                {
                    _type = type;
                    _index = index;
                }
                public override bool CanResetValue(object component) => true;
                public override void ResetValue(object component) => ((TuxedoRow)component).Remove(_index);
                public override bool IsReadOnly => false;
                public override bool ShouldSerializeValue(object component) => ((TuxedoRow)component).TryGetValue(_index, out _);
                public override Type ComponentType => typeof(TuxedoRow);
                public override Type PropertyType => _type;
                public override object GetValue(object? component)
                    => ((TuxedoRow)component!).TryGetValue(_index, out var val) ? (val ?? DBNull.Value): DBNull.Value;
                public override void SetValue(object? component, object? value)
                    => ((TuxedoRow)component!).SetValue(_index, value is DBNull ? null : value);
            }
        }
    }
}



