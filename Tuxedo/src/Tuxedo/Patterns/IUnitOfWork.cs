using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Tuxedo.Patterns
{
    public interface IUnitOfWork : IDisposable
    {
        IDbConnection Connection { get; }
        IDbTransaction? Transaction { get; }
        
        IRepository<TEntity> Repository<TEntity>() where TEntity : class;
        
        Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
        
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation, 
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);
    }
}