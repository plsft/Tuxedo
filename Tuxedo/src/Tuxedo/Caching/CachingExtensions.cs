using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tuxedo.Caching
{
    public static class CachingExtensions
    {
        public static IServiceCollection AddTuxedoCaching(this IServiceCollection services, Action<CachingOptions>? configure = null)
        {
            var options = new CachingOptions();
            configure?.Invoke(options);

            if (options.UseMemoryCache)
            {
                services.AddMemoryCache();
                services.TryAddSingleton<IQueryCache, MemoryQueryCache>();
            }

            if (options.UseDistributedCache && options.DistributedCacheFactory != null)
            {
                services.TryAddSingleton<IQueryCache>(sp => options.DistributedCacheFactory(sp));
            }

            return services;
        }

        public static async Task<IEnumerable<T>> QueryWithCacheAsync<T>(
            this IDbConnection connection,
            string sql,
            object? param = null,
            IDbTransaction? transaction = null,
            TimeSpan? cacheExpiration = null,
            string? cacheKey = null,
            CancellationToken cancellationToken = default)
        {
            var cache = ServiceLocator.GetService<IQueryCache>();
            if (cache == null)
                return await connection.QueryAsync<T>(sql, param, transaction).ConfigureAwait(false);

            cacheKey ??= GenerateCacheKey(sql, param);
            
            return await cache.GetOrAddAsync(
                cacheKey,
                async () => await connection.QueryAsync<T>(sql, param, transaction).ConfigureAwait(false),
                cacheExpiration,
                cancellationToken).ConfigureAwait(false) ?? new List<T>();
        }

        public static async Task<T?> QuerySingleWithCacheAsync<T>(
            this IDbConnection connection,
            string sql,
            object? param = null,
            IDbTransaction? transaction = null,
            TimeSpan? cacheExpiration = null,
            string? cacheKey = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var cache = ServiceLocator.GetService<IQueryCache>();
            if (cache == null)
                return await connection.QuerySingleOrDefaultAsync<T>(sql, param, transaction).ConfigureAwait(false);

            cacheKey ??= GenerateCacheKey(sql, param);
            
            return await cache.GetOrAddAsync(
                cacheKey,
                async () => await connection.QuerySingleOrDefaultAsync<T>(sql, param, transaction).ConfigureAwait(false),
                cacheExpiration,
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task InvalidateCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cache = ServiceLocator.GetService<IQueryCache>();
            if (cache != null)
                await cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }

        public static async Task InvalidateCacheByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            var cache = ServiceLocator.GetService<IQueryCache>();
            if (cache != null)
                await cache.InvalidateByTagAsync(tag, cancellationToken).ConfigureAwait(false);
        }

        private static string GenerateCacheKey(string sql, object? parameters)
        {
            var key = new StringBuilder(sql);
            
            if (parameters != null)
            {
                var json = JsonSerializer.Serialize(parameters);
                key.Append(":").Append(json);
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key.ToString()));
            return Convert.ToBase64String(hash);
        }
    }

    public class CachingOptions
    {
        public bool UseMemoryCache { get; set; } = true;
        public bool UseDistributedCache { get; set; } = false;
        public Func<IServiceProvider, IQueryCache>? DistributedCacheFactory { get; set; }
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
    }

    // Simple service locator for extension methods
    internal static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static T? GetService<T>() where T : class
        {
            return _serviceProvider?.GetService<T>();
        }
    }
}