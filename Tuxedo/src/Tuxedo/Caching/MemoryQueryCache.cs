using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Tuxedo.Caching
{
    public class MemoryQueryCache : IQueryCache, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryQueryCache>? _logger;
        private readonly ConcurrentDictionary<string, HashSet<string>> _taggedKeys;
        private readonly SemaphoreSlim _semaphore;

        public MemoryQueryCache(IMemoryCache cache, ILogger<MemoryQueryCache>? logger = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger;
            _taggedKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var result = _cache.Get<T>(key);
            
            if (result != null)
                _logger?.LogDebug("Cache hit for key: {Key}", key);
            else
                _logger?.LogDebug("Cache miss for key: {Key}", key);
            
            return Task.FromResult(result);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var options = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
                options.SetSlidingExpiration(expiration.Value);
            else
                options.SetSlidingExpiration(TimeSpan.FromMinutes(5)); // Default 5 minutes

            _cache.Set(key, value, options);
            _logger?.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
            
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            _cache.Remove(key);
            _logger?.LogDebug("Removed cache entry for key: {Key}", key);
            
            return Task.CompletedTask;
        }

        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (cached != null)
                return cached;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
                if (cached != null)
                    return cached;

                var value = await factory().ConfigureAwait(false);
                await SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
                return value;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

            if (_taggedKeys.TryGetValue(tag, out var keys))
            {
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                }
                
                _taggedKeys.TryRemove(tag, out _);
                _logger?.LogDebug("Invalidated {Count} cache entries with tag: {Tag}", keys.Count, tag);
            }
            
            return Task.CompletedTask;
        }

        public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
        {
            // Note: IMemoryCache doesn't provide a clear all method
            // This is a limitation of the current implementation
            foreach (var tagEntry in _taggedKeys)
            {
                foreach (var key in tagEntry.Value)
                {
                    _cache.Remove(key);
                }
            }
            
            _taggedKeys.Clear();
            _logger?.LogDebug("Invalidated all cache entries");
            
            return Task.CompletedTask;
        }

        public void AddKeyToTag(string key, string tag)
        {
            _taggedKeys.AddOrUpdate(tag,
                new HashSet<string> { key },
                (_, existing) =>
                {
                    existing.Add(key);
                    return existing;
                });
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}