using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.Contrib;

namespace Tuxedo.Specifications
{
    public class SpecificationEvaluator<T> where T : class
    {
        public static async Task<IEnumerable<T>> GetQueryAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var sql = BuildSqlQuery(specification);
            var parameters = ExtractParameters(specification);
            
            return await connection.QueryAsync<T>(sql, parameters, transaction).ConfigureAwait(false);
        }

        public static async Task<T?> GetFirstOrDefaultAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var sql = BuildSqlQuery(specification, limit: 1);
            var parameters = ExtractParameters(specification);
            
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters, transaction).ConfigureAwait(false);
        }

        public static async Task<int> GetCountAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var sql = BuildCountQuery(specification);
            var parameters = ExtractParameters(specification);
            
            return await connection.ExecuteScalarAsync<int>(sql, parameters, transaction).ConfigureAwait(false);
        }

        private static string BuildSqlQuery(ISpecification<T> specification, int? limit = null)
        {
            var tableName = GetTableName();
            var sql = new StringBuilder($"SELECT * FROM {tableName}");

            // WHERE clause
            var whereClause = BuildWhereClause(specification);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql.Append($" WHERE {whereClause}");
            }

            // GROUP BY
            if (specification.GroupBy != null)
            {
                var groupByColumn = GetPropertyName(specification.GroupBy);
                sql.Append($" GROUP BY {groupByColumn}");
            }

            // ORDER BY
            if (specification.OrderBy != null)
            {
                var orderByColumn = GetPropertyName(specification.OrderBy);
                sql.Append($" ORDER BY {orderByColumn} ASC");
            }
            else if (specification.OrderByDescending != null)
            {
                var orderByColumn = GetPropertyName(specification.OrderByDescending);
                sql.Append($" ORDER BY {orderByColumn} DESC");
            }

            // LIMIT and OFFSET
            if (limit.HasValue)
            {
                sql.Append($" LIMIT {limit.Value}");
            }
            else if (specification.IsPagingEnabled)
            {
                sql.Append($" LIMIT {specification.Take} OFFSET {specification.Skip}");
            }

            return sql.ToString();
        }

        private static string BuildCountQuery(ISpecification<T> specification)
        {
            var tableName = GetTableName();
            var sql = new StringBuilder($"SELECT COUNT(*) FROM {tableName}");

            var whereClause = BuildWhereClause(specification);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql.Append($" WHERE {whereClause}");
            }

            return sql.ToString();
        }

        private static string BuildWhereClause(ISpecification<T> specification)
        {
            // This is a simplified implementation
            // In a production scenario, you'd need a proper expression visitor
            // to convert the Expression<Func<T, bool>> to SQL
            if (specification.Criteria != null)
            {
                var visitor = new SqlExpressionVisitor();
                return visitor.Visit(specification.Criteria);
            }

            return string.Empty;
        }

        private static object? ExtractParameters(ISpecification<T> specification)
        {
            // Extract parameter values from the specification criteria
            // This would need to be implemented based on your expression parsing
            return null;
        }

        private static string GetTableName()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault() as TableAttribute;
            return tableAttr?.Name ?? type.Name + "s";
        }

        private static string GetPropertyName(System.Linq.Expressions.Expression<System.Func<T, object>> expression)
        {
            var memberExpression = expression.Body as System.Linq.Expressions.MemberExpression;
            if (memberExpression == null && expression.Body is System.Linq.Expressions.UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as System.Linq.Expressions.MemberExpression;
            }

            return memberExpression?.Member.Name ?? string.Empty;
        }
    }

    internal class SqlExpressionVisitor
    {
        public string Visit<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> expression)
        {
            // Simplified implementation - returns a placeholder
            // In production, implement a full expression visitor
            return "1=1";
        }
    }
}