using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.Contrib;
using Tuxedo.DependencyInjection;
using Tuxedo.Expressions;

namespace Tuxedo.Patterns
{
    public class DapperRepository<TEntity> : IRepository<TEntity>, ITransactional where TEntity : class
    {
        protected readonly IDbConnection Connection;
        protected IDbTransaction? Transaction;
        private readonly string _tableName;
        private readonly TuxedoDialect _dialect;
        private readonly ExpressionToSqlConverter _expressionConverter;

        public DapperRepository(IDbConnection connection, IDbTransaction? transaction = null, TuxedoDialect? dialect = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Transaction = transaction;
            _tableName = GetTableName();
            _dialect = dialect ?? DetectDialect(connection);
            _expressionConverter = new ExpressionToSqlConverter();
        }

        private TuxedoDialect DetectDialect(IDbConnection connection)
        {
            var typeName = connection.GetType().Name.ToLowerInvariant();
            
            if (typeName.Contains("sqlite"))
                return TuxedoDialect.Sqlite;
            if (typeName.Contains("npgsql") || typeName.Contains("postgres"))
                return TuxedoDialect.Postgres;
            if (typeName.Contains("mysql"))
                return TuxedoDialect.MySql;
            
            return TuxedoDialect.SqlServer; // Default
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
            var parameters = _expressionConverter.GetParameters();
            return await Connection.QueryAsync<TEntity>(sql, parameters, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var sql = BuildSelectQuery(predicate);
            var parameters = _expressionConverter.GetParameters();
            return await Connection.QuerySingleOrDefaultAsync<TEntity>(sql, parameters, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var sql = _dialect switch
            {
                TuxedoDialect.SqlServer => $"SELECT CASE WHEN EXISTS({BuildSelectQuery(predicate, "1")}) THEN 1 ELSE 0 END",
                _ => $"SELECT EXISTS({BuildSelectQuery(predicate, "1")})"
            };
            var parameters = _expressionConverter.GetParameters();
            return await Connection.ExecuteScalarAsync<bool>(sql, parameters, Transaction).ConfigureAwait(false);
        }

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            string sql;
            object? parameters = null;
            
            if (predicate != null)
            {
                sql = $"SELECT COUNT(*) FROM {_tableName} WHERE {GetWhereClause(predicate)}";
                parameters = _expressionConverter.GetParameters();
            }
            else
            {
                sql = $"SELECT COUNT(*) FROM {_tableName}";
            }
            
            return await Connection.ExecuteScalarAsync<int>(sql, parameters, Transaction).ConfigureAwait(false);
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
            var parameters = predicate != null ? _expressionConverter.GetParameters() : null;
            var items = await Connection.QueryAsync<TEntity>(sql, parameters, Transaction).ConfigureAwait(false);
            
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
            var offset = pageIndex * pageSize;
            
            // Add ORDER BY clause (required for pagination)
            // For simplicity, we'll use the first property for ordering
            // In production, you might want to parse the orderBy expression
            var firstProperty = typeof(TEntity).GetProperties().FirstOrDefault();
            var orderByClause = firstProperty != null ? firstProperty.Name : "1";
            
            sql += $" ORDER BY {orderByClause}";
            
            // Add pagination based on dialect
            sql = _dialect switch
            {
                TuxedoDialect.SqlServer => sql + $" OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY",
                TuxedoDialect.Postgres => sql + $" LIMIT {pageSize} OFFSET {offset}",
                TuxedoDialect.MySql => sql + $" LIMIT {pageSize} OFFSET {offset}",
                TuxedoDialect.Sqlite => sql + $" LIMIT {pageSize} OFFSET {offset}",
                _ => sql + $" OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY" // Default to SQL Server syntax
            };
            
            return sql;
        }

        protected virtual string GetWhereClause(Expression<Func<TEntity, bool>> predicate)
        {
            return _expressionConverter.Convert(predicate);
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

        void ITransactional.SetTransaction(IDbTransaction? transaction)
        {
            Transaction = transaction;
        }
    }
}