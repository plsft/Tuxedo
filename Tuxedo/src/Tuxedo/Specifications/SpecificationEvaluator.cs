using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.Contrib;
using Tuxedo.DependencyInjection;
using Tuxedo.Expressions;

namespace Tuxedo.Specifications
{
    public class SpecificationEvaluator<T> where T : class
    {
        private static readonly ExpressionToSqlConverter _expressionConverter = new();

        public static async Task<IEnumerable<T>> GetQueryAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var (sql, parameters) = BuildSqlQuery(specification, GetDialect(connection));
            return await connection.QueryAsync<T>(sql, parameters, transaction).ConfigureAwait(false);
        }

        public static async Task<T?> GetFirstOrDefaultAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var (sql, parameters) = BuildSqlQuery(specification, GetDialect(connection), limit: 1);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters, transaction).ConfigureAwait(false);
        }

        public static async Task<int> GetCountAsync(
            IDbConnection connection,
            ISpecification<T> specification,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var (sql, parameters) = BuildCountQuery(specification);
            return await connection.ExecuteScalarAsync<int>(sql, parameters, transaction).ConfigureAwait(false);
        }

        private static (string sql, object? parameters) BuildSqlQuery(ISpecification<T> specification, TuxedoDialect dialect, int? limit = null)
        {
            var tableName = GetTableName();
            var sql = new StringBuilder($"SELECT * FROM {tableName}");

            // WHERE clause
            object? parameters = null;
            var whereClause = BuildWhereClause(specification, out parameters);
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
            if (limit.HasValue || specification.IsPagingEnabled)
            {
                var skipCount = specification.Skip;
                var takeCount = limit ?? specification.Take;
                
                var paginationSql = dialect switch
                {
                    TuxedoDialect.SqlServer => $" OFFSET {skipCount} ROWS FETCH NEXT {takeCount} ROWS ONLY",
                    _ => $" LIMIT {takeCount} OFFSET {skipCount}"
                };
                
                sql.Append(paginationSql);
            }

            return (sql.ToString(), parameters);
        }

        private static (string sql, object? parameters) BuildCountQuery(ISpecification<T> specification)
        {
            var tableName = GetTableName();
            var sql = new StringBuilder($"SELECT COUNT(*) FROM {tableName}");

            object? parameters = null;
            var whereClause = BuildWhereClause(specification, out parameters);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql.Append($" WHERE {whereClause}");
            }

            return (sql.ToString(), parameters);
        }

        private static string BuildWhereClause(ISpecification<T> specification, out object? parameters)
        {
            parameters = null;
            
            if (specification.Criteria != null)
            {
                var whereClause = _expressionConverter.Convert(specification.Criteria);
                parameters = _expressionConverter.GetParameters();
                return whereClause;
            }

            return string.Empty;
        }

        private static TuxedoDialect GetDialect(IDbConnection connection)
        {
            var typeName = connection.GetType().Name.ToLowerInvariant();
            
            if (typeName.Contains("sqlite"))
                return TuxedoDialect.Sqlite;
            if (typeName.Contains("npgsql") || typeName.Contains("postgres"))
                return TuxedoDialect.Postgres;
            if (typeName.Contains("mysql"))
                return TuxedoDialect.MySql;
            
            return TuxedoDialect.SqlServer;
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

}