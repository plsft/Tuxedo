using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Tuxedo.Patterns
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDbConnection _connection;
        private IDbTransaction? _transaction;
        private readonly Dictionary<Type, object> _repositories;
        private readonly ILogger<UnitOfWork>? _logger;
        private bool _disposed;

        public IDbConnection Connection => _connection;
        public IDbTransaction? Transaction => _transaction;

        public UnitOfWork(IDbConnection connection, ILogger<UnitOfWork>? logger = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = logger;
            _repositories = new Dictionary<Type, object>();
            
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);
            
            if (!_repositories.ContainsKey(type))
            {
                var repository = new DapperRepository<TEntity>(_connection, _transaction);
                _repositories.Add(type, repository);
            }
            
            return (IRepository<TEntity>)_repositories[type];
        }

        public async Task<IDbTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, 
            CancellationToken cancellationToken = default)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }
            
            _transaction = await Task.Run(() => _connection.BeginTransaction(isolationLevel), cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("Transaction started with isolation level: {IsolationLevel}", isolationLevel);
            
            // Update all existing repositories with the new transaction
            foreach (var repository in _repositories.Values)
            {
                if (repository is ITransactional transactional)
                {
                    transactional.SetTransaction(_transaction);
                }
            }
            
            return _transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction to commit");
            }
            
            try
            {
                await Task.Run(() => _transaction.Commit(), cancellationToken).ConfigureAwait(false);
                _logger?.LogDebug("Transaction committed successfully");
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
                UpdateRepositoriesTransaction(null);
            }
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No transaction to rollback");
            }
            
            try
            {
                await Task.Run(() => _transaction.Rollback(), cancellationToken).ConfigureAwait(false);
                _logger?.LogDebug("Transaction rolled back");
            }
            finally
            {
                _transaction.Dispose();
                _transaction = null;
                UpdateRepositoriesTransaction(null);
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // In a pure Dapper implementation, changes are immediate
            // This method is here for compatibility with the pattern
            // You could track changes and batch them here if needed
            
            if (_transaction != null)
            {
                await CommitAsync(cancellationToken).ConfigureAwait(false);
                return 1; // Return a non-zero value to indicate success
            }
            
            return 0;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation, 
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            await BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            
            try
            {
                var result = await operation().ConfigureAwait(false);
                await CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during transaction execution");
                await RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private void UpdateRepositoriesTransaction(IDbTransaction? transaction)
        {
            foreach (var repository in _repositories.Values)
            {
                if (repository is ITransactional transactional)
                {
                    transactional.SetTransaction(transaction);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _connection?.Dispose();
                    _repositories.Clear();
                }
                
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    internal interface ITransactional
    {
        void SetTransaction(IDbTransaction? transaction);
    }
}