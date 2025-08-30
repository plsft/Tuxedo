using System.Reflection;
using Bowtie.Attributes;
using Bowtie.Models;
using Tuxedo.Contrib;

namespace Bowtie.Analysis
{
    public class ModelAnalyzer
    {
        public List<TableModel> AnalyzeAssembly(Assembly assembly, string? defaultSchema = null)
        {
            var tableModels = new List<TableModel>();
            
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && HasTableAttribute(t))
                .ToList();
            
            foreach (var type in types)
            {
                var tableModel = AnalyzeType(type, defaultSchema);
                if (tableModel != null)
                {
                    tableModels.Add(tableModel);
                }
            }
            
            return tableModels;
        }
        
        public List<TableModel> AnalyzeTypes(IEnumerable<Type> types, string? defaultSchema = null)
        {
            var tableModels = new List<TableModel>();
            
            foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract))
            {
                var tableModel = AnalyzeType(type, defaultSchema);
                if (tableModel != null)
                {
                    tableModels.Add(tableModel);
                }
            }
            
            return tableModels;
        }

        public TableModel? AnalyzeType(Type type, string? defaultSchema = null)
        {
            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            var tableName = tableAttribute?.Name ?? type.Name;
            
            // Extract schema from table name if present (e.g., "schema.table")
            string? schema = defaultSchema;
            if (tableName.Contains('.'))
            {
                var parts = tableName.Split('.');
                schema = parts[0];
                tableName = parts[1];
            }
            
            var tableModel = new TableModel
            {
                Name = tableName,
                Schema = schema,
                ModelType = type
            };
            
            // Analyze properties for columns
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => !p.GetCustomAttributes<ComputedAttribute>().Any())
                .ToList();
            
            foreach (var property in properties)
            {
                var column = AnalyzeProperty(property);
                if (column != null)
                {
                    tableModel.Columns.Add(column);
                }
            }
            
            // Analyze indexes
            tableModel.Indexes.AddRange(AnalyzeIndexes(properties, tableName));
            
            // Analyze constraints
            tableModel.Constraints.AddRange(AnalyzeConstraints(properties, tableName));
            
            return tableModel;
        }

        private ColumnModel? AnalyzeProperty(PropertyInfo property)
        {
            var writeAttribute = property.GetCustomAttribute<WriteAttribute>();
            if (writeAttribute != null && !writeAttribute.Write)
            {
                return null;
            }
            
            var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
            var keyAttribute = property.GetCustomAttribute<KeyAttribute>();
            var explicitKeyAttribute = property.GetCustomAttribute<ExplicitKeyAttribute>();
            var primaryKeyAttribute = property.GetCustomAttribute<PrimaryKeyAttribute>();
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            
            var column = new ColumnModel
            {
                Name = columnAttribute?.Name ?? property.Name,
                PropertyInfo = property,
                PropertyType = property.PropertyType,
                IsNullable = IsNullableType(property.PropertyType),
                IsPrimaryKey = keyAttribute != null || explicitKeyAttribute != null || primaryKeyAttribute != null,
                IsIdentity = (keyAttribute != null || primaryKeyAttribute?.IsIdentity == true) && IsIntegerType(property.PropertyType)
            };
            
            // Set column attributes from ColumnAttribute
            if (columnAttribute != null)
            {
                column.DataType = columnAttribute.TypeName ?? string.Empty;
                column.MaxLength = columnAttribute.MaxLength > 0 ? columnAttribute.MaxLength : null;
                column.Precision = columnAttribute.Precision > 0 ? columnAttribute.Precision : null;
                column.Scale = columnAttribute.Scale >= 0 ? columnAttribute.Scale : null;
                column.IsNullable = columnAttribute.IsNullable;
                column.Collation = columnAttribute.Collation;
            }
            
            // Set default value
            if (defaultValueAttribute != null)
            {
                column.DefaultValue = defaultValueAttribute.Value;
                column.IsDefaultRawSql = defaultValueAttribute.IsRawSql;
            }
            
            return column;
        }

        private List<IndexModel> AnalyzeIndexes(List<PropertyInfo> properties, string tableName)
        {
            var indexes = new Dictionary<string, IndexModel>();
            
            foreach (var property in properties)
            {
                var indexAttributes = property.GetCustomAttributes<IndexAttribute>().ToList();
                var uniqueAttribute = property.GetCustomAttribute<UniqueAttribute>();
                
                // Handle IndexAttribute
                foreach (var indexAttr in indexAttributes)
                {
                    var indexName = indexAttr.Name ?? 
                                   (!string.IsNullOrEmpty(indexAttr.Group) 
                                       ? $"IX_{tableName}_{indexAttr.Group}" 
                                       : $"IX_{tableName}_{property.Name}");
                    
                    if (!indexes.TryGetValue(indexName, out var index))
                    {
                        index = new IndexModel
                        {
                            Name = indexName,
                            IsUnique = indexAttr.IsUnique,
                            IndexType = indexAttr.IndexType,
                            IncludeColumns = indexAttr.Include,
                            WhereClause = indexAttr.Where,
                            IsClustered = indexAttr.IndexType == IndexType.Clustered
                        };
                        indexes[indexName] = index;
                    }
                    
                    index.Columns.Add(new IndexColumnModel
                    {
                        ColumnName = property.Name,
                        Order = indexAttr.Order,
                        IsDescending = indexAttr.IsDescending
                    });
                }
                
                // Handle UniqueAttribute
                if (uniqueAttribute != null)
                {
                    var indexName = uniqueAttribute.Name ?? 
                                   (!string.IsNullOrEmpty(uniqueAttribute.Group) 
                                       ? $"UQ_{tableName}_{uniqueAttribute.Group}" 
                                       : $"UQ_{tableName}_{property.Name}");
                    
                    if (!indexes.TryGetValue(indexName, out var index))
                    {
                        index = new IndexModel
                        {
                            Name = indexName,
                            IsUnique = true,
                            IndexType = IndexType.BTree
                        };
                        indexes[indexName] = index;
                    }
                    
                    index.Columns.Add(new IndexColumnModel
                    {
                        ColumnName = property.Name,
                        Order = uniqueAttribute.Order,
                        IsDescending = false
                    });
                }
            }
            
            // Sort index columns by order
            foreach (var index in indexes.Values)
            {
                index.Columns = index.Columns.OrderBy(c => c.Order).ToList();
            }
            
            return indexes.Values.ToList();
        }

        private List<ConstraintModel> AnalyzeConstraints(List<PropertyInfo> properties, string tableName)
        {
            var constraints = new List<ConstraintModel>();
            
            // Analyze primary key constraint
            var pkColumns = properties
                .Where(p => p.GetCustomAttribute<KeyAttribute>() != null || 
                           p.GetCustomAttribute<ExplicitKeyAttribute>() != null ||
                           p.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<PrimaryKeyAttribute>()?.Order ?? 0)
                .ToList();
            
            if (pkColumns.Any())
            {
                constraints.Add(new ConstraintModel
                {
                    Name = $"PK_{tableName}",
                    Type = ConstraintType.PrimaryKey,
                    Columns = pkColumns.Select(p => p.Name).ToList()
                });
            }
            
            // Analyze other constraints
            foreach (var property in properties)
            {
                var foreignKeyAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                if (foreignKeyAttr != null)
                {
                    constraints.Add(new ConstraintModel
                    {
                        Name = foreignKeyAttr.Name ?? $"FK_{tableName}_{property.Name}",
                        Type = ConstraintType.ForeignKey,
                        Columns = new List<string> { property.Name },
                        ReferencedTable = foreignKeyAttr.ReferencedTable,
                        ReferencedColumn = foreignKeyAttr.ReferencedColumn ?? "Id",
                        OnDelete = foreignKeyAttr.OnDelete,
                        OnUpdate = foreignKeyAttr.OnUpdate
                    });
                }
                
                var checkConstraintAttr = property.GetCustomAttribute<CheckConstraintAttribute>();
                if (checkConstraintAttr != null)
                {
                    constraints.Add(new ConstraintModel
                    {
                        Name = checkConstraintAttr.Name ?? $"CK_{tableName}_{property.Name}",
                        Type = ConstraintType.Check,
                        Columns = new List<string> { property.Name },
                        CheckExpression = checkConstraintAttr.Expression
                    });
                }
            }
            
            return constraints;
        }

        private bool HasTableAttribute(Type type)
        {
            return type.GetCustomAttribute<TableAttribute>() != null;
        }

        private bool IsNullableType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private bool IsIntegerType(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType == typeof(int) || 
                   underlyingType == typeof(long) || 
                   underlyingType == typeof(short) || 
                   underlyingType == typeof(byte);
        }
    }
}