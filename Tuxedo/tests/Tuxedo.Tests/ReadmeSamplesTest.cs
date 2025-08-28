using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tuxedo;
using Tuxedo.Contrib;
using Tuxedo.DependencyInjection;
using Xunit;

public class ReadmeSamplesTest
{
        [Table("Products")]
        public class Product
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? Category { get; set; }
            [Computed]
            public DateTime LastModified { get; set; } = DateTime.Now;
        }

        public record DbOptions { public string ConnectionString { get; init; } = string.Empty; }

        private IDbConnection CreateSqliteConnection()
        {
            var connectionString = "Data Source=:memory:";
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            
            // Create the Products table
            connection.Execute(@"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price DECIMAL(10,2) NOT NULL,
                    Category TEXT,
                    LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
                )");
            
            return connection;
        }

        [Fact]
        public void BasicCrudOperations_Work()
        {
            using var db = CreateSqliteConnection();

            // Test Insert
            var id = db.Insert(new Product { Name = "Gizmo", Price = 19.99m, Category = "Gadgets" });
            Assert.True(id > 0);

            // Test Get
            var p = db.Get<Product>(id);
            Assert.NotNull(p);
            Assert.Equal("Gizmo", p.Name);
            Assert.Equal(19.99m, p.Price);
            Assert.Equal("Gadgets", p.Category);

            // Test Update
            p.Price = 17.99m;
            var updated = db.Update(p);
            Assert.True(updated);

            var updatedProduct = db.Get<Product>(id);
            Assert.Equal(17.99m, updatedProduct.Price);

            // Test Delete
            var deleted = db.Delete(p);
            Assert.True(deleted);

            var deletedProduct = db.Get<Product>(id);
            Assert.Null(deletedProduct);
        }

        [Fact]
        public async Task AsyncCrudOperations_Work()
        {
            using var db = CreateSqliteConnection();

            // Test InsertAsync
            var newId = await db.InsertAsync(new Product { Name = "Async", Price = 10m });
            Assert.True(newId > 0);

            // Test SelectAsync (alias for GetAsync)
            var item = await db.SelectAsync<Product>(newId);
            Assert.NotNull(item);
            Assert.Equal("Async", item.Name);
            Assert.Equal(10m, item.Price);

            // Test UpdateAsync
            item.Price = 15m;
            var updated = await db.UpdateAsync(item);
            Assert.True(updated);

            // Test DeleteAsync
            var deleted = await db.DeleteAsync(item);
            Assert.True(deleted);
        }

        [Fact]
        public void DapperQuery_Works()
        {
            using var db = CreateSqliteConnection();

            // Insert test data
            db.Insert(new Product { Name = "Book1", Price = 29.99m, Category = "Books" });
            db.Insert(new Product { Name = "Book2", Price = 39.99m, Category = "Books" });
            db.Insert(new Product { Name = "Gadget1", Price = 19.99m, Category = "Gadgets" });

            // Dapper query
            var products = db.Query<Product>("SELECT * FROM Products WHERE Category = @cat", new { cat = "Books" });
            
            Assert.Equal(2, products.Count());
            Assert.All(products, p => Assert.Equal("Books", p.Category));
        }

        [Fact]
        public void DependencyInjection_BasicSetup_Works()
        {
            var services = new ServiceCollection();
            
            // Register connection factory
            services.AddTuxedo(_ => CreateSqliteConnection());

            var provider = services.BuildServiceProvider();
            var conn = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotNull(conn);
            Assert.Equal(ConnectionState.Open, conn.State);

            // Test that we can use it for operations
            var id = conn.Insert(new Product { Name = "DI Test", Price = 99.99m });
            Assert.True(id > 0);

            provider.Dispose();
        }

        [Fact]
        public void DependencyInjection_WithOptions_Works()
        {
            var services = new ServiceCollection();
            
            services.AddSingleton(new DbOptions { ConnectionString = "Data Source=:memory:" });
            services.AddTuxedoWithOptions<DbOptions>((sp, opt) => 
            {
                var conn = new SqliteConnection(opt.ConnectionString);
                conn.Open();
                conn.Execute(@"
                    CREATE TABLE Products (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Price DECIMAL(10,2) NOT NULL,
                        Category TEXT,
                        LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
                    )");
                return conn;
            });

            var provider = services.BuildServiceProvider();
            var conn = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotNull(conn);
            
            // Test functionality
            var id = conn.Insert(new Product { Name = "Options Test", Price = 49.99m });
            Assert.True(id > 0);

            provider.Dispose();
        }

        [Fact]
        public void DependencyInjection_SqliteInMemory_Works()
        {
            var services = new ServiceCollection();
            services.AddTuxedoSqliteInMemory("TestDb");

            var provider = services.BuildServiceProvider();
            var conn = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotNull(conn);
            Assert.IsType<SqliteConnection>(conn);

            provider.Dispose();
        }

        [Fact]
        public void ConfigurationBasedHelpers_Work()
        {
            // Build configuration (example uses in-memory for simplicity)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TuxedoSqlite:ConnectionString"] = "Data Source=:memory:",
                    ["TuxedoSqlite:Mode"] = "ReadWriteCreate",
                    ["TuxedoSqlite:Cache"] = "Default"
                })
                .Build();

            var services = new ServiceCollection();
            
            // Use SQLite instead of SQL Server for testing
            services.AddTuxedoSqliteWithOptions(config, "TuxedoSqlite");

            var provider = services.BuildServiceProvider();
            var conn = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotNull(conn);
            Assert.IsType<SqliteConnection>(conn);

            provider.Dispose();
        }

        [Fact]
        public void ProviderHelpers_SqliteFile_Works()
        {
            var services = new ServiceCollection();
            services.AddTuxedoSqlite("test.db");

            var provider = services.BuildServiceProvider();
            var conn = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotNull(conn);
            Assert.IsType<SqliteConnection>(conn);

            provider.Dispose();
        }

        [Fact]
        public void GetAll_Works()
        {
            using var db = CreateSqliteConnection();

            // Insert test data
            db.Insert(new Product { Name = "Product1", Price = 10m, Category = "Cat1" });
            db.Insert(new Product { Name = "Product2", Price = 20m, Category = "Cat2" });
            db.Insert(new Product { Name = "Product3", Price = 30m, Category = "Cat1" });

            // Test GetAll
            var allProducts = db.GetAll<Product>();
            Assert.Equal(3, allProducts.Count());

            // Test async version
            var allProductsAsync = db.GetAllAsync<Product>().Result;
            Assert.Equal(3, allProductsAsync.Count());
        }

        [Fact]
        public void AttributeMapping_Works()
        {
            using var db = CreateSqliteConnection();

            // Test that [Table], [Key], [Computed] attributes work
            var product = new Product 
            { 
                Name = "Attribute Test", 
                Price = 25.50m, 
                Category = "Test"
                // LastModified should be set by the database due to [Computed]
            };

            var id = db.Insert(product);
            Assert.True(id > 0);

            var retrieved = db.Get<Product>(id);
            Assert.NotNull(retrieved);
            Assert.Equal("Attribute Test", retrieved.Name);
            Assert.Equal(25.50m, retrieved.Price);
            Assert.Equal("Test", retrieved.Category);
            // LastModified should have been set by the database
            Assert.True(retrieved.LastModified > DateTime.MinValue);
        }

    [Fact]
    public void PartialUpdate_WithPropertyNames_Works()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Original Product", 
            Price = 29.99m, 
            Category = "Electronics" 
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Update only specific fields
        product.Name = "Updated Product";
        product.LastModified = DateTime.Now;
        product.Price = 999.99m; // This should NOT be updated

        var updated = db.Update(product, null, null, new[] { "Name", "LastModified" });
        Assert.True(updated);

        // Verify only specified fields were updated
        var retrieved = db.Get<Product>(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Product", retrieved.Name);
        Assert.Equal(29.99m, retrieved.Price); // Should remain unchanged
        Assert.Equal("Electronics", retrieved.Category); // Should remain unchanged
    }

    [Fact]
    public void PartialUpdate_WithKeyValueObjects_Works()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Test Product", 
            Price = 15.99m, 
            Category = "Books" 
        };
        var id = (int)db.Insert(product);

        // Update using separate key/value objects
        var updated = db.Update<Product>(
            keyValues: new { Id = id },
            updateValues: new { Name = "Updated via Objects", Price = 19.99m }
        );
        Assert.True(updated);

        // Verify update
        var retrieved = db.Get<Product>(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Updated via Objects", retrieved.Name);
        Assert.Equal(19.99m, retrieved.Price);
        Assert.Equal("Books", retrieved.Category); // Should remain unchanged
    }

    [Fact]
    public async Task PartialUpdateAsync_Works()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Async Product", 
            Price = 9.99m, 
            Category = "Software" 
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Update only Name
        product.Name = "Async Updated Product";
        product.Price = 999.99m; // Should NOT be updated

        var updated = await db.UpdateAsync(product, null, null, new[] { "Name" });
        Assert.True(updated);

        // Verify only Name was updated
        var retrieved = db.Get<Product>(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Async Updated Product", retrieved.Name);
        Assert.Equal(9.99m, retrieved.Price); // Should remain unchanged
        Assert.Equal("Software", retrieved.Category); // Should remain unchanged
    }
}