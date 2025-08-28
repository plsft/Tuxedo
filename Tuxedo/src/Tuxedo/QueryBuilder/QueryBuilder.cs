using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.DependencyInjection;
using Tuxedo.Contrib;
using Tuxedo.Expressions;

namespace Tuxedo.QueryBuilder
{
    public class QueryBuilder<T> : IQueryBuilder<T> where T : class
    {
        private readonly StringBuilder _selectClause = new();
        private readonly StringBuilder _fromClause = new();
        private readonly StringBuilder _whereClause = new();
        private readonly StringBuilder _joinClause = new();
        private readonly StringBuilder _groupByClause = new();
        private readonly StringBuilder _havingClause = new();
        private readonly StringBuilder _orderByClause = new();
        private string? _skipTakeClause;
        private readonly Dictionary<string, object> _parameters = new();
        private readonly TuxedoDialect _dialect;
        private int _parameterCounter = 0;
        private readonly ExpressionToSqlConverter _expressionConverter = new();

        public QueryBuilder(TuxedoDialect dialect = TuxedoDialect.SqlServer)
        {
            _dialect = dialect;
            var tableName = GetTableName();
            _fromClause.Append($"FROM {tableName}");
        }

        public IQueryBuilder<T> Select(params string[] columns)
        {
            _selectClause.Clear();
            _selectClause.Append("SELECT ");
            _selectClause.Append(string.Join(", ", columns));
            return this;
        }

        public IQueryBuilder<T> Select(Expression<Func<T, object>> selector)
        {
            var columns = GetPropertyNames(selector);
            return Select(columns.ToArray());
        }

        public IQueryBuilder<T> SelectAll()
        {
            _selectClause.Clear();
            _selectClause.Append("SELECT *");
            return this;
        }

        public IQueryBuilder<T> From(string tableName)
        {
            _fromClause.Clear();
            _fromClause.Append($"FROM {tableName}");
            return this;
        }

        public IQueryBuilder<T> InnerJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class
        {
            var joinTable = GetTableName<TJoin>();
            var joinCondition = ExpressionToJoinSql<TJoin>(condition);
            _joinClause.AppendLine($"INNER JOIN {joinTable} ON {joinCondition}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> LeftJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class
        {
            var joinTable = GetTableName<TJoin>();
            var joinCondition = ExpressionToJoinSql<TJoin>(condition);
            _joinClause.AppendLine($"LEFT JOIN {joinTable} ON {joinCondition}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> RightJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class
        {
            var joinTable = GetTableName<TJoin>();
            var joinCondition = ExpressionToJoinSql<TJoin>(condition);
            _joinClause.AppendLine($"RIGHT JOIN {joinTable} ON {joinCondition}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
        {
            var sql = ExpressionToSql(predicate);
            AppendWhereCondition(sql);
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> Where(string condition, object? parameters = null)
        {
            AppendWhereCondition(condition);
            if (parameters != null)
            {
                AddParameters(parameters);
            }
            return this;
        }

        public IQueryBuilder<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
        {
            var columnName = GetPropertyName(selector);
            var paramNames = new List<string>();
            
            foreach (var value in values)
            {
                var paramName = GetNextParameterName();
                paramNames.Add($"@{paramName}");
                _parameters[paramName] = value!;
            }
            
            var condition = $"{columnName} IN ({string.Join(", ", paramNames)})";
            AppendWhereCondition(condition);
            return this;
        }

        public IQueryBuilder<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
        {
            var columnName = GetPropertyName(selector);
            var paramNames = new List<string>();
            
            foreach (var value in values)
            {
                var paramName = GetNextParameterName();
                paramNames.Add($"@{paramName}");
                _parameters[paramName] = value!;
            }
            
            var condition = $"{columnName} NOT IN ({string.Join(", ", paramNames)})";
            AppendWhereCondition(condition);
            return this;
        }

        public IQueryBuilder<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue start, TValue end)
        {
            var columnName = GetPropertyName(selector);
            var startParam = GetNextParameterName();
            var endParam = GetNextParameterName();
            
            _parameters[startParam] = start!;
            _parameters[endParam] = end!;
            
            var condition = $"{columnName} BETWEEN @{startParam} AND @{endParam}";
            AppendWhereCondition(condition);
            return this;
        }

        public IQueryBuilder<T> WhereNull(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            AppendWhereCondition($"{columnName} IS NULL");
            return this;
        }

        public IQueryBuilder<T> WhereNotNull(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            AppendWhereCondition($"{columnName} IS NOT NULL");
            return this;
        }

        public IQueryBuilder<T> And(Expression<Func<T, bool>> predicate)
        {
            var sql = ExpressionToSql(predicate);
            _whereClause.Append($" AND {sql}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> Or(Expression<Func<T, bool>> predicate)
        {
            var sql = ExpressionToSql(predicate);
            _whereClause.Append($" OR {sql}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> Not(Expression<Func<T, bool>> predicate)
        {
            var sql = ExpressionToSql(predicate);
            AppendWhereCondition($"NOT ({sql})");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> GroupBy(params Expression<Func<T, object>>[] selectors)
        {
            var columns = selectors.SelectMany(GetPropertyNames);
            _groupByClause.Clear();
            _groupByClause.Append($"GROUP BY {string.Join(", ", columns)}");
            return this;
        }

        public IQueryBuilder<T> Having(Expression<Func<T, bool>> predicate)
        {
            var sql = ExpressionToSql(predicate);
            _havingClause.Clear();
            _havingClause.Append($"HAVING {sql}");
            MergeExpressionParameters();
            return this;
        }

        public IQueryBuilder<T> OrderBy(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            _orderByClause.Clear();
            _orderByClause.Append($"ORDER BY {columnName} ASC");
            return this;
        }

        public IQueryBuilder<T> OrderByDescending(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            _orderByClause.Clear();
            _orderByClause.Append($"ORDER BY {columnName} DESC");
            return this;
        }

        public IQueryBuilder<T> ThenBy(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            _orderByClause.Append($", {columnName} ASC");
            return this;
        }

        public IQueryBuilder<T> ThenByDescending(Expression<Func<T, object>> selector)
        {
            var columnName = GetPropertyName(selector);
            _orderByClause.Append($", {columnName} DESC");
            return this;
        }

        public IQueryBuilder<T> Skip(int count)
        {
            UpdatePagination(count, null);
            return this;
        }

        public IQueryBuilder<T> Take(int count)
        {
            UpdatePagination(null, count);
            return this;
        }

        public IQueryBuilder<T> Page(int pageIndex, int pageSize)
        {
            UpdatePagination(pageIndex * pageSize, pageSize);
            return this;
        }

        public IQueryBuilder<T> Count(Expression<Func<T, object>>? selector = null)
        {
            _selectClause.Clear();
            _selectClause.Append(selector != null 
                ? $"SELECT COUNT({GetPropertyName(selector)})" 
                : "SELECT COUNT(*)");
            return this;
        }

        public IQueryBuilder<T> Sum(Expression<Func<T, object>> selector)
        {
            _selectClause.Clear();
            _selectClause.Append($"SELECT SUM({GetPropertyName(selector)})");
            return this;
        }

        public IQueryBuilder<T> Average(Expression<Func<T, object>> selector)
        {
            _selectClause.Clear();
            _selectClause.Append($"SELECT AVG({GetPropertyName(selector)})");
            return this;
        }

        public IQueryBuilder<T> Min(Expression<Func<T, object>> selector)
        {
            _selectClause.Clear();
            _selectClause.Append($"SELECT MIN({GetPropertyName(selector)})");
            return this;
        }

        public IQueryBuilder<T> Max(Expression<Func<T, object>> selector)
        {
            _selectClause.Clear();
            _selectClause.Append($"SELECT MAX({GetPropertyName(selector)})");
            return this;
        }

        public IQueryBuilder<T> Raw(string sql, object? parameters = null)
        {
            _selectClause.Clear();
            _selectClause.Append(sql);
            if (parameters != null)
            {
                AddParameters(parameters);
            }
            return this;
        }

        public string BuildSql()
        {
            var sql = new StringBuilder();
            
            // SELECT
            if (_selectClause.Length == 0)
            {
                sql.Append("SELECT * ");
            }
            else
            {
                sql.Append(_selectClause).Append(" ");
            }
            
            // FROM
            sql.Append(_fromClause).Append(" ");
            
            // JOIN
            if (_joinClause.Length > 0)
            {
                sql.Append(_joinClause);
            }
            
            // WHERE
            if (_whereClause.Length > 0)
            {
                sql.Append(_whereClause).Append(" ");
            }
            
            // GROUP BY
            if (_groupByClause.Length > 0)
            {
                sql.Append(_groupByClause).Append(" ");
            }
            
            // HAVING
            if (_havingClause.Length > 0)
            {
                sql.Append(_havingClause).Append(" ");
            }
            
            // ORDER BY
            if (_orderByClause.Length > 0)
            {
                sql.Append(_orderByClause).Append(" ");
            }
            
            // LIMIT/OFFSET (pagination)
            if (!string.IsNullOrEmpty(_skipTakeClause))
            {
                sql.Append(_skipTakeClause);
            }
            
            return sql.ToString().Trim();
        }

        public object? GetParameters()
        {
            return _parameters.Count > 0 ? _parameters : null;
        }

        public async Task<IEnumerable<T>> ToListAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var sql = BuildSql();
            return await connection.QueryAsync<T>(sql, GetParameters(), transaction).ConfigureAwait(false);
        }

        public async Task<T?> FirstOrDefaultAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Take(1);
            var sql = BuildSql();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, GetParameters(), transaction).ConfigureAwait(false);
        }

        public async Task<T> SingleAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var sql = BuildSql();
            return await connection.QuerySingleAsync<T>(sql, GetParameters(), transaction).ConfigureAwait(false);
        }

        public async Task<int> CountAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Count();
            var sql = BuildSql();
            return await connection.ExecuteScalarAsync<int>(sql, GetParameters(), transaction).ConfigureAwait(false);
        }

        public async Task<bool> AnyAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var count = await CountAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            return count > 0;
        }

        private void AppendWhereCondition(string condition)
        {
            if (_whereClause.Length == 0)
            {
                _whereClause.Append($"WHERE {condition}");
            }
            else
            {
                _whereClause.Append($" AND {condition}");
            }
        }

        private void UpdatePagination(int? skip, int? take)
        {
            _skipTakeClause = _dialect switch
            {
                TuxedoDialect.SqlServer => BuildSqlServerPagination(skip, take),
                TuxedoDialect.Postgres => BuildPostgresPagination(skip, take),
                TuxedoDialect.MySql => BuildMySqlPagination(skip, take),
                TuxedoDialect.Sqlite => BuildSqlitePagination(skip, take),
                _ => BuildStandardPagination(skip, take)
            };
        }

        private string BuildSqlServerPagination(int? skip, int? take)
        {
            if (_orderByClause.Length == 0)
            {
                // SQL Server requires ORDER BY for OFFSET/FETCH
                OrderBy(x => typeof(T).GetProperties()[0]);
            }
            
            var pagination = "";
            if (skip.HasValue)
            {
                pagination += $"OFFSET {skip.Value} ROWS ";
            }
            if (take.HasValue)
            {
                pagination += $"FETCH NEXT {take.Value} ROWS ONLY";
            }
            return pagination;
        }

        private string BuildPostgresPagination(int? skip, int? take)
        {
            return BuildStandardPagination(skip, take);
        }

        private string BuildMySqlPagination(int? skip, int? take)
        {
            return BuildStandardPagination(skip, take);
        }

        private string BuildSqlitePagination(int? skip, int? take)
        {
            return BuildStandardPagination(skip, take);
        }

        private string BuildStandardPagination(int? skip, int? take)
        {
            var pagination = "";
            if (take.HasValue)
            {
                pagination += $"LIMIT {take.Value} ";
            }
            if (skip.HasValue)
            {
                pagination += $"OFFSET {skip.Value}";
            }
            return pagination;
        }

        private string GetTableName()
        {
            return GetTableName<T>();
        }

        private string GetTableName<TEntity>()
        {
            var type = typeof(TEntity);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name + "s";
        }

        private string GetPropertyName<TValue>(Expression<Func<T, TValue>> selector)
        {
            return GetPropertyNames(selector).First();
        }

        private IEnumerable<string> GetPropertyNames(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            if (memberExpression == null && expression is LambdaExpression lambda)
            {
                memberExpression = lambda.Body as MemberExpression;
            }
            
            if (memberExpression != null)
            {
                return new[] { memberExpression.Member.Name };
            }
            
            // Handle new expressions for anonymous types
            if (expression is NewExpression newExpression)
            {
                return newExpression.Members?.Select(m => m.Name) ?? Enumerable.Empty<string>();
            }
            
            return Enumerable.Empty<string>();
        }

        private string ExpressionToSql(Expression expression)
        {
            if (expression is LambdaExpression lambda)
            {
                return _expressionConverter.Convert((Expression<Func<T, bool>>)lambda);
            }
            return "1=1";
        }

        private string ExpressionToJoinSql<TJoin>(Expression expression) where TJoin : class
        {
            // For join expressions, we need to handle two-parameter lambdas
            if (expression is LambdaExpression lambda && lambda.Body is BinaryExpression binary)
            {
                // Simple implementation for equality joins
                var left = GetMemberName(binary.Left);
                var right = GetMemberName(binary.Right);
                
                if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
                {
                    return $"{left} = {right}";
                }
            }
            return "1=1";
        }

        private string GetMemberName(Expression expression)
        {
            if (expression is MemberExpression member)
            {
                // Check if it's accessing a parameter
                if (member.Expression is ParameterExpression param)
                {
                    // For joins, we might need table aliases
                    var tableName = param.Type == typeof(T) ? GetTableName<T>() : GetTableName(param.Type);
                    return $"{tableName}.{member.Member.Name}";
                }
                return member.Member.Name;
            }
            return string.Empty;
        }

        private string GetTableName(Type type)
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name + "s";
        }

        private void MergeExpressionParameters()
        {
            var exprParams = _expressionConverter.GetParameters();
            foreach (var kvp in exprParams)
            {
                _parameters[kvp.Key] = kvp.Value;
            }
        }

        private string GetNextParameterName()
        {
            return $"p{++_parameterCounter}";
        }

        private void AddParameters(object parameters)
        {
            if (parameters is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    _parameters[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                var props = parameters.GetType().GetProperties();
                foreach (var prop in props)
                {
                    _parameters[prop.Name] = prop.GetValue(parameters)!;
                }
            }
        }
    }

    public static class QueryBuilderExtensions
    {
        public static IQueryBuilder<T> Query<T>() where T : class
        {
            return new QueryBuilder<T>();
        }
        
        public static IQueryBuilder<T> Query<T>(this IDbConnection connection) where T : class
        {
            return new QueryBuilder<T>();
        }
    }
}