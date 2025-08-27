using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.Patterns;

namespace Tuxedo.Pagination
{
    public static class PaginationExtensions
    {
        /// <summary>
        /// Execute a paginated query with automatic count
        /// </summary>
        public static async Task<PagedResult<T>> QueryPagedAsync<T>(
            this IDbConnection connection,
            string sql,
            int pageIndex,
            int pageSize,
            object? param = null,
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            // Build count query
            var countSql = BuildCountQuery(sql);
            
            // Execute count query
            var totalCount = await connection.ExecuteScalarAsync<int>(
                countSql, param, transaction, commandTimeout).ConfigureAwait(false);
            
            // Build paginated query
            var pagedSql = BuildPagedQuery(sql, pageIndex, pageSize);
            
            // Execute paginated query
            var items = await connection.QueryAsync<T>(
                pagedSql, param, transaction, commandTimeout).ConfigureAwait(false);
            
            return new PagedResult<T>(items.ToList(), pageIndex, pageSize, totalCount);
        }
        
        /// <summary>
        /// Execute a paginated query with separate count query
        /// </summary>
        public static async Task<PagedResult<T>> QueryPagedAsync<T>(
            this IDbConnection connection,
            string selectSql,
            string countSql,
            int pageIndex,
            int pageSize,
            object? param = null,
            IDbTransaction? transaction = null,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            // Execute count query
            var totalCount = await connection.ExecuteScalarAsync<int>(
                countSql, param, transaction, commandTimeout).ConfigureAwait(false);
            
            // Build paginated query
            var pagedSql = BuildPagedQuery(selectSql, pageIndex, pageSize);
            
            // Execute paginated query
            var items = await connection.QueryAsync<T>(
                pagedSql, param, transaction, commandTimeout).ConfigureAwait(false);
            
            return new PagedResult<T>(items.ToList(), pageIndex, pageSize, totalCount);
        }
        
        /// <summary>
        /// Get a page of all records from a table
        /// </summary>
        public static async Task<PagedResult<T>> GetPagedAsync<T>(
            this IDbConnection connection,
            int pageIndex,
            int pageSize,
            string? orderBy = null,
            IDbTransaction? transaction = null,
            int? commandTimeout = null) where T : class
        {
            var tableName = GetTableName<T>();
            
            // Count query
            var countSql = $"SELECT COUNT(*) FROM {tableName}";
            var totalCount = await connection.ExecuteScalarAsync<int>(
                countSql, null, transaction, commandTimeout).ConfigureAwait(false);
            
            // Select query with pagination
            var selectSql = $"SELECT * FROM {tableName}";
            if (!string.IsNullOrEmpty(orderBy))
            {
                selectSql += $" ORDER BY {orderBy}";
            }
            else
            {
                // Default ordering by first column (usually ID)
                selectSql += " ORDER BY 1";
            }
            
            selectSql = BuildPagedQuery(selectSql, pageIndex, pageSize);
            
            var items = await connection.QueryAsync<T>(
                selectSql, null, transaction, commandTimeout).ConfigureAwait(false);
            
            return new PagedResult<T>(items.ToList(), pageIndex, pageSize, totalCount);
        }
        
        /// <summary>
        /// Extension method to convert enumerable to paged result
        /// </summary>
        public static PagedResult<T> ToPagedResult<T>(
            this IEnumerable<T> source,
            int pageIndex,
            int pageSize) where T : class
        {
            var items = source.ToList();
            var totalCount = items.Count;
            
            var pagedItems = items
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();
            
            return new PagedResult<T>(pagedItems, pageIndex, pageSize, totalCount);
        }
        
        /// <summary>
        /// Async extension method to convert enumerable to paged result
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this Task<IEnumerable<T>> sourceTask,
            int pageIndex,
            int pageSize) where T : class
        {
            var source = await sourceTask.ConfigureAwait(false);
            return source.ToPagedResult(pageIndex, pageSize);
        }
        
        private static string BuildCountQuery(string sql)
        {
            // Simple implementation - wrap original query
            // In production, you'd want more sophisticated SQL parsing
            var cleanSql = sql.Trim();
            if (cleanSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return $"SELECT COUNT(*) FROM ({sql}) AS CountQuery";
            }
            return sql;
        }
        
        private static string BuildPagedQuery(string sql, int pageIndex, int pageSize)
        {
            var offset = pageIndex * pageSize;
            
            // Check if ORDER BY exists
            if (!sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
            {
                // Add default ORDER BY for databases that require it
                sql += " ORDER BY 1";
            }
            
            // Add OFFSET and LIMIT/FETCH
            // This is a simplified version - in production, detect the SQL dialect
            return $"{sql} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        }
        
        private static string GetTableName<T>()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttributes(typeof(Contrib.TableAttribute), false)
                .FirstOrDefault() as Contrib.TableAttribute;
            return tableAttr?.Name ?? type.Name + "s";
        }
    }
    
    /// <summary>
    /// Options for pagination
    /// </summary>
    public class PaginationOptions
    {
        public int DefaultPageSize { get; set; } = 20;
        public int MaxPageSize { get; set; } = 100;
        public bool IncludeTotalCount { get; set; } = true;
        public bool ThrowOnInvalidPage { get; set; } = false;
    }
    
    /// <summary>
    /// Request model for pagination
    /// </summary>
    public class PageRequest
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public Dictionary<string, object>? Filters { get; set; }
        
        public PageRequest() { }
        
        public PageRequest(int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
        }
        
        public void Validate(PaginationOptions? options = null)
        {
            options ??= new PaginationOptions();
            
            if (PageIndex < 0)
                PageIndex = 0;
            
            if (PageSize <= 0)
                PageSize = options.DefaultPageSize;
            else if (PageSize > options.MaxPageSize)
                PageSize = options.MaxPageSize;
        }
    }
}