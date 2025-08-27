using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tuxedo.QueryBuilder
{
    public interface IQueryBuilder<T> where T : class
    {
        // SELECT
        IQueryBuilder<T> Select(params string[] columns);
        IQueryBuilder<T> Select(Expression<Func<T, object>> selector);
        IQueryBuilder<T> SelectAll();
        
        // FROM
        IQueryBuilder<T> From(string tableName);
        
        // JOIN
        IQueryBuilder<T> InnerJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class;
        IQueryBuilder<T> LeftJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class;
        IQueryBuilder<T> RightJoin<TJoin>(Expression<Func<T, TJoin, bool>> condition) where TJoin : class;
        
        // WHERE
        IQueryBuilder<T> Where(Expression<Func<T, bool>> predicate);
        IQueryBuilder<T> Where(string condition, object? parameters = null);
        IQueryBuilder<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);
        IQueryBuilder<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values);
        IQueryBuilder<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue start, TValue end);
        IQueryBuilder<T> WhereNull(Expression<Func<T, object>> selector);
        IQueryBuilder<T> WhereNotNull(Expression<Func<T, object>> selector);
        
        // Logical operators
        IQueryBuilder<T> And(Expression<Func<T, bool>> predicate);
        IQueryBuilder<T> Or(Expression<Func<T, bool>> predicate);
        IQueryBuilder<T> Not(Expression<Func<T, bool>> predicate);
        
        // GROUP BY
        IQueryBuilder<T> GroupBy(params Expression<Func<T, object>>[] selectors);
        IQueryBuilder<T> Having(Expression<Func<T, bool>> predicate);
        
        // ORDER BY
        IQueryBuilder<T> OrderBy(Expression<Func<T, object>> selector);
        IQueryBuilder<T> OrderByDescending(Expression<Func<T, object>> selector);
        IQueryBuilder<T> ThenBy(Expression<Func<T, object>> selector);
        IQueryBuilder<T> ThenByDescending(Expression<Func<T, object>> selector);
        
        // Pagination
        IQueryBuilder<T> Skip(int count);
        IQueryBuilder<T> Take(int count);
        IQueryBuilder<T> Page(int pageIndex, int pageSize);
        
        // Aggregations
        IQueryBuilder<T> Count(Expression<Func<T, object>>? selector = null);
        IQueryBuilder<T> Sum(Expression<Func<T, object>> selector);
        IQueryBuilder<T> Average(Expression<Func<T, object>> selector);
        IQueryBuilder<T> Min(Expression<Func<T, object>> selector);
        IQueryBuilder<T> Max(Expression<Func<T, object>> selector);
        
        // Build and Execute
        string BuildSql();
        object? GetParameters();
        
        Task<IEnumerable<T>> ToListAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<T?> FirstOrDefaultAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<T> SingleAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<int> CountAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        
        // Raw SQL
        IQueryBuilder<T> Raw(string sql, object? parameters = null);
    }
}