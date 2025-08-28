using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Tuxedo;
using Tuxedo.Caching;
using Tuxedo.Contrib;
using Xunit;

public class CachingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IQueryCache _cache;
    private readonly ServiceProvider _serviceProvider;

    public CachingTests()
    {
        _connection = CreateSqliteConnection();
        CreateTestTable();
        SeedTestData();
        
        // Setup DI container with caching
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<IQueryCache, MemoryQueryCache>();
        
        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<IQueryCache>();
        
        // Set service provider for extension methods
        var serviceLocatorType = typeof(CachingExtensions).Assembly
            .GetType("Tuxedo.Caching.ServiceLocator");
        serviceLocatorType?.GetMethod("SetServiceProvider")
            ?.Invoke(null, new object[] { _serviceProvider });
    }

    private SqliteConnection CreateSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private void CreateTestTable()
    {
        var createTable = @"
            CREATE TABLE CacheTestProducts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Category TEXT,
                Stock INTEGER DEFAULT 0,
                LastModified TEXT DEFAULT CURRENT_TIMESTAMP
            )";
        
        _connection.Execute(createTable);
    }

    private void SeedTestData()
    {
        var products = new[]
        {
            new { Name = "Laptop", Price = 999.99, Category = "Electronics", Stock = 10 },
            new { Name = "Mouse", Price = 19.99, Category = "Electronics", Stock = 50 },
            new { Name = "Desk", Price = 199.99, Category = "Furniture", Stock = 5 }
        };
        
        foreach (var product in products)
        {
            _connection.Execute(
                "INSERT INTO CacheTestProducts (Name, Price, Category, Stock) VALUES (@Name, @Price, @Category, @Stock)",
                product);
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNonExistentKey()
    {
        var result = await _cache.GetAsync<string>("non-existent-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_StoresAndRetrievesValue()
    {
        var key = "test-key";
        var value = "test-value";
        
        await _cache.SetAsync(key, value);
        var retrieved = await _cache.GetAsync<string>(key);
        
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task SetAsync_WithExpiration_ExpiresAfterTimeout()
    {
        var key = "expiring-key";
        var value = "expiring-value";
        var expiration = TimeSpan.FromMilliseconds(100);
        
        await _cache.SetAsync(key, value, expiration);
        
        // Should exist immediately
        var retrieved = await _cache.GetAsync<string>(key);
        Assert.Equal(value, retrieved);
        
        // Wait for expiration
        await Task.Delay(150);
        
        // Should be expired
        var expired = await _cache.GetAsync<string>(key);
        Assert.Null(expired);
    }

    [Fact]
    public async Task RemoveAsync_RemovesCachedItem()
    {
        var key = "removable-key";
        var value = "removable-value";
        
        await _cache.SetAsync(key, value);
        await _cache.RemoveAsync(key);
        
        var result = await _cache.GetAsync<string>(key);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrAddAsync_ReturnsExistingValue()
    {
        var key = "get-or-add-key";
        var value = "existing-value";
        
        await _cache.SetAsync(key, value);
        
        var factoryCalled = false;
        var result = await _cache.GetOrAddAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult("new-value");
        });
        
        Assert.Equal(value, result);
        Assert.False(factoryCalled); // Factory should not be called
    }

    [Fact]
    public async Task GetOrAddAsync_CallsFactoryWhenMissing()
    {
        var key = "missing-key";
        var value = "factory-value";
        
        var factoryCalled = false;
        var result = await _cache.GetOrAddAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(value);
        });
        
        Assert.Equal(value, result);
        Assert.True(factoryCalled);
        
        // Verify it was cached
        var cached = await _cache.GetAsync<string>(key);
        Assert.Equal(value, cached);
    }

    [Fact]
    public async Task InvalidateByTagAsync_RemovesTaggedItems()
    {
        var cache = _cache as MemoryQueryCache;
        Assert.NotNull(cache);
        
        // Add items with tags
        var tag = "product-tag";
        var key1 = "product-1";
        var key2 = "product-2";
        var key3 = "other-key";
        
        await cache.SetAsync(key1, "value1");
        cache.AddKeyToTag(key1, tag);
        
        await cache.SetAsync(key2, "value2");
        cache.AddKeyToTag(key2, tag);
        
        await cache.SetAsync(key3, "value3");
        
        // Invalidate by tag
        await cache.InvalidateByTagAsync(tag);
        
        // Tagged items should be removed
        Assert.Null(await cache.GetAsync<string>(key1));
        Assert.Null(await cache.GetAsync<string>(key2));
        
        // Untagged item should remain
        Assert.Equal("value3", await cache.GetAsync<string>(key3));
    }

    [Fact]
    public async Task InvalidateAllAsync_RemovesAllItems()
    {
        // Add multiple items
        await _cache.SetAsync("key1", "value1");
        await _cache.SetAsync("key2", "value2");
        await _cache.SetAsync("key3", "value3");
        
        // Invalidate all
        await _cache.InvalidateAllAsync();
        
        // All items should be removed
        Assert.Null(await _cache.GetAsync<string>("key1"));
        Assert.Null(await _cache.GetAsync<string>("key2"));
        Assert.Null(await _cache.GetAsync<string>("key3"));
    }

    [Fact]
    public async Task QueryWithCacheAsync_CachesQueryResults()
    {
        var sql = "SELECT * FROM CacheTestProducts WHERE Category = @Category";
        var param = new { Category = "Electronics" };
        var cacheKey = "electronics-products";
        
        // First call - should hit database
        var result1 = await _connection.QueryWithCacheAsync<CacheTestProduct>(
            sql, param, cacheExpiration: TimeSpan.FromMinutes(5), cacheKey: cacheKey);
        
        Assert.NotNull(result1);
        Assert.Equal(2, result1.Count());
        
        // Modify data in database
        await _connection.ExecuteAsync(
            "UPDATE CacheTestProducts SET Price = Price * 2 WHERE Category = @Category",
            new { Category = "Electronics" });
        
        // Second call - should hit cache (not see the price change)
        var result2 = await _connection.QueryWithCacheAsync<CacheTestProduct>(
            sql, param, cacheExpiration: TimeSpan.FromMinutes(5), cacheKey: cacheKey);
        
        Assert.NotNull(result2);
        Assert.Equal(2, result2.Count());
        Assert.Equal(result1.First().Price, result2.First().Price); // Prices should be same (from cache)
        
        // Invalidate cache
        await CachingExtensions.InvalidateCacheAsync(cacheKey);
        
        // Third call - should hit database again (see the new prices)
        var result3 = await _connection.QueryWithCacheAsync<CacheTestProduct>(
            sql, param, cacheExpiration: TimeSpan.FromMinutes(5), cacheKey: cacheKey);
        
        Assert.NotNull(result3);
        Assert.Equal(2, result3.Count());
        Assert.NotEqual(result1.First().Price, result3.First().Price); // Prices should be different
    }

    [Fact]
    public async Task QuerySingleWithCacheAsync_CachesSingleResult()
    {
        var sql = "SELECT * FROM CacheTestProducts WHERE Name = @Name";
        var param = new { Name = "Laptop" };
        var cacheKey = "laptop-product";
        
        // First call - should hit database
        var result1 = await _connection.QuerySingleWithCacheAsync<CacheTestProduct>(
            sql, param, cacheExpiration: TimeSpan.FromMinutes(5), cacheKey: cacheKey);
        
        Assert.NotNull(result1);
        Assert.Equal("Laptop", result1.Name);
        
        // Update database
        await _connection.ExecuteAsync(
            "UPDATE CacheTestProducts SET Stock = Stock + 100 WHERE Name = @Name",
            new { Name = "Laptop" });
        
        // Second call - should hit cache
        var result2 = await _connection.QuerySingleWithCacheAsync<CacheTestProduct>(
            sql, param, cacheExpiration: TimeSpan.FromMinutes(5), cacheKey: cacheKey);
        
        Assert.NotNull(result2);
        Assert.Equal(result1.Stock, result2.Stock); // Stock should be same (from cache)
    }

    [Fact]
    public async Task CacheKey_AutoGeneration_WorksCorrectly()
    {
        var sql = "SELECT * FROM CacheTestProducts WHERE Category = @Category";
        var param1 = new { Category = "Electronics" };
        var param2 = new { Category = "Furniture" };
        
        // Query with different parameters should generate different cache keys
        var result1 = await _connection.QueryWithCacheAsync<CacheTestProduct>(
            sql, param1, cacheExpiration: TimeSpan.FromMinutes(5));
        
        var result2 = await _connection.QueryWithCacheAsync<CacheTestProduct>(
            sql, param2, cacheExpiration: TimeSpan.FromMinutes(5));
        
        Assert.NotEqual(result1.Count(), result2.Count());
        Assert.Equal(2, result1.Count()); // Electronics
        Assert.Equal(1, result2.Count()); // Furniture
    }

    [Fact]
    public async Task ConcurrentAccess_HandlesCorrectly()
    {
        var key = "concurrent-key";
        var callCount = 0;
        
        // Create multiple concurrent GetOrAdd calls
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_cache.GetOrAddAsync(key, async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10); // Simulate slow factory
                return "concurrent-value";
            }));
        }
        
        var results = await Task.WhenAll(tasks);
        
        // All should return the same value
        Assert.All(results, r => Assert.Equal("concurrent-value", r));
        
        // Factory should only be called once (thanks to semaphore)
        Assert.Equal(1, callCount);
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Table("CacheTestProducts")]
    private class CacheTestProduct
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public int Stock { get; set; }
        public DateTime LastModified { get; set; }
    }
}