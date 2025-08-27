using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tuxedo.Resiliency
{
    public interface IRetryPolicy
    {
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
        Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
        T Execute<T>(Func<T> operation);
        void Execute(Action operation);
    }
}