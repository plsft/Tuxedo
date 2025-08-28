using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Tuxedo.Expressions
{
    public class ExpressionToSqlConverter
    {
        private readonly Dictionary<ExpressionType, string> _operatorMap = new()
        {
            { ExpressionType.Equal, "=" },
            { ExpressionType.NotEqual, "<>" },
            { ExpressionType.LessThan, "<" },
            { ExpressionType.LessThanOrEqual, "<=" },
            { ExpressionType.GreaterThan, ">" },
            { ExpressionType.GreaterThanOrEqual, ">=" },
            { ExpressionType.AndAlso, "AND" },
            { ExpressionType.OrElse, "OR" },
            { ExpressionType.Add, "+" },
            { ExpressionType.Subtract, "-" },
            { ExpressionType.Multiply, "*" },
            { ExpressionType.Divide, "/" },
            { ExpressionType.Modulo, "%" },
            { ExpressionType.And, "&" },
            { ExpressionType.Or, "|" }
        };

        private readonly Dictionary<string, object> _parameters = new();
        private int _parameterIndex = 0;

        public string Convert<T>(Expression<Func<T, bool>> expression)
        {
            _parameters.Clear();
            _parameterIndex = 0;
            return Visit(expression.Body);
        }

        public Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>(_parameters);
        }

        private string Visit(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return VisitConstant((ConstantExpression)expression);
                case ExpressionType.MemberAccess:
                    return VisitMember((MemberExpression)expression);
                case ExpressionType.Not:
                    return VisitUnary((UnaryExpression)expression);
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.AndAlso:
                case ExpressionType.OrElse:
                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.Or:
                    return VisitBinary((BinaryExpression)expression);
                case ExpressionType.Call:
                    return VisitMethodCall((MethodCallExpression)expression);
                case ExpressionType.Lambda:
                    return Visit(((LambdaExpression)expression).Body);
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    return Visit(((UnaryExpression)expression).Operand);
                case ExpressionType.Parameter:
                    return string.Empty; // Parameter expressions don't need SQL representation
                default:
                    throw new NotSupportedException($"Expression type '{expression.NodeType}' is not supported");
            }
        }

        private string VisitBinary(BinaryExpression expression)
        {
            var left = Visit(expression.Left);
            var right = Visit(expression.Right);
            var op = _operatorMap[expression.NodeType];

            // Handle null comparisons
            if (expression.NodeType == ExpressionType.Equal && (IsNullConstant(expression.Right) || IsNullConstant(expression.Left)))
            {
                var nonNullSide = IsNullConstant(expression.Right) ? left : right;
                return $"{nonNullSide} IS NULL";
            }
            
            if (expression.NodeType == ExpressionType.NotEqual && (IsNullConstant(expression.Right) || IsNullConstant(expression.Left)))
            {
                var nonNullSide = IsNullConstant(expression.Right) ? left : right;
                return $"{nonNullSide} IS NOT NULL";
            }

            // Handle boolean member access
            if (expression.NodeType == ExpressionType.Equal && expression.Right is ConstantExpression { Value: true } && expression.Left.Type == typeof(bool))
            {
                return left;
            }
            
            if (expression.NodeType == ExpressionType.Equal && expression.Right is ConstantExpression { Value: false } && expression.Left.Type == typeof(bool))
            {
                return $"NOT {left}";
            }

            // Wrap OR conditions in parentheses when needed
            if (expression.NodeType == ExpressionType.OrElse)
            {
                return $"({left} {op} {right})";
            }

            return $"{left} {op} {right}";
        }

        private bool IsNullConstant(Expression expression)
        {
            return expression is ConstantExpression { Value: null };
        }

        private string VisitConstant(ConstantExpression expression)
        {
            if (expression.Value == null)
                return "NULL";

            // Handle boolean values
            if (expression.Value is bool boolValue)
                return boolValue ? "1" : "0";

            // Create parameter for value
            var paramName = $"@p{_parameterIndex++}";
            _parameters[paramName.Substring(1)] = expression.Value; // Remove @ for parameter name
            return paramName;
        }

        private string VisitMember(MemberExpression expression)
        {
            // Handle member access on parameter (e.g., p.Name)
            if (expression.Expression?.NodeType == ExpressionType.Parameter)
            {
                return expression.Member.Name;
            }

            // Handle member access on constant or closure (e.g., local variable)
            if (expression.Expression is ConstantExpression || expression.Expression is MemberExpression)
            {
                var value = GetValue(expression);
                if (value == null)
                    return "NULL";

                var paramName = $"@p{_parameterIndex++}";
                _parameters[paramName.Substring(1)] = value;
                return paramName;
            }

            // Handle static member access
            if (expression.Expression == null)
            {
                var value = GetValue(expression);
                if (value == null)
                    return "NULL";

                var paramName = $"@p{_parameterIndex++}";
                _parameters[paramName.Substring(1)] = value;
                return paramName;
            }

            throw new NotSupportedException($"Member expression type '{expression.Expression?.NodeType}' is not supported");
        }

        private string VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    var operand = Visit(expression.Operand);
                    // If operand is a boolean column, use NOT
                    if (expression.Operand.Type == typeof(bool))
                    {
                        return $"NOT ({operand})";
                    }
                    return $"NOT ({operand})";
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    return Visit(expression.Operand);
                default:
                    throw new NotSupportedException($"Unary operator '{expression.NodeType}' is not supported");
            }
        }

        private string VisitMethodCall(MethodCallExpression expression)
        {
            // Handle string methods
            if (expression.Method.DeclaringType == typeof(string))
            {
                switch (expression.Method.Name)
                {
                    case "Contains":
                        if (expression.Object != null)
                        {
                            var obj = Visit(expression.Object);
                            var arg = Visit(expression.Arguments[0]);
                            return $"{obj} LIKE '%' + {arg} + '%'";
                        }
                        break;
                    case "StartsWith":
                        if (expression.Object != null)
                        {
                            var obj = Visit(expression.Object);
                            var arg = Visit(expression.Arguments[0]);
                            return $"{obj} LIKE {arg} + '%'";
                        }
                        break;
                    case "EndsWith":
                        if (expression.Object != null)
                        {
                            var obj = Visit(expression.Object);
                            var arg = Visit(expression.Arguments[0]);
                            return $"{obj} LIKE '%' + {arg}";
                        }
                        break;
                    case "IsNullOrEmpty":
                        var param = Visit(expression.Arguments[0]);
                        return $"({param} IS NULL OR {param} = '')";
                    case "IsNullOrWhiteSpace":
                        var param2 = Visit(expression.Arguments[0]);
                        return $"({param2} IS NULL OR LTRIM(RTRIM({param2})) = '')";
                }
            }

            // Handle Enumerable.Contains (IN clause)
            if (expression.Method.Name == "Contains" && expression.Method.DeclaringType != null)
            {
                if (typeof(IEnumerable).IsAssignableFrom(expression.Method.DeclaringType) || 
                    expression.Method.DeclaringType.IsGenericType && 
                    expression.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var list = GetValue(expression.Arguments[0]);
                    var member = Visit(expression.Arguments[1]);
                    
                    if (list is IEnumerable enumerable)
                    {
                        var values = new List<string>();
                        foreach (var item in enumerable)
                        {
                            var paramName = $"@p{_parameterIndex++}";
                            _parameters[paramName.Substring(1)] = item;
                            values.Add(paramName);
                        }
                        
                        if (values.Count == 0)
                            return "1=0"; // No items, always false
                            
                        return $"{member} IN ({string.Join(", ", values)})";
                    }
                }

                // Handle List<T>.Contains
                if (expression.Object != null && expression.Arguments.Count == 1)
                {
                    var list = GetValue(expression.Object);
                    var member = Visit(expression.Arguments[0]);
                    
                    if (list is IEnumerable enumerable)
                    {
                        var values = new List<string>();
                        foreach (var item in enumerable)
                        {
                            var paramName = $"@p{_parameterIndex++}";
                            _parameters[paramName.Substring(1)] = item;
                            values.Add(paramName);
                        }
                        
                        if (values.Count == 0)
                            return "1=0";
                            
                        return $"{member} IN ({string.Join(", ", values)})";
                    }
                }
            }

            throw new NotSupportedException($"Method '{expression.Method.Name}' is not supported");
        }

        private object? GetValue(Expression expression)
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    return constant.Value;
                    
                case MemberExpression member:
                    var obj = member.Expression == null ? null : GetValue(member.Expression);
                    
                    if (member.Member is FieldInfo field)
                        return field.GetValue(obj);
                        
                    if (member.Member is PropertyInfo property)
                        return property.GetValue(obj);
                        
                    throw new NotSupportedException($"Member type '{member.Member.GetType()}' is not supported");
                    
                default:
                    throw new NotSupportedException($"Cannot get value from expression type '{expression.NodeType}'");
            }
        }
    }

    public static class OrderByExpressionConverter
    {
        public static string ConvertToSql<T>(Expression<Func<T, object>> expression, bool descending = false)
        {
            var member = GetMemberExpression(expression.Body);
            if (member == null)
                throw new NotSupportedException("Order by expression must be a member expression");

            var columnName = member.Member.Name;
            return descending ? $"{columnName} DESC" : $"{columnName} ASC";
        }

        public static string ConvertOrderByClause<T>(Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy) where T : class
        {
            if (orderBy == null)
                return string.Empty;

            // This is a simplified implementation
            // In production, you'd need to parse the actual expression tree
            var queryable = new List<T>().AsQueryable();
            var ordered = orderBy(queryable);
            
            // Get the ordering expression from the IOrderedQueryable
            if (ordered.Expression is MethodCallExpression methodCall)
            {
                return ExtractOrderByFromExpression(methodCall);
            }

            return string.Empty;
        }

        private static string ExtractOrderByFromExpression(MethodCallExpression expression)
        {
            var orderClauses = new List<string>();
            var current = expression;

            while (current != null)
            {
                if (current.Method.Name.StartsWith("OrderBy") || current.Method.Name.StartsWith("ThenBy"))
                {
                    var lambda = (current.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression;
                    if (lambda != null)
                    {
                        var member = GetMemberExpression(lambda.Body);
                        if (member != null)
                        {
                            var columnName = member.Member.Name;
                            var descending = current.Method.Name.Contains("Descending");
                            orderClauses.Insert(0, descending ? $"{columnName} DESC" : $"{columnName} ASC");
                        }
                    }
                }

                // Check if there's a previous method call in the chain
                current = current.Arguments[0] as MethodCallExpression;
            }

            return orderClauses.Count > 0 ? string.Join(", ", orderClauses) : string.Empty;
        }

        private static MemberExpression? GetMemberExpression(Expression expression)
        {
            switch (expression)
            {
                case MemberExpression member:
                    return member;
                case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                    return GetMemberExpression(unary.Operand);
                default:
                    return null;
            }
        }
    }
}