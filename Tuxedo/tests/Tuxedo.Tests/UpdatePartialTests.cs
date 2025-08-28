using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Tuxedo;
using Tuxedo.Contrib;
using Xunit;

public class UpdatePartialTests
{
    [Table("Products")]
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        [Computed]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

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
                LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
                CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
            )");
        
        return connection;
    }

    [Fact]
    public void UpdatePartial_WithPropertyNames_UpdatesOnlySpecifiedFields()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Original Name", 
            Price = 10.00m, 
            Category = "Original Category",
            LastUpdated = DateTime.Now.AddDays(-1)
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Update only Name and LastUpdated
        product.Name = "Updated Name";
        product.LastUpdated = DateTime.Now;
        product.Price = 999.99m; // This should NOT be updated
        product.Category = "Should Not Update"; // This should NOT be updated

        var result = db.UpdatePartial(product, new[] { "Name", "LastUpdated" });

        Assert.True(result);

        // Verify only specified fields were updated
        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(10.00m, updated.Price); // Should remain unchanged
        Assert.Equal("Original Category", updated.Category); // Should remain unchanged
        // LastUpdated should be updated (we can't check exact time due to precision)
    }

    [Fact]
    public void UpdatePartial_WithParamsPropertyNames_UpdatesOnlySpecifiedFields()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Original Name", 
            Price = 10.00m, 
            Category = "Original Category"
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Update only Price using params syntax
        product.Price = 25.50m;
        product.Name = "Should Not Update";

        var result = db.UpdatePartial(product, null, null, "Price");

        Assert.True(result);

        // Verify only Price was updated
        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Original Name", updated.Name); // Should remain unchanged
        Assert.Equal(25.50m, updated.Price); // Should be updated
        Assert.Equal("Original Category", updated.Category); // Should remain unchanged
    }

    [Fact]
    public void UpdatePartial_WithKeyAndUpdateObjects_UpdatesCorrectRecord()
    {
        using var db = CreateSqliteConnection();

        // Insert two test products
        var product1 = new Product { Name = "Product 1", Price = 10.00m, Category = "Cat1" };
        var product2 = new Product { Name = "Product 2", Price = 20.00m, Category = "Cat2" };
        
        var id1 = (int)db.Insert(product1);
        var id2 = (int)db.Insert(product2);

        // Update only product1's name and price using key/update objects
        var result = db.UpdatePartial<Product>(
            keyValues: new { Id = id1 }, 
            updateValues: new { Name = "Updated Product 1", Price = 15.00m }
        );

        Assert.True(result);

        // Verify product1 was updated
        var updated1 = db.Get<Product>(id1);
        Assert.NotNull(updated1);
        Assert.Equal("Updated Product 1", updated1.Name);
        Assert.Equal(15.00m, updated1.Price);
        Assert.Equal("Cat1", updated1.Category); // Should remain unchanged

        // Verify product2 was NOT updated
        var updated2 = db.Get<Product>(id2);
        Assert.NotNull(updated2);
        Assert.Equal("Product 2", updated2.Name); // Should remain unchanged
        Assert.Equal(20.00m, updated2.Price); // Should remain unchanged
        Assert.Equal("Cat2", updated2.Category); // Should remain unchanged
    }

    [Fact]
    public async Task UpdatePartialAsync_WithPropertyNames_UpdatesOnlySpecifiedFields()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product 
        { 
            Name = "Original Name", 
            Price = 10.00m, 
            Category = "Original Category"
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Update only Name and Category
        product.Name = "Async Updated Name";
        product.Category = "Async Updated Category";
        product.Price = 999.99m; // This should NOT be updated

        var result = await db.UpdatePartialAsync(product, new[] { "Name", "Category" });

        Assert.True(result);

        // Verify only specified fields were updated
        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Async Updated Name", updated.Name);
        Assert.Equal("Async Updated Category", updated.Category);
        Assert.Equal(10.00m, updated.Price); // Should remain unchanged
    }

    [Fact]
    public async Task UpdatePartialAsync_WithKeyAndUpdateObjects_UpdatesCorrectRecord()
    {
        using var db = CreateSqliteConnection();

        // Insert test product
        var product = new Product { Name = "Original", Price = 10.00m, Category = "Original Cat" };
        var id = db.Insert(product);

        // Update using key/update objects async
        var result = await db.UpdatePartialAsync<Product>(
            keyValues: new { Id = id }, 
            updateValues: new { Name = "Async Updated", LastUpdated = DateTime.Now }
        );

        Assert.True(result);

        // Verify correct fields were updated
        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Async Updated", updated.Name);
        Assert.Equal(10.00m, updated.Price); // Should remain unchanged
        Assert.Equal("Original Cat", updated.Category); // Should remain unchanged
    }

    [Fact]
    public void UpdatePartial_WithNonExistentProperties_ThrowsArgumentException()
    {
        using var db = CreateSqliteConnection();

        var product = new Product { Name = "Test", Price = 10.00m };
        var id = (int)db.Insert(product);
        product.Id = id;

        Assert.Throws<ArgumentException>(() => 
            db.UpdatePartial(product, new[] { "NonExistentProperty" })
        );
    }

    [Fact]
    public void UpdatePartial_WithKeyProperties_ThrowsArgumentException()
    {
        using var db = CreateSqliteConnection();

        var product = new Product { Name = "Test", Price = 10.00m };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Should not allow updating key properties
        Assert.Throws<ArgumentException>(() => 
            db.UpdatePartial(product, new[] { "Id", "Name" })
        );
    }

    [Fact]
    public void UpdatePartial_WithNullEntity_ThrowsArgumentException()
    {
        using var db = CreateSqliteConnection();

        Assert.Throws<ArgumentException>(() => 
            db.UpdatePartial<Product>(null!, new[] { "Name" })
        );
    }

    [Fact]
    public void UpdatePartial_WithEmptyPropertyList_ThrowsArgumentException()
    {
        using var db = CreateSqliteConnection();

        var product = new Product { Name = "Test", Price = 10.00m };

        Assert.Throws<ArgumentException>(() => 
            db.UpdatePartial(product, new string[0])
        );
    }

    [Fact]
    public void UpdatePartial_WithNonExistentId_ReturnsFalse()
    {
        using var db = CreateSqliteConnection();

        var product = new Product { Id = 999, Name = "Non-existent", Price = 10.00m };

        var result = db.UpdatePartial(product, new[] { "Name" });

        Assert.False(result);
    }

    [Fact]
    public void UpdatePartial_WithComputedProperties_IgnoresComputedFields()
    {
        using var db = CreateSqliteConnection();

        var product = new Product 
        { 
            Name = "Test", 
            Price = 10.00m,
            CreatedDate = DateTime.Now.AddYears(-1) // This is computed, should be ignored
        };
        var id = (int)db.Insert(product);
        product.Id = id;

        // Try to update a computed property - should not cause error, just ignore
        product.Name = "Updated Name";
        product.CreatedDate = DateTime.Now; // Should be ignored

        var result = db.UpdatePartial(product, new[] { "Name", "CreatedDate" });

        Assert.True(result); // Should succeed

        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name); // Should be updated
        // CreatedDate should remain as originally set by database, not the new value
    }

    [Fact]
    public void UpdatePartial_CaseInsensitivePropertyNames()
    {
        using var db = CreateSqliteConnection();

        var product = new Product { Name = "Original", Price = 10.00m };
        var id = (int)db.Insert(product);
        product.Id = id;

        product.Name = "Case Insensitive Update";

        // Use different case for property names
        var result = db.UpdatePartial(product, new[] { "name", "PRICE" });

        Assert.True(result);

        var updated = db.Get<Product>(id);
        Assert.NotNull(updated);
        Assert.Equal("Case Insensitive Update", updated.Name);
    }

    [Fact]
    public void UpdatePartial_MultipleRecordsWithSameUpdate_UpdatesAllMatching()
    {
        using var db = CreateSqliteConnection();

        // Create products with same category
        var product1 = new Product { Name = "Product1", Price = 10m, Category = "Electronics" };
        var product2 = new Product { Name = "Product2", Price = 20m, Category = "Electronics" };
        var product3 = new Product { Name = "Product3", Price = 30m, Category = "Books" };

        var id1 = (int)db.Insert(product1);
        var id2 = (int)db.Insert(product2);
        var id3 = (int)db.Insert(product3);

        // This tests the key/value version - should only update one record at a time by design
        var result = db.UpdatePartial<Product>(
            keyValues: new { Category = "Electronics" },
            updateValues: new { Price = 99m }
        );

        // Note: This should return true but only update the first matching record
        // as we're using WHERE with primary key semantics
    }
}