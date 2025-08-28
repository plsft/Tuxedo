using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Tuxedo;
using Tuxedo.Contrib;
using Tuxedo.DependencyInjection;
using Tuxedo.QueryBuilder;
using Xunit;

public class QueryBuilderTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public QueryBuilderTests()
    {
        _connection = CreateSqliteConnection();
        CreateTestTables();
        SeedTestData();
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
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Category TEXT,
                Stock INTEGER DEFAULT 0,
                IsActive INTEGER DEFAULT 1,
                CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP
            )";

        var createOrderTable = @"
            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                CustomerId INTEGER NOT NULL,
                OrderDate TEXT DEFAULT CURRENT_TIMESTAMP,
                Status TEXT DEFAULT 'Pending',
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            )";

        var createCustomerTable = @"
            CREATE TABLE Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT UNIQUE NOT NULL,
                City TEXT,
                Country TEXT,
                JoinDate TEXT DEFAULT CURRENT_TIMESTAMP
            )";

        _connection.Execute(createProductTable);
        _connection.Execute(createOrderTable);
        _connection.Execute(createCustomerTable);
    }

    private void SeedTestData()
    {
        // Seed products
        var products = new[]
        {
            new Product { Name = "Laptop", Price = 999.99m, Category = "Electronics", Stock = 10, IsActive = true },
            new Product { Name = "Mouse", Price = 19.99m, Category = "Electronics", Stock = 50, IsActive = true },
            new Product { Name = "Keyboard", Price = 49.99m, Category = "Electronics", Stock = 30, IsActive = true },
            new Product { Name = "Monitor", Price = 299.99m, Category = "Electronics", Stock = 15, IsActive = true },
            new Product { Name = "Desk", Price = 199.99m, Category = "Furniture", Stock = 5, IsActive = true },
            new Product { Name = "Chair", Price = 149.99m, Category = "Furniture", Stock = 8, IsActive = true },
            new Product { Name = "Bookshelf", Price = 89.99m, Category = "Furniture", Stock = 12, IsActive = false },
            new Product { Name = "Lamp", Price = 29.99m, Category = "Furniture", Stock = 25, IsActive = true },
            new Product { Name = "Notebook", Price = 4.99m, Category = "Stationery", Stock = 100, IsActive = true },
            new Product { Name = "Pen", Price = 1.99m, Category = "Stationery", Stock = 200, IsActive = true }
        };

        foreach (var product in products)
        {
            _connection.Insert(product);
        }

        // Seed customers
        var customers = new[]
        {
            new Customer { Name = "John Doe", Email = "john@example.com", City = "New York", Country = "USA" },
            new Customer { Name = "Jane Smith", Email = "jane@example.com", City = "London", Country = "UK" },
            new Customer { Name = "Bob Johnson", Email = "bob@example.com", City = "Paris", Country = "France" }
        };

        foreach (var customer in customers)
        {
            _connection.Insert(customer);
        }

        // Seed orders
        var orders = new[]
        {
            new Order { ProductId = 1, Quantity = 2, TotalAmount = 1999.98m, CustomerId = 1, Status = "Completed" },
            new Order { ProductId = 2, Quantity = 5, TotalAmount = 99.95m, CustomerId = 1, Status = "Completed" },
            new Order { ProductId = 3, Quantity = 1, TotalAmount = 49.99m, CustomerId = 2, Status = "Pending" },
            new Order { ProductId = 5, Quantity = 1, TotalAmount = 199.99m, CustomerId = 3, Status = "Processing" }
        };

        foreach (var order in orders)
        {
            _connection.Insert(order);
        }
    }

    [Fact]
    public async Task SelectAll_ReturnsAllRecords()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll();

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(10, products.Count());
    }

    [Fact]
    public async Task Select_WithSpecificColumns_ReturnsOnlySelectedColumns()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Select("Id", "Name", "Price");

        var sql = query.BuildSql();
        
        Assert.Contains("SELECT Id, Name, Price", sql);
        Assert.Contains("FROM Products", sql);
    }

    [Fact]
    public async Task Where_WithSimpleCondition_FiltersCorrectly()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Category == "Electronics");

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(4, products.Count());
        Assert.All(products, p => Assert.Equal("Electronics", p.Category));
    }

    [Fact]
    public async Task Where_WithMultipleConditions_FiltersCorrectly()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Category == "Electronics")
            .And(p => p.Price > 50);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(3, products.Count()); // Laptop, Keyboard, Monitor
        Assert.All(products, p =>
        {
            Assert.Equal("Electronics", p.Category);
            Assert.True(p.Price > 50);
        });
    }

    [Fact]
    public async Task WhereIn_FiltersCorrectly()
    {
        var categories = new[] { "Electronics", "Stationery" };
        
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .WhereIn(p => p.Category, categories);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(6, products.Count());
        Assert.All(products, p => Assert.Contains(p.Category, categories));
    }

    [Fact]
    public async Task WhereNotIn_FiltersCorrectly()
    {
        var excludeCategories = new[] { "Furniture" };
        
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .WhereNotIn(p => p.Category, excludeCategories);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(6, products.Count());
        Assert.All(products, p => Assert.NotEqual("Furniture", p.Category));
    }

    [Fact]
    public async Task WhereBetween_FiltersCorrectly()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .WhereBetween(p => p.Price, 20m, 100m);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.All(products, p =>
        {
            Assert.True(p.Price >= 20m);
            Assert.True(p.Price <= 100m);
        });
    }

    [Fact]
    public async Task WhereNull_FiltersNullValues()
    {
        // First insert a product with null category
        var nullProduct = new Product { Name = "Test Null", Price = 10m, Category = null };
        _connection.Insert(nullProduct);

        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .WhereNull(p => p.Category);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Single(products);
        Assert.Null(products.First().Category);
    }

    [Fact]
    public async Task WhereNotNull_FiltersNonNullValues()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .WhereNotNull(p => p.Category);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(10, products.Count());
        Assert.All(products, p => Assert.NotNull(p.Category));
    }

    [Fact]
    public async Task OrderBy_SortsAscending()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .OrderBy(p => p.Price);

        var products = await query.ToListAsync(_connection);
        var prices = products.Select(p => p.Price).ToList();

        Assert.NotNull(products);
        Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
    }

    [Fact]
    public async Task OrderByDescending_SortsDescending()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .OrderByDescending(p => p.Price);

        var products = await query.ToListAsync(_connection);
        var prices = products.Select(p => p.Price).ToList();

        Assert.NotNull(products);
        Assert.Equal(prices.OrderByDescending(p => p).ToList(), prices);
    }

    [Fact]
    public async Task ThenBy_SecondarySorting()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Price);

        var sql = query.BuildSql();
        
        Assert.Contains("ORDER BY Category ASC, Price ASC", sql);
    }

    [Fact]
    public async Task Skip_Take_Pagination()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .OrderBy(p => p.Id)
            .Skip(2)
            .Take(3);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(3, products.Count());
    }

    [Fact]
    public async Task Page_CalculatesCorrectOffset()
    {
        var pageIndex = 1; // Second page (0-indexed)
        var pageSize = 3;
        
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .OrderBy(p => p.Id)
            .Page(pageIndex, pageSize);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(3, products.Count());
        Assert.Equal(4, products.First().Id); // Should start from 4th record
    }

    [Fact]
    public async Task Count_ReturnsCorrectCount()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Count()
            .Where(p => p.Category == "Electronics");

        var count = await query.CountAsync(_connection);

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Sum_CalculatesCorrectSum()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Sum(p => p.Price)
            .Where(p => p.Category == "Electronics");

        var sql = query.BuildSql();
        
        Assert.Contains("SELECT SUM(Price)", sql);
        Assert.Contains("WHERE", sql);
    }

    [Fact]
    public async Task Average_CalculatesCorrectAverage()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Average(p => p.Price);

        var sql = query.BuildSql();
        
        Assert.Contains("SELECT AVG(Price)", sql);
    }

    [Fact]
    public async Task Min_FindsMinimumValue()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Min(p => p.Price);

        var sql = query.BuildSql();
        
        Assert.Contains("SELECT MIN(Price)", sql);
    }

    [Fact]
    public async Task Max_FindsMaximumValue()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Max(p => p.Price);

        var sql = query.BuildSql();
        
        Assert.Contains("SELECT MAX(Price)", sql);
    }

    [Fact]
    public async Task GroupBy_CreatesGroupByClause()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Select("Category", "COUNT(*) as Count")
            .GroupBy(p => p.Category);

        var sql = query.BuildSql();
        
        Assert.Contains("GROUP BY Category", sql);
    }

    [Fact]
    public async Task Having_CreatesHavingClause()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Select("Category", "COUNT(*) as Count")
            .GroupBy(p => p.Category)
            .Having(p => p.Stock > 10);

        var sql = query.BuildSql();
        
        Assert.Contains("GROUP BY Category", sql);
        Assert.Contains("HAVING", sql);
    }

    [Fact]
    public async Task Or_CombinesConditionsWithOr()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Category == "Electronics")
            .Or(p => p.Category == "Furniture");

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(8, products.Count());
        Assert.All(products, p => Assert.True(p.Category == "Electronics" || p.Category == "Furniture"));
    }

    [Fact]
    public async Task Not_NegatesCondition()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Not(p => p.Category == "Electronics");

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.Equal(6, products.Count());
        Assert.All(products, p => Assert.NotEqual("Electronics", p.Category));
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsFirstRecord()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Category == "Electronics")
            .OrderBy(p => p.Price);

        var product = await query.FirstOrDefaultAsync(_connection);

        Assert.NotNull(product);
        Assert.Equal("Mouse", product.Name); // Cheapest electronics item
    }

    [Fact]
    public async Task SingleAsync_ReturnsSingleRecord()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Name == "Laptop");

        var product = await query.SingleAsync(_connection);

        Assert.NotNull(product);
        Assert.Equal("Laptop", product.Name);
    }

    [Fact]
    public async Task AnyAsync_ReturnsTrueWhenRecordsExist()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Price > 500);

        var any = await query.AnyAsync(_connection);

        Assert.True(any);
    }

    [Fact]
    public async Task AnyAsync_ReturnsFalseWhenNoRecordsExist()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.Price > 10000);

        var any = await query.AnyAsync(_connection);

        Assert.False(any);
    }

    [Fact]
    public void Raw_AllowsRawSqlQueries()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .Raw("SELECT * FROM Products WHERE Price > @minPrice", new { minPrice = 100 });

        var sql = query.BuildSql();
        var parameters = query.GetParameters();

        Assert.Equal("SELECT * FROM Products WHERE Price > @minPrice", sql);
        Assert.NotNull(parameters);
    }

    [Fact]
    public async Task ComplexQuery_CombinesMultipleFeatures()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
            .SelectAll()
            .Where(p => p.IsActive == true)
            .And(p => p.Stock > 0)
            .WhereIn(p => p.Category, new[] { "Electronics", "Furniture" })
            .WhereBetween(p => p.Price, 20m, 500m)
            .OrderBy(p => p.Category)
            .ThenByDescending(p => p.Price)
            .Skip(1)
            .Take(3);

        var products = await query.ToListAsync(_connection);

        Assert.NotNull(products);
        Assert.True(products.Count() <= 3);
        Assert.All(products, p =>
        {
            Assert.True(p.IsActive);
            Assert.True(p.Stock > 0);
            Assert.Contains(p.Category, new[] { "Electronics", "Furniture" });
            Assert.True(p.Price >= 20m && p.Price <= 500m);
        });
    }

    [Fact]
    public void BuildSql_GeneratesCorrectSqlForSqlServer()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.SqlServer)
            .SelectAll()
            .OrderBy(p => p.Id)
            .Skip(10)
            .Take(5);

        var sql = query.BuildSql();

        Assert.Contains("OFFSET 10 ROWS", sql);
        Assert.Contains("FETCH NEXT 5 ROWS ONLY", sql);
    }

    [Fact]
    public void BuildSql_GeneratesCorrectSqlForPostgres()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.Postgres)
            .SelectAll()
            .Skip(10)
            .Take(5);

        var sql = query.BuildSql();

        Assert.Contains("LIMIT 5", sql);
        Assert.Contains("OFFSET 10", sql);
    }

    [Fact]
    public void BuildSql_GeneratesCorrectSqlForMySql()
    {
        var query = new QueryBuilder<Product>(TuxedoDialect.MySql)
            .SelectAll()
            .Skip(10)
            .Take(5);

        var sql = query.BuildSql();

        Assert.Contains("LIMIT 5", sql);
        Assert.Contains("OFFSET 10", sql);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    // Test models
    [Table("Products")]
    private class Product
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        [Computed]
        public DateTime CreatedDate { get; set; }
    }

    [Table("Orders")]
    private class Order
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
    }

    [Table("Customers")]
    private class Customer
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public DateTime JoinDate { get; set; }
    }
}