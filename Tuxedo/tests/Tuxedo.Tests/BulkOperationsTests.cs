using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Tuxedo;
using Tuxedo.BulkOperations;
using Tuxedo.Contrib;
using Tuxedo.DependencyInjection;
using Xunit;

public class BulkOperationsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IBulkOperations _bulkOperations;

    public BulkOperationsTests()
    {
        _connection = CreateSqliteConnection();
        _bulkOperations = new BulkOperations(TuxedoDialect.Sqlite);
        CreateTestTables();
    }

    private SqliteConnection CreateSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private void CreateTestTables()
    {
        var createProductTable = @"
            CREATE TABLE BulkProducts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Category TEXT,
                Stock INTEGER DEFAULT 0,
                IsActive INTEGER DEFAULT 1,
                LastModified TEXT,
                CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP
            )";

        var createCustomerTable = @"
            CREATE TABLE BulkCustomers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT UNIQUE NOT NULL,
                City TEXT,
                Country TEXT,
                CreditLimit REAL DEFAULT 0,
                IsVip INTEGER DEFAULT 0,
                JoinDate TEXT DEFAULT CURRENT_TIMESTAMP
            )";

        var createOrderTable = @"
            CREATE TABLE BulkOrders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderNumber TEXT UNIQUE NOT NULL,
                CustomerId INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT DEFAULT 'Pending',
                OrderDate TEXT DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (CustomerId) REFERENCES BulkCustomers(Id)
            )";

        _connection.Execute(createProductTable);
        _connection.Execute(createCustomerTable);
        _connection.Execute(createOrderTable);
    }

    [Fact]
    public async Task BulkInsertAsync_InsertsMultipleRecords()
    {
        var products = GenerateProducts(100);
        
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(100, count);
        
        var insertedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts");
        Assert.Equal(100, insertedProducts.Count());
    }

    [Fact]
    public async Task BulkInsertAsync_WithBatchSize_InsertsInBatches()
    {
        var products = GenerateProducts(50);
        
        await _bulkOperations.BulkInsertAsync(_connection, products, batchSize: 10);
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(50, count);
    }

    [Fact]
    public async Task BulkInsertAsync_EmptyCollection_DoesNothing()
    {
        var products = new List<BulkProduct>();
        
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BulkInsertAsync_HandlesComputedProperties()
    {
        var products = new List<BulkProduct>
        {
            new BulkProduct 
            { 
                Name = "Test Product", 
                Price = 99.99m, 
                Category = "Test",
                CreatedDate = DateTime.Now // This should be ignored as it's computed
            }
        };
        
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var inserted = _connection.QueryFirst<BulkProduct>("SELECT * FROM BulkProducts WHERE Name = @Name", new { Name = "Test Product" });
        Assert.NotNull(inserted);
        Assert.Equal("Test Product", inserted.Name);
    }

    [Fact]
    public async Task BulkUpdateAsync_UpdatesMultipleRecords()
    {
        // First insert some products
        var products = GenerateProducts(50);
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        // Get inserted products and update them
        var insertedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts").ToList();
        foreach (var product in insertedProducts)
        {
            product.Price *= 1.1m; // 10% price increase
            product.LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        await _bulkOperations.BulkUpdateAsync(_connection, insertedProducts);
        
        var updatedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts");
        Assert.All(updatedProducts, p => Assert.NotNull(p.LastModified));
    }

    [Fact]
    public async Task BulkUpdateAsync_WithBatchSize_UpdatesInBatches()
    {
        // First insert some products
        var products = GenerateProducts(30);
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var insertedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts").ToList();
        foreach (var product in insertedProducts)
        {
            product.Stock += 10;
        }
        
        await _bulkOperations.BulkUpdateAsync(_connection, insertedProducts, batchSize: 5);
        
        var updatedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts");
        Assert.All(updatedProducts, p => Assert.True(p.Stock >= 10));
    }

    [Fact]
    public async Task BulkDeleteAsync_DeletesMultipleRecords()
    {
        // Insert products
        var products = GenerateProducts(20);
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        // Get products to delete
        var productsToDelete = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts WHERE Price < 50").ToList();
        var deleteCount = productsToDelete.Count();
        
        await _bulkOperations.BulkDeleteAsync(_connection, productsToDelete);
        
        var remainingCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(20 - deleteCount, remainingCount);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithBatchSize_DeletesInBatches()
    {
        // Insert products
        var products = GenerateProducts(15);
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var productsToDelete = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts").ToList();
        
        await _bulkOperations.BulkDeleteAsync(_connection, productsToDelete, batchSize: 3);
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BulkMergeAsync_InsertsAndUpdates()
    {
        // Insert initial products
        var initialProducts = GenerateProducts(10);
        await _bulkOperations.BulkInsertAsync(_connection, initialProducts);
        
        // Create merge set: 5 existing (to update) and 5 new (to insert)
        var existingProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts LIMIT 5").ToList();
        foreach (var product in existingProducts)
        {
            product.Price *= 2; // Double the price
            product.Stock = 999;
        }
        
        var newProducts = GenerateProducts(5, startId: 1000); // Use high IDs to ensure they're new
        
        var mergeProducts = existingProducts.Concat(newProducts).ToList();
        
        await _bulkOperations.BulkMergeAsync(_connection, mergeProducts);
        
        var totalCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(15, totalCount); // 10 original + 5 new
        
        var updatedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts WHERE Stock = 999");
        Assert.Equal(5, updatedProducts.Count());
    }

    [Fact]
    public async Task BulkOperations_WithTransaction_CommitsSuccessfully()
    {
        using var transaction = _connection.BeginTransaction();
        
        var products = GenerateProducts(10);
        await _bulkOperations.BulkInsertAsync(_connection, products, transaction: transaction);
        
        transaction.Commit();
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task BulkOperations_WithTransaction_RollbackWorks()
    {
        using var transaction = _connection.BeginTransaction();
        
        var products = GenerateProducts(10);
        await _bulkOperations.BulkInsertAsync(_connection, products, transaction: transaction);
        
        transaction.Rollback();
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BulkInsertAsync_LargeDataset_PerformsWell()
    {
        var products = GenerateProducts(1000);
        
        var startTime = DateTime.Now;
        await _bulkOperations.BulkInsertAsync(_connection, products, batchSize: 100);
        var duration = DateTime.Now - startTime;
        
        var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM BulkProducts");
        Assert.Equal(1000, count);
        Assert.True(duration.TotalSeconds < 5, $"Bulk insert took too long: {duration.TotalSeconds} seconds");
    }

    [Fact]
    public async Task BulkInsertAsync_WithComplexTypes_HandlesCorrectly()
    {
        var customers = new List<BulkCustomer>
        {
            new BulkCustomer 
            { 
                Name = "John Doe", 
                Email = "john@example.com", 
                City = "New York",
                Country = "USA",
                CreditLimit = 5000.00m,
                IsVip = true
            },
            new BulkCustomer 
            { 
                Name = "Jane Smith", 
                Email = "jane@example.com", 
                City = "London",
                Country = "UK",
                CreditLimit = 3000.00m,
                IsVip = false
            }
        };
        
        await _bulkOperations.BulkInsertAsync(_connection, customers);
        
        var inserted = _connection.Query<BulkCustomer>("SELECT * FROM BulkCustomers");
        Assert.Equal(2, inserted.Count());
        
        var vipCustomer = inserted.First(c => c.IsVip);
        Assert.Equal("John Doe", vipCustomer.Name);
        Assert.Equal(5000.00m, vipCustomer.CreditLimit);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithNullValues_HandlesCorrectly()
    {
        var products = new List<BulkProduct>
        {
            new BulkProduct { Name = "Product 1", Price = 10m, Category = "Cat1" },
            new BulkProduct { Name = "Product 2", Price = 20m, Category = "Cat2" }
        };
        
        await _bulkOperations.BulkInsertAsync(_connection, products);
        
        var insertedProducts = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts").ToList();
        
        // Set some values to null
        insertedProducts[0].Category = null;
        insertedProducts[1].LastModified = null;
        
        await _bulkOperations.BulkUpdateAsync(_connection, insertedProducts);
        
        var updated = _connection.Query<BulkProduct>("SELECT * FROM BulkProducts ORDER BY Id");
        Assert.Null(updated.First().Category);
    }

    [Fact]
    public async Task BulkMergeAsync_WithKeyAttribute_UsesCorrectKey()
    {
        var orders = new List<BulkOrder>
        {
            new BulkOrder { OrderNumber = "ORD001", CustomerId = 1, TotalAmount = 100m },
            new BulkOrder { OrderNumber = "ORD002", CustomerId = 2, TotalAmount = 200m }
        };
        
        // Insert initial orders
        foreach (var order in orders)
        {
            _connection.Insert(order);
        }
        
        // Prepare merge data - update existing and add new
        var mergeOrders = new List<BulkOrder>
        {
            new BulkOrder { OrderNumber = "ORD001", CustomerId = 1, TotalAmount = 150m, Status = "Processed" },
            new BulkOrder { OrderNumber = "ORD003", CustomerId = 3, TotalAmount = 300m }
        };
        
        await _bulkOperations.BulkMergeAsync(_connection, mergeOrders);
        
        var allOrders = _connection.Query<BulkOrder>("SELECT * FROM BulkOrders ORDER BY OrderNumber");
        Assert.Equal(3, allOrders.Count());
        
        var updatedOrder = allOrders.First(o => o.OrderNumber == "ORD001");
        Assert.Equal(150m, updatedOrder.TotalAmount);
        Assert.Equal("Processed", updatedOrder.Status);
    }

    [Fact]
    public async Task BulkOperations_WithCancellation_CanBeCancelled()
    {
        var products = GenerateProducts(100);
        var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _bulkOperations.BulkInsertAsync(_connection, products, cancellationToken: cts.Token);
        });
    }


    private List<BulkProduct> GenerateProducts(int count, int startId = 1)
    {
        var products = new List<BulkProduct>();
        var random = new Random();
        var categories = new[] { "Electronics", "Furniture", "Clothing", "Food", "Books" };
        
        for (int i = 0; i < count; i++)
        {
            products.Add(new BulkProduct
            {
                Name = $"Product {startId + i}",
                Price = (decimal)(random.NextDouble() * 100 + 10),
                Category = categories[random.Next(categories.Length)],
                Stock = random.Next(0, 100),
                IsActive = random.Next(2) == 1
            });
        }
        
        return products;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    // Test models
    [Table("BulkProducts")]
    private class BulkProduct
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public string LastModified { get; set; }
        
        [Computed]
        public DateTime CreatedDate { get; set; }
    }

    [Table("BulkCustomers")]
    private class BulkCustomer
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsVip { get; set; }
        
        [Computed]
        public DateTime JoinDate { get; set; }
    }

    [Table("BulkOrders")]
    private class BulkOrder
    {
        [Key]
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        
        [Computed]
        public DateTime OrderDate { get; set; }
    }
}