using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tuxedo.Caching
{
    public interface IQueryCache
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
        Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
        Task InvalidateAllAsync(CancellationToken cancellationToken = default);
    }
}