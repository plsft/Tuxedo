using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.Contrib;

namespace Tuxedo.Patterns
{
    public class DapperRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly IDbConnection Connection;
        protected readonly IDbTransaction? Transaction;
        private readonly string _tableName;

        public DapperRepository(IDbConnection connection, IDbTransaction? transaction = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Transaction = transaction;
            _tableName = GetTableName();
        }

        public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return await Connection.GetAsync<TEntity>(id, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Connection.GetAllAsync<TEntity>(Transaction).ConfigureAwait(false);
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var sql = BuildSelectQuery(predicate);
            return await Connection.QueryAsync<TEntity>(sql, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var sql = BuildSelectQuery(predicate);
            return await Connection.QuerySingleOrDefaultAsync<TEntity>(sql, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT EXISTS({BuildSelectQuery(predicate, "1")})";
            return await Connection.ExecuteScalarAsync<bool>(sql, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var sql = predicate != null 
                ? $"SELECT COUNT(*) FROM {_tableName} WHERE {GetWhereClause(predicate)}"
                : $"SELECT COUNT(*) FROM {_tableName}";
            
            return await Connection.ExecuteScalarAsync<int>(sql, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var id = await Connection.InsertAsync(entity, Transaction).ConfigureAwait(false);
            SetIdProperty(entity, id);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                await AddAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }

        public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await Connection.UpdateAsync(entity, Transaction).ConfigureAwait(false);
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                await UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }

        public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await Connection.DeleteAsync(entity, Transaction).ConfigureAwait(false);
        }

        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                await RemoveAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }

        public virtual async Task RemoveByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (entity != null)
            {
                await RemoveAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }

        public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
            int pageIndex, 
            int pageSize, 
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            CancellationToken cancellationToken = default)
        {
            var totalCount = await CountAsync(predicate, cancellationToken).ConfigureAwait(false);
            
            var sql = BuildPagedQuery(pageIndex, pageSize, predicate, orderBy);
            var items = await Connection.QueryAsync<TEntity>(sql, Transaction).ConfigureAwait(false);
            
            return new PagedResult<TEntity>(items.ToList(), pageIndex, pageSize, totalCount);
        }

        protected virtual string GetTableName()
        {
            var type = typeof(TEntity);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                return tableAttr.Name;
            }

            return type.Name + "s"; // Simple pluralization
        }

        protected virtual string BuildSelectQuery(Expression<Func<TEntity, bool>>? predicate = null, string selectClause = "*")
        {
            var sql = $"SELECT {selectClause} FROM {_tableName}";
            
            if (predicate != null)
            {
                sql += $" WHERE {GetWhereClause(predicate)}";
            }
            
            return sql;
        }

        protected virtual string BuildPagedQuery(
            int pageIndex, 
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null)
        {
            var sql = BuildSelectQuery(predicate);
            
            // Add ORDER BY clause (required for pagination)
            if (orderBy == null)
            {
                // Default to ordering by the first property (usually ID)
                var firstProperty = typeof(TEntity).GetProperties().FirstOrDefault();
                if (firstProperty != null)
                {
                    sql += $" ORDER BY {firstProperty.Name}";
                }
            }
            
            // Add pagination
            sql += $" LIMIT {pageSize} OFFSET {pageIndex * pageSize}";
            
            return sql;
        }

        protected virtual string GetWhereClause(Expression<Func<TEntity, bool>> predicate)
        {
            // This is a simplified implementation
            // In a real-world scenario, you'd want a proper expression visitor
            return "1=1"; // Placeholder
        }

        protected virtual void SetIdProperty(TEntity entity, object id)
        {
            var idProperty = typeof(TEntity)
                .GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null || 
                                   p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
            
            if (idProperty != null && idProperty.CanWrite)
            {
                var convertedId = Convert.ChangeType(id, idProperty.PropertyType);
                idProperty.SetValue(entity, convertedId);
            }
        }
    }
}