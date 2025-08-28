# Tuxedo

Tuxedo merges Dapper and Dapper.Contrib functionality into a single package for modern .NET. It provides high‑performance ADO.NET mapping and CRUD helpers that work with SQL Server, PostgreSQL, MySQL, and SQLite.

## Features

- High performance query/execute APIs (`Query`, `Execute`, async variants)
- **Dynamic Query Builder**: Fluent LINQ-style query building with expression support
- Contrib‑style CRUD: `Get`/`GetAll`, `Insert`, `Update`, `Delete` (+ async)
- **Partial Updates**: Enhanced `Update`/`UpdateAsync` methods support selective column updates
- **Bulk Operations**: High-performance bulk insert, update, delete, and merge operations
- **Resiliency**: Built-in retry policies and circuit breakers with Polly integration
- **Diagnostics & Monitoring**: Comprehensive event tracking, health checks, and performance analysis
- Attribute mapping: `[Table]`, `[Key]`, `[ExplicitKey]`, `[Computed]`
- Works with any ADO.NET provider (SqlClient, Npgsql, MySqlConnector, Sqlite)
- Optional DI helpers for registering a connection, including in‑memory SQLite

## Installation

- Library: `dotnet add package Tuxedo`
- Providers as needed: `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`

## Quick Start

```csharp
using System.Data;
using Tuxedo;              // Dapper APIs
using Tuxedo.Contrib;      // CRUD helpers
using Tuxedo.QueryBuilder; // Dynamic query building
```

### Configuration-based helpers

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tuxedo.DependencyInjection;

// Build configuration (example uses in-memory for simplicity)
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["TuxedoSqlServer:ConnectionString"] = "Server=.;Database=Demo;Integrated Security=true;TrustServerCertificate=true;",
        ["TuxedoSqlServer:MultipleActiveResultSets"] = "true",
        ["TuxedoSqlServer:ConnectTimeout"] = "60",

        ["TuxedoPostgres:ConnectionString"] = "Host=localhost;Database=demo;Username=demo;Password=pass",
        ["TuxedoPostgres:Pooling"] = "true",
        ["TuxedoPostgres:MinPoolSize"] = "5",
        ["TuxedoPostgres:MaxPoolSize"] = "100",

        ["TuxedoMySql:ConnectionString"] = "Server=localhost;Database=demo;Uid=demo;Pwd=pass;",
        ["TuxedoMySql:AllowUserVariables"] = "true",
        ["TuxedoMySql:UseCompression"] = "true",
        ["TuxedoMySql:ConnectionLifeTime"] = "300",
        ["TuxedoMySql:CommandTimeout"] = "90"
    })
    .Build();

var services = new ServiceCollection();
services.AddTuxedoSqlServerWithOptions(config);         // section "TuxedoSqlServer"
// services.AddTuxedoPostgresWithOptions(config);       // section "TuxedoPostgres"
// services.AddTuxedoMySqlWithOptions(config);          // section "TuxedoMySql"

// Or use a custom section name
services.AddTuxedoPostgresWithOptions(config, sectionName: "CustomPostgres");
```
### Model

```csharp
using Tuxedo.Contrib;

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Category { get; set; }
    [Computed]
    public DateTime LastModified { get; set; }
}
```

### Using a connection

```csharp
using Microsoft.Data.SqlClient;   // or Npgsql/MySqlConnector/Microsoft.Data.Sqlite

var cs = "Server=localhost;Database=Demo;Integrated Security=true;TrustServerCertificate=true";
using IDbConnection db = new SqlConnection(cs);
db.Open();

// Dapper query
var products = db.Query<Product>("SELECT * FROM Products WHERE Category = @cat", new { cat = "Books" });

// Contrib CRUD (sync)
var id = db.Insert(new Product { Name = "Gizmo", Price = 19.99m, Category = "Gadgets" });
var p  = db.Get<Product>(id);
p.Price = 17.99m;
db.Update(p);
db.Delete(p);

// Contrib CRUD (async)
var newId = await db.InsertAsync(new Product { Name = "Async", Price = 10m });
var item  = await db.SelectAsync<Product>(newId); // alias for GetAsync
await db.UpdateAsync(item);
await db.DeleteAsync(item);

// Partial Updates (sync)
var product = db.Get<Product>(id);
product.Name = "Updated Name";
product.Price = 25.99m; // This won't be updated
db.Update(product, propertiesToUpdate: new[] { "Name" }); // Only updates Name column

// Partial Updates with key/value objects
db.Update<Product>(
    keyValues: new { Id = id },
    updateValues: new { Name = "New Name", Price = 29.99m }
);

// Partial Updates (async)
await db.UpdateAsync(product, propertiesToUpdate: new[] { "Name", "Category" });

// Dynamic Query Builder
var expensiveProducts = await QueryBuilder.Query<Product>()
    .Where(p => p.Price > 100)
    .Where(p => p.Category == "Electronics")
    .OrderByDescending(p => p.Price)
    .Take(10)
    .ToListAsync(db);

// Complex queries with joins and aggregations
var categoryCount = await QueryBuilder.Query<Product>()
    .Where(p => p.Active == true)
    .GroupBy(p => p.Category)
    .CountAsync(db);
```

## Partial Updates

Tuxedo supports efficient partial updates that only modify specified columns using the same `Update`/`UpdateAsync` methods:

```csharp
// Update only specific properties by name
var product = db.Get<Product>(1);
product.Name = "Updated Product";
product.LastModified = DateTime.Now;
// Price remains unchanged in database
db.Update(product, propertiesToUpdate: new[] { "Name", "LastModified" });

// Update using separate key and value objects
db.Update<Product>(
    keyValues: new { Id = 1 },
    updateValues: new { Name = "Direct Update", Price = 39.99m }
);

// Async versions available
await db.UpdateAsync(product, propertiesToUpdate: new[] { "Name" });
await db.UpdateAsync<Product>(
    keyValues: new { Id = 1 },
    updateValues: new { Category = "Updated Category" }
);
```

**Partial Update Features:**
- Uses the same `Update`/`UpdateAsync` methods with optional parameters
- Updates only specified columns, leaving others unchanged
- Supports both property name arrays and key/value object patterns
- Case-insensitive property name matching
- Validates property names and prevents key column updates
- Ignores `[Computed]` properties automatically
- Backward compatible - existing `Update` calls continue to work unchanged

## Dynamic Query Builder

Tuxedo includes a powerful fluent query builder with LINQ-style expressions and multi-database dialect support:

```csharp
using Tuxedo.QueryBuilder;

// Basic query building
var query = QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .Where(p => p.Price > 100)
    .OrderBy(p => p.Name)
    .Skip(10)
    .Take(20);

var products = await query.ToListAsync(db);
```

### WHERE Clause Operations

The QueryBuilder supports comprehensive WHERE conditions with full expression tree support:

```csharp
// Basic equality and comparison
var query1 = QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .Where(p => p.Price > 100);

// Complex boolean expressions
var query2 = QueryBuilder.Query<Product>()
    .Where(p => p.Price > 50 && p.Price < 500)
    .Where(p => p.InStock == true || p.PreOrder == true);

// IN and NOT IN operations
var categoryIds = new[] { 1, 2, 3, 4, 5 };
var query3 = QueryBuilder.Query<Product>()
    .WhereIn(p => p.CategoryId, categoryIds)
    .WhereNotIn(p => p.StatusId, new[] { 99, 100 });

// BETWEEN ranges
var query4 = QueryBuilder.Query<Product>()
    .WhereBetween(p => p.Price, 10.00m, 99.99m)
    .WhereBetween(p => p.CreatedDate, startDate, endDate);

// NULL checks
var query5 = QueryBuilder.Query<Product>()
    .WhereNotNull(p => p.Description)
    .WhereNull(p => p.DeletedAt);

// Combining with OR logic
var query6 = QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .Or(p => p.Featured == true)
    .Or(p => p.Price < 20);

// Using NOT for negation
var query7 = QueryBuilder.Query<Product>()
    .Not(p => p.Category == "Discontinued")
    .Not(p => p.Price > 1000);

// Raw SQL conditions when needed
var query8 = QueryBuilder.Query<Product>()
    .Where("Price * Quantity > @threshold", new { threshold = 1000 })
    .Where("LOWER(Name) LIKE @pattern", new { pattern = "%phone%" });
```

### JOIN Operations

Support for INNER, LEFT, and RIGHT joins with type-safe expressions:

```csharp
// INNER JOIN
var innerJoin = QueryBuilder.Query<Product>()
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .Where(p => p.Price > 100)
    .Select("p.*, c.Name as CategoryName");

// LEFT JOIN (include products even without categories)
var leftJoin = QueryBuilder.Query<Product>()
    .LeftJoin<Category>((p, c) => p.CategoryId == c.Id)
    .Where(p => p.Active == true)
    .OrderBy(p => p.Name);

// RIGHT JOIN (include all categories even without products)
var rightJoin = QueryBuilder.Query<Category>()
    .RightJoin<Product>((c, p) => c.Id == p.CategoryId)
    .GroupBy(c => c.Name)
    .Select("c.Name, COUNT(p.Id) as ProductCount");

// Multiple JOINs
var multiJoin = QueryBuilder.Query<Order>()
    .InnerJoin<Customer>((o, c) => o.CustomerId == c.Id)
    .InnerJoin<Product>((o, p) => o.ProductId == p.Id)
    .Where(o => o.OrderDate > DateTime.Today.AddDays(-30))
    .Select("o.*, c.Name as CustomerName, p.Name as ProductName");

// Complex JOIN with additional conditions
var complexJoin = QueryBuilder.Query<Product>()
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .InnerJoin<Supplier>((p, s) => p.SupplierId == s.Id)
    .Where(p => p.Price > 50)
    .Where(c => c.Active == true)
    .OrderBy(p => p.Name);
```

### Aggregations and Grouping

```csharp
// COUNT operations
var totalProducts = await QueryBuilder.Query<Product>()
    .CountAsync(db);

var activeCount = await QueryBuilder.Query<Product>()
    .Where(p => p.Active == true)
    .CountAsync(db);

var categoryCount = await QueryBuilder.Query<Product>()
    .Count(p => p.CategoryId)  // COUNT(CategoryId)
    .ToListAsync(db);

// SUM operations
var totalRevenue = QueryBuilder.Query<Order>()
    .Where(o => o.OrderDate >= DateTime.Today.AddMonths(-1))
    .Sum(o => o.TotalAmount);

// AVERAGE operations
var avgPrice = QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .Average(p => p.Price);

// MIN/MAX operations
var cheapestPrice = QueryBuilder.Query<Product>()
    .Where(p => p.InStock == true)
    .Min(p => p.Price);

var mostExpensive = QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Luxury")
    .Max(p => p.Price);

// GROUP BY with HAVING
var categoryStats = QueryBuilder.Query<Product>()
    .GroupBy(p => p.Category)
    .Having(p => p.Price > 50)
    .Select("Category, COUNT(*) as ProductCount, AVG(Price) as AvgPrice, MAX(Price) as MaxPrice");

// Multiple GROUP BY columns
var salesByRegionAndCategory = QueryBuilder.Query<Sale>()
    .GroupBy(s => s.Region, s => s.ProductCategory)
    .Having(s => s.Amount > 1000)
    .Select("Region, ProductCategory, SUM(Amount) as TotalSales, COUNT(*) as SaleCount")
    .OrderByDescending(s => s.TotalSales);

// Complex aggregation with JOINs
var categoryPerformance = QueryBuilder.Query<Product>()
    .InnerJoin<OrderItem>((p, oi) => p.Id == oi.ProductId)
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .GroupBy(c => c.Name)
    .Select(@"c.Name as CategoryName, 
              COUNT(DISTINCT p.Id) as UniqueProducts,
              SUM(oi.Quantity) as TotalSold,
              AVG(oi.Price) as AvgSellingPrice")
    .OrderByDescending(x => x.TotalSold);
```

### Pagination

Full support for pagination with dialect-specific SQL generation:

```csharp
// Basic Skip and Take
var page = await QueryBuilder.Query<Product>()
    .Where(p => p.Active == true)
    .OrderBy(p => p.Name)
    .Skip(20)      // Skip first 20 records
    .Take(10)      // Take next 10 records
    .ToListAsync(db);

// Page-based pagination (convenience method)
var pagedResults = await QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .OrderByDescending(p => p.CreatedDate)
    .Page(pageIndex: 2, pageSize: 25)  // Gets page 3 (0-indexed), 25 items per page
    .ToListAsync(db);

// First N records only
var top10 = await QueryBuilder.Query<Product>()
    .Where(p => p.Featured == true)
    .OrderByDescending(p => p.Rating)
    .Take(10)
    .ToListAsync(db);

// Skip without Take (returns all remaining)
var afterFirst100 = await QueryBuilder.Query<Product>()
    .OrderBy(p => p.Id)
    .Skip(100)
    .ToListAsync(db);

// Combining with complex queries
var complexPaged = await QueryBuilder.Query<Product>()
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .Where(p => p.Price > 50)
    .Where(c => c.Active == true)
    .OrderByDescending(p => p.CreatedDate)
    .ThenBy(p => p.Name)
    .Page(1, 20)  // Page 2, 20 items
    .ToListAsync(db);
```

#### Dialect-Specific SQL Generation

```csharp
// SQL Server (uses OFFSET/FETCH)
var sqlServerQuery = new QueryBuilder<Product>(TuxedoDialect.SqlServer)
    .OrderBy(p => p.Name)
    .Skip(20).Take(10);
// Generates: ORDER BY Name ASC OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY

// PostgreSQL (uses LIMIT/OFFSET)
var postgresQuery = new QueryBuilder<Product>(TuxedoDialect.Postgres)
    .OrderBy(p => p.Name)
    .Skip(20).Take(10);
// Generates: ORDER BY Name ASC LIMIT 10 OFFSET 20

// MySQL (uses LIMIT/OFFSET)
var mysqlQuery = new QueryBuilder<Product>(TuxedoDialect.MySql)
    .OrderBy(p => p.Name)
    .Skip(20).Take(10);
// Generates: ORDER BY Name ASC LIMIT 10 OFFSET 20

// SQLite (uses LIMIT/OFFSET)
var sqliteQuery = new QueryBuilder<Product>(TuxedoDialect.Sqlite)
    .OrderBy(p => p.Name)
    .Skip(20).Take(10);
// Generates: ORDER BY Name ASC LIMIT 10 OFFSET 20
```

### Sorting and Ordering

```csharp
// Simple ordering
var sorted1 = QueryBuilder.Query<Product>()
    .OrderBy(p => p.Name);

var sorted2 = QueryBuilder.Query<Product>()
    .OrderByDescending(p => p.Price);

// Multiple sort columns with ThenBy
var multiSort = QueryBuilder.Query<Product>()
    .OrderBy(p => p.Category)
    .ThenByDescending(p => p.Price)
    .ThenBy(p => p.Name);

// Combining with other operations
var complexSort = QueryBuilder.Query<Product>()
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .Where(p => p.Active == true)
    .OrderBy(c => c.Name)           // Sort by category name first
    .ThenByDescending(p => p.Price)  // Then by price descending
    .ThenBy(p => p.Name)             // Finally by product name
    .Take(50);
```

### SQL Generation and Inspection

The QueryBuilder allows you to build and inspect SQL before execution:

```csharp
// Build complex query
var builder = QueryBuilder.Query<Product>()
    .InnerJoin<Category>((p, c) => p.CategoryId == c.Id)
    .Where(p => p.Price > 100)
    .Where(p => p.Active == true)
    .WhereIn(p => p.StatusId, new[] { 1, 2, 3 })
    .OrderByDescending(p => p.CreatedDate)
    .Skip(10)
    .Take(20);

// Get generated SQL
string sql = builder.BuildSql();
// Result: SELECT * FROM Products 
//         INNER JOIN Categories ON Products.CategoryId = Categories.Id
//         WHERE Price > @p1 AND Active = @p2 AND StatusId IN (@p3, @p4, @p5)
//         ORDER BY CreatedDate DESC
//         LIMIT 20 OFFSET 10

// Get parameters dictionary
object parameters = builder.GetParameters();
// Result: { p1: 100, p2: true, p3: 1, p4: 2, p5: 3 }

// Execute manually with Dapper/Tuxedo
var results = await db.QueryAsync<Product>(sql, parameters);

// Or use built-in execution
var directResults = await builder.ToListAsync(db);
```

### SELECT Projections

Control what columns are selected:

```csharp
// Select all columns (default)
var all = QueryBuilder.Query<Product>()
    .SelectAll();

// Select specific columns by name
var specific = QueryBuilder.Query<Product>()
    .Select("Id", "Name", "Price");

// Select with expressions
var withExpression = QueryBuilder.Query<Product>()
    .Select(p => new { p.Id, p.Name, p.Price });

// Select with calculations and aliases
var calculated = QueryBuilder.Query<Product>()
    .Select(@"Id, Name, Price, 
              Price * 1.1 as PriceWithTax,
              CASE WHEN Price > 100 THEN 'Expensive' ELSE 'Affordable' END as PriceCategory");

// Select with aggregate functions
var aggregated = QueryBuilder.Query<Product>()
    .GroupBy(p => p.Category)
    .Select("Category, COUNT(*) as Count, AVG(Price) as AvgPrice, MAX(Price) as MaxPrice");
```

### Execution Methods

Multiple ways to execute and retrieve data:

```csharp
// Get list of results
var products = await QueryBuilder.Query<Product>()
    .Where(p => p.Active == true)
    .ToListAsync(db);

// Get first or default
var firstProduct = await QueryBuilder.Query<Product>()
    .Where(p => p.Featured == true)
    .OrderByDescending(p => p.Rating)
    .FirstOrDefaultAsync(db);

// Get single result (throws if multiple)
var singleProduct = await QueryBuilder.Query<Product>()
    .Where(p => p.Id == 123)
    .SingleAsync(db);

// Get count
var count = await QueryBuilder.Query<Product>()
    .Where(p => p.Category == "Electronics")
    .CountAsync(db);

// Check if any exist
var hasProducts = await QueryBuilder.Query<Product>()
    .Where(p => p.Price < 10)
    .AnyAsync(db);
```

### Raw SQL Integration

When you need complete control:

```csharp
// Use raw SQL for complex queries
var rawQuery = QueryBuilder.Query<Product>()
    .Raw(@"SELECT p.*, c.Name as CategoryName
           FROM Products p
           LEFT JOIN Categories c ON p.CategoryId = c.Id
           WHERE p.Price > @minPrice
           AND EXISTS (
               SELECT 1 FROM OrderItems oi 
               WHERE oi.ProductId = p.Id 
               AND oi.OrderDate > @startDate
           )", new { minPrice = 100, startDate = DateTime.Today.AddMonths(-1) });

var results = await rawQuery.ToListAsync(db);

// Combine raw WHERE conditions with fluent API
var mixed = QueryBuilder.Query<Product>()
    .Where(p => p.Active == true)
    .Where("LOWER(Name) LIKE @pattern", new { pattern = "%phone%" })
    .Where("Price BETWEEN @min AND @max", new { min = 100, max = 1000 })
    .OrderBy(p => p.Name);
```

### Advanced Scenarios

```csharp
// Subqueries using raw SQL
var withSubquery = QueryBuilder.Query<Product>()
    .Where(@"CategoryId IN (
        SELECT Id FROM Categories 
        WHERE Active = 1 AND ParentId = @parentId
    )", new { parentId = 5 });

// Complex business logic
var businessQuery = QueryBuilder.Query<Order>()
    .InnerJoin<Customer>((o, c) => o.CustomerId == c.Id)
    .Where(o => o.Status == "Completed")
    .Where(o => o.TotalAmount > 1000)
    .Where(c => c.LoyaltyTier == "Gold")
    .WhereBetween(o => o.OrderDate, startOfMonth, endOfMonth)
    .GroupBy(c => c.Region)
    .Having(o => o.TotalAmount > 5000)
    .Select(@"c.Region,
              COUNT(DISTINCT o.CustomerId) as UniqueCustomers,
              COUNT(o.Id) as OrderCount,
              SUM(o.TotalAmount) as Revenue,
              AVG(o.TotalAmount) as AvgOrderValue")
    .OrderByDescending(x => x.Revenue);

// Dynamic query building
var dynamicQuery = QueryBuilder.Query<Product>();

if (!string.IsNullOrEmpty(searchTerm))
    dynamicQuery.Where(p => p.Name.Contains(searchTerm));

if (minPrice.HasValue)
    dynamicQuery.Where(p => p.Price >= minPrice.Value);

if (maxPrice.HasValue)
    dynamicQuery.Where(p => p.Price <= maxPrice.Value);

if (categoryIds?.Any() == true)
    dynamicQuery.WhereIn(p => p.CategoryId, categoryIds);

dynamicQuery.OrderBy(p => p.Name)
            .Page(pageIndex, pageSize);

var results = await dynamicQuery.ToListAsync(db);
```

## Query Builder Feature Summary

**Core Capabilities:**
- ✅ Fluent API with method chaining
- ✅ Expression tree to SQL conversion
- ✅ Type-safe column references
- ✅ Parameterized queries (SQL injection safe)
- ✅ Multi-database dialect support (SQL Server, PostgreSQL, MySQL, SQLite)

**WHERE Operations:**
- ✅ Complex boolean expressions (AND, OR, NOT)
- ✅ IN and NOT IN clauses
- ✅ BETWEEN ranges
- ✅ NULL/NOT NULL checks
- ✅ Raw SQL conditions

**JOIN Support:**
- ✅ INNER, LEFT, and RIGHT joins
- ✅ Multiple joins in single query
- ✅ Type-safe join conditions

**Aggregations:**
- ✅ COUNT, SUM, AVG, MIN, MAX
- ✅ GROUP BY with multiple columns
- ✅ HAVING clauses

**Pagination & Sorting:**
- ✅ Skip/Take operations
- ✅ Page-based navigation
- ✅ OrderBy/ThenBy chaining
- ✅ Dialect-specific SQL generation

**Execution:**
- ✅ Async-first design
- ✅ Multiple result types (List, Single, First, Count, Any)
- ✅ SQL inspection before execution
- ✅ Raw SQL integration

## Resiliency and Fault Tolerance

Tuxedo provides enterprise-grade resiliency features powered by Polly, including automatic retries, circuit breakers, and timeout handling for transient database failures.

### Basic Resiliency Setup

```csharp
using Tuxedo.Resiliency;
using Tuxedo.DependencyInjection;

// Method 1: Configure with options
services.AddTuxedoResiliency(options =>
{
    options.MaxRetryAttempts = 3;
    options.BaseDelay = TimeSpan.FromSeconds(1);
    options.EnableCircuitBreaker = true;
    options.CircuitBreakerThreshold = 5;
    options.CircuitBreakerTimeout = TimeSpan.FromSeconds(30);
});

// Method 2: Configure from appsettings.json
services.AddTuxedoResiliency(configuration, "TuxedoResiliency");

// Method 3: Use default settings (3 retries with exponential backoff)
services.AddTuxedoResiliency();
```

### Configuration Options

```json
// appsettings.json
{
  "TuxedoResiliency": {
    "MaxRetryAttempts": 3,
    "BaseDelay": "00:00:01",
    "EnableCircuitBreaker": true,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerTimeout": "00:00:30"
  }
}
```

### Automatic Resiliency with Dependency Injection

When resiliency is configured, all `IDbConnection` instances are automatically wrapped with retry policies:

```csharp
// Configure services
services.AddTuxedoSqlServer(connectionString);
services.AddTuxedoResiliency(options =>
{
    options.MaxRetryAttempts = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(500);
});

// Use in your service - retries are automatic
public class ProductService
{
    private readonly IDbConnection _db;
    
    public ProductService(IDbConnection db) => _db = db;
    
    public async Task<Product> GetProductAsync(int id)
    {
        // This query will automatically retry on transient failures
        return await _db.GetAsync<Product>(id);
    }
    
    public async Task<IEnumerable<Product>> SearchProductsAsync(string term)
    {
        // Complex queries also benefit from automatic retries
        return await _db.QueryAsync<Product>(
            "SELECT * FROM Products WHERE Name LIKE @term",
            new { term = $"%{term}%" });
    }
}
```

### Manual Retry Execution

For fine-grained control, use the extension methods directly:

```csharp
// Async with retry
var products = await connection.ExecuteWithRetryAsync(
    async conn => await conn.QueryAsync<Product>("SELECT * FROM Products"),
    retryPolicy: new ExponentialBackoffRetryPolicy(maxAttempts: 5)
);

// Sync with retry
var count = connection.ExecuteWithRetry(
    conn => conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Products"),
    retryPolicy: new ExponentialBackoffRetryPolicy()
);

// Custom retry logic for specific operations
var result = await connection.ExecuteWithRetryAsync(
    async conn =>
    {
        using var transaction = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync("UPDATE Inventory SET Quantity = Quantity - @qty WHERE ProductId = @id",
                new { qty = 1, id = productId }, transaction);
            
            await conn.ExecuteAsync("INSERT INTO Orders (ProductId, Quantity) VALUES (@id, @qty)",
                new { id = productId, qty = 1 }, transaction);
            
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
);
```

### Using Polly Provider Directly

For advanced scenarios, use the `PollyResiliencyProvider` with full Polly features:

```csharp
// Register with DI
services.AddSingleton<IResiliencyProvider, PollyResiliencyProvider>();

// Use in your code
public class ResilientRepository
{
    private readonly IDbConnection _connection;
    private readonly IResiliencyProvider _resiliency;
    
    public ResilientRepository(IDbConnection connection, IResiliencyProvider resiliency)
    {
        _connection = connection;
        _resiliency = resiliency;
    }
    
    public async Task<T> GetWithResiliencyAsync<T>(int id) where T : class
    {
        // Wrap connection with resiliency
        using var resilientConnection = _resiliency.WrapConnection(_connection);
        
        // All operations through this connection have retry/circuit breaker
        return await resilientConnection.GetAsync<T>(id);
    }
    
    public async Task<IEnumerable<T>> QueryWithFallbackAsync<T>(string sql, object param)
    {
        try
        {
            // Primary query with resiliency
            return await _resiliency.ExecuteAsync(
                async () => await _connection.QueryAsync<T>(sql, param)
            );
        }
        catch (Exception ex) when (IsCircuitOpen(ex))
        {
            // Fallback to cache or alternative data source
            return GetFromCache<T>() ?? Enumerable.Empty<T>();
        }
    }
}
```

### Transient Error Detection

Tuxedo automatically detects and retries transient errors for all major databases:

**SQL Server Transient Errors:**
- Connection timeouts (Error: -2, 121)
- Deadlocks (Error: 1205)
- Database unavailable (Error: 4060)
- Resource throttling (Error: 49918, 49919, 49920)
- Network issues (Error: 10053, 10054, 10060, 10061)

**PostgreSQL Transient Errors:**
- Connection failures (SQLSTATE: 08000, 08001, 08003, 08004, 08006)
- Serialization failures (SQLSTATE: 40001, 40P01)
- System errors (SQLSTATE: 57P01, 57P02, 57P03, 58000, 58030)

**MySQL Transient Errors:**
- Deadlocks (Error: 1213)
- Lock wait timeouts (Error: 1205)
- Too many connections (Error: 1040, 1041)
- Lost connection (Error: 2002, 2003, 2006, 2013)

### Circuit Breaker Pattern

Prevent cascading failures with circuit breakers:

```csharp
services.AddTuxedoResiliency(options =>
{
    options.EnableCircuitBreaker = true;
    options.CircuitBreakerThreshold = 5;        // Open after 5 consecutive failures
    options.CircuitBreakerTimeout = TimeSpan.FromSeconds(30); // Try again after 30 seconds
});

// Circuit breaker states:
// - Closed: Normal operation, requests pass through
// - Open: Requests fail immediately without attempting database call
// - Half-Open: Test with single request to check if service recovered

// Monitor circuit breaker state
public class HealthCheckService
{
    private readonly IResiliencyProvider _resiliency;
    private readonly ILogger<HealthCheckService> _logger;
    
    public async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            await _resiliency.ExecuteAsync(async () =>
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                await conn.ExecuteScalarAsync("SELECT 1");
            });
            
            _logger.LogInformation("Database is healthy");
            return true;
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogWarning("Circuit breaker is open - database is unhealthy");
            return false;
        }
    }
}
```

### Combining with Other Patterns

```csharp
// Resiliency + Repository Pattern
public class ResilientProductRepository : IProductRepository
{
    private readonly IDbConnection _connection;
    
    public ResilientProductRepository(IDbConnection connection)
    {
        // Connection is already wrapped with resiliency from DI
        _connection = connection;
    }
    
    public async Task<Product?> GetByIdAsync(int id)
    {
        // Automatic retry on transient failures
        return await _connection.GetAsync<Product>(id);
    }
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        // Query builder with automatic resiliency
        return await QueryBuilder.Query<Product>()
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name)
            .ToListAsync(_connection);
    }
}

// Resiliency + Unit of Work
public class ResilientUnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;
    
    public async Task<bool> ExecuteInTransactionAsync(Func<Task> operation)
    {
        // Connection open will retry on failure
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        
        _transaction = _connection.BeginTransaction();
        
        try
        {
            await operation();
            _transaction.Commit();
            return true;
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            _transaction.Rollback();
            // Let retry policy handle it
            throw;
        }
        catch
        {
            _transaction.Rollback();
            return false;
        }
    }
}
```

### Testing with Resiliency

```csharp
[Fact]
public async Task Should_Retry_On_Transient_Failure()
{
    var attempts = 0;
    var mockConnection = new Mock<IDbConnection>();
    
    // Fail twice, then succeed
    mockConnection
        .Setup(c => c.ExecuteScalar(It.IsAny<string>(), It.IsAny<object>()))
        .Returns(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new SqlException("Timeout expired");
            return 42;
        });
    
    var resilientConnection = new ResilientDbConnection(
        mockConnection.Object,
        new ExponentialBackoffRetryPolicy(maxAttempts: 3));
    
    var result = resilientConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products");
    
    Assert.Equal(42, result);
    Assert.Equal(3, attempts); // Should have retried twice
}
```

## Resiliency Best Practices

1. **Configure Appropriate Delays**: Use exponential backoff to avoid overwhelming the database
2. **Set Reasonable Retry Limits**: 3-5 retries are usually sufficient
3. **Enable Circuit Breakers**: Prevent cascading failures in microservices
4. **Log Retry Attempts**: Monitor and alert on excessive retries
5. **Test Failure Scenarios**: Ensure your application handles database outages gracefully
6. **Use Idempotent Operations**: Ensure retried operations don't cause data inconsistencies
7. **Combine with Caching**: Use cache as fallback when circuit breaker is open

## Bulk Operations

Tuxedo provides high-performance bulk operations for efficiently handling large datasets with minimal round trips to the database.

### Bulk Insert

Insert thousands of records in optimized batches:

```csharp
using Tuxedo.BulkOperations;

// Create bulk operations instance
var bulkOps = new BulkOperations(TuxedoDialect.SqlServer);

// Insert large dataset
var products = GenerateProducts(10000); // 10,000 products
var inserted = await bulkOps.BulkInsertAsync(
    connection,
    products,
    tableName: "Products",
    batchSize: 1000  // Process in batches of 1000
);

Console.WriteLine($"Inserted {inserted} products");

// Using with dependency injection
services.AddSingleton<IBulkOperations>(provider =>
{
    var dialect = TuxedoDialect.SqlServer; // Or from configuration
    return new BulkOperations(dialect);
});
```

### Bulk Update

Update multiple records efficiently:

```csharp
// Update pricing for all products in a category
var productsToUpdate = await connection.QueryAsync<Product>(
    "SELECT * FROM Products WHERE Category = @category",
    new { category = "Electronics" });

// Apply 10% discount
foreach (var product in productsToUpdate)
{
    product.Price *= 0.9m;
    product.LastModified = DateTime.Now;
}

var updated = await bulkOps.BulkUpdateAsync(
    connection,
    productsToUpdate,
    batchSize: 500
);

Console.WriteLine($"Updated {updated} products with discount");
```

### Bulk Delete

Remove multiple records in optimized batches:

```csharp
// Delete discontinued products
var discontinuedProducts = await connection.QueryAsync<Product>(
    "SELECT * FROM Products WHERE Discontinued = 1");

var deleted = await bulkOps.BulkDeleteAsync(
    connection,
    discontinuedProducts,
    batchSize: 1000
);

Console.WriteLine($"Deleted {deleted} discontinued products");

// Or by IDs
var idsToDelete = new[] { 1, 2, 3, 4, 5 };
var productsToDelete = idsToDelete.Select(id => new Product { Id = id });

await bulkOps.BulkDeleteAsync(connection, productsToDelete);
```

### Bulk Merge (Upsert)

Insert or update records based on key matching:

```csharp
// Merge product catalog from external source
var externalProducts = await FetchExternalCatalog();

var merged = await bulkOps.BulkMergeAsync(
    connection,
    externalProducts,
    tableName: "Products",
    batchSize: 1000
);

Console.WriteLine($"Merged {merged} products (inserted or updated)");

// Database-specific UPSERT operations:
// - SQL Server: Uses MERGE statement
// - PostgreSQL/SQLite: Uses INSERT ... ON CONFLICT
// - MySQL: Uses INSERT ... ON DUPLICATE KEY UPDATE
```

### Advanced Bulk Operations

```csharp
// Bulk operations with transaction
using var transaction = connection.BeginTransaction();
try
{
    // Import new products
    await bulkOps.BulkInsertAsync(
        connection,
        newProducts,
        transaction: transaction,
        batchSize: 1000
    );
    
    // Update existing products
    await bulkOps.BulkUpdateAsync(
        connection,
        updatedProducts,
        transaction: transaction,
        batchSize: 1000
    );
    
    // Remove old products
    await bulkOps.BulkDeleteAsync(
        connection,
        oldProducts,
        transaction: transaction,
        batchSize: 1000
    );
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}

// Async with cancellation
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(5));

var result = await bulkOps.BulkInsertAsync(
    connection,
    largeDataset,
    batchSize: 2000,
    commandTimeout: 120,
    cancellationToken: cts.Token
);

// Custom table names
await bulkOps.BulkInsertAsync(
    connection,
    temporaryProducts,
    tableName: "Products_Staging",
    batchSize: 5000
);
```

### Performance Considerations

**Bulk Operations Performance Guide:**

| Records | Single Insert | Bulk Insert | Improvement |
|---------|--------------|-------------|-------------|
| 100     | ~500ms       | ~50ms       | 10x faster  |
| 1,000   | ~5,000ms     | ~200ms      | 25x faster  |
| 10,000  | ~50,000ms    | ~1,000ms    | 50x faster  |
| 100,000 | ~500,000ms   | ~8,000ms    | 62x faster  |

**Best Practices:**
- Use batch sizes between 500-5000 depending on record size
- Larger batches for simple schemas, smaller for complex ones
- Enable connection pooling for better throughput
- Use transactions for data consistency
- Consider disabling indexes/constraints during bulk loads

## Caching

Tuxedo provides a comprehensive caching layer to improve query performance and reduce database load. The caching system supports both in-memory and distributed caching scenarios.

### Setup and Configuration

Configure caching in your application:

```csharp
using Tuxedo.Caching;
using Tuxedo.DependencyInjection;

// Basic setup with memory cache
services.AddTuxedoCaching(options =>
{
    options.DefaultCacheDuration = TimeSpan.FromMinutes(5);
    options.MaxCacheSize = 1000; // Maximum number of cached items
    options.EnableSlidingExpiration = true;
});

// With configuration
services.AddTuxedoCaching(configuration, "TuxedoCaching");

// appsettings.json
{
  "TuxedoCaching": {
    "DefaultCacheDuration": "00:05:00",
    "MaxCacheSize": 1000,
    "EnableSlidingExpiration": true
  }
}

// Complete enterprise setup
services.AddTuxedoEnterprise(options =>
{
    options.EnableCaching = true;
    options.DefaultCacheDuration = TimeSpan.FromMinutes(10);
    options.MaxCacheSize = 5000;
});
```

### Basic Query Caching

Cache query results with automatic key generation:

```csharp
using Tuxedo.Caching;

// Cache query results
var products = await connection.QueryWithCacheAsync<Product>(
    "SELECT * FROM Products WHERE Category = @Category",
    new { Category = "Electronics" },
    cacheExpiration: TimeSpan.FromMinutes(10)
);

// Cache single result
var product = await connection.QuerySingleWithCacheAsync<Product>(
    "SELECT * FROM Products WHERE Id = @Id",
    new { Id = productId },
    cacheExpiration: TimeSpan.FromHours(1)
);

// Custom cache key
var topProducts = await connection.QueryWithCacheAsync<Product>(
    "SELECT TOP 10 * FROM Products ORDER BY Sales DESC",
    cacheKey: "top-products",
    cacheExpiration: TimeSpan.FromMinutes(15)
);
```

### Advanced Caching Patterns

#### Cache with Tags

Organize cached items with tags for bulk invalidation:

```csharp
public class ProductRepository
{
    private readonly IDbConnection _connection;
    private readonly IQueryCache _cache;
    private const string ProductTag = "products";
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var key = $"products:category:{category}";
        
        return await _cache.GetOrAddAsync(key, async () =>
        {
            var products = await _connection.QueryAsync<Product>(
                "SELECT * FROM Products WHERE Category = @Category",
                new { Category = category }
            );
            
            // Tag this cache entry
            if (_cache is MemoryQueryCache memCache)
            {
                memCache.AddKeyToTag(key, ProductTag);
            }
            
            return products;
        },
        expiration: TimeSpan.FromMinutes(10));
    }
    
    public async Task InvalidateProductCacheAsync()
    {
        // Invalidate all product-related cache entries
        await _cache.InvalidateByTagAsync(ProductTag);
    }
}
```

#### Conditional Caching

Cache based on specific conditions:

```csharp
public async Task<IEnumerable<Order>> GetOrdersAsync(
    DateTime startDate, 
    DateTime endDate,
    bool useCache = true)
{
    // Only cache if date range is reasonable
    var shouldCache = useCache && 
                     (endDate - startDate).TotalDays <= 30;
    
    if (shouldCache)
    {
        var cacheKey = $"orders:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        return await connection.QueryWithCacheAsync<Order>(
            "SELECT * FROM Orders WHERE OrderDate BETWEEN @Start AND @End",
            new { Start = startDate, End = endDate },
            cacheKey: cacheKey,
            cacheExpiration: TimeSpan.FromMinutes(30)
        );
    }
    
    // Direct query without caching
    return await connection.QueryAsync<Order>(
        "SELECT * FROM Orders WHERE OrderDate BETWEEN @Start AND @End",
        new { Start = startDate, End = endDate }
    );
}
```

#### Cache-Aside Pattern

Manual cache management for complex scenarios:

```csharp
public class CustomerService
{
    private readonly IQueryCache _cache;
    private readonly IDbConnection _connection;
    
    public async Task<Customer> GetCustomerWithOrdersAsync(int customerId)
    {
        var cacheKey = $"customer:{customerId}:full";
        
        // Try to get from cache
        var cached = await _cache.GetAsync<CustomerWithOrders>(cacheKey);
        if (cached != null)
            return cached;
        
        // Load from database
        using var multi = await _connection.QueryMultipleAsync(@"
            SELECT * FROM Customers WHERE Id = @Id;
            SELECT * FROM Orders WHERE CustomerId = @Id;",
            new { Id = customerId });
        
        var customer = await multi.ReadSingleAsync<Customer>();
        var orders = await multi.ReadAsync<Order>();
        
        var result = new CustomerWithOrders
        {
            Customer = customer,
            Orders = orders.ToList()
        };
        
        // Cache the result
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));
        
        return result;
    }
    
    public async Task UpdateCustomerAsync(Customer customer)
    {
        // Update database
        await _connection.UpdateAsync(customer);
        
        // Invalidate cache
        await _cache.RemoveAsync($"customer:{customer.Id}:full");
    }
}
```

### Cache Invalidation Strategies

#### Time-Based Expiration

```csharp
// Absolute expiration
await _cache.SetAsync("key", value, TimeSpan.FromMinutes(30));

// Sliding expiration (via configuration)
services.AddTuxedoCaching(options =>
{
    options.EnableSlidingExpiration = true;
    options.DefaultCacheDuration = TimeSpan.FromMinutes(5);
});
```

#### Event-Based Invalidation

```csharp
public class ProductService
{
    private readonly IQueryCache _cache;
    
    public async Task CreateProductAsync(Product product)
    {
        await _connection.InsertAsync(product);
        
        // Invalidate related caches
        await InvalidateProductCachesAsync(product.Category);
    }
    
    public async Task UpdateProductAsync(Product product)
    {
        await _connection.UpdateAsync(product);
        
        // Invalidate specific and related caches
        await _cache.RemoveAsync($"product:{product.Id}");
        await InvalidateProductCachesAsync(product.Category);
    }
    
    private async Task InvalidateProductCachesAsync(string category)
    {
        // Invalidate category cache
        await _cache.RemoveAsync($"products:category:{category}");
        
        // Invalidate aggregate caches
        await _cache.RemoveAsync("products:count");
        await _cache.RemoveAsync($"products:category:{category}:count");
        
        // Invalidate top products if exists
        await _cache.RemoveAsync("top-products");
    }
}
```

#### Dependency-Based Invalidation

```csharp
public class CacheDependencyManager
{
    private readonly IQueryCache _cache;
    private readonly Dictionary<string, HashSet<string>> _dependencies;
    
    public async Task<T> GetWithDependenciesAsync<T>(
        string key,
        Func<Task<T>> factory,
        params string[] dependsOn) where T : class
    {
        // Track dependencies
        foreach (var dependency in dependsOn)
        {
            if (!_dependencies.ContainsKey(dependency))
                _dependencies[dependency] = new HashSet<string>();
            _dependencies[dependency].Add(key);
        }
        
        return await _cache.GetOrAddAsync(key, factory);
    }
    
    public async Task InvalidateDependenciesAsync(string dependency)
    {
        if (_dependencies.TryGetValue(dependency, out var keys))
        {
            foreach (var key in keys)
            {
                await _cache.RemoveAsync(key);
            }
            _dependencies.Remove(dependency);
        }
    }
}
```

### Distributed Caching

Implement distributed caching with Redis or SQL Server:

```csharp
// Custom distributed cache implementation
public class RedisQueryCache : IQueryCache
{
    private readonly IDistributedCache _distributedCache;
    private readonly ISerializer _serializer;
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) 
        where T : class
    {
        var bytes = await _distributedCache.GetAsync(key, cancellationToken);
        if (bytes == null) return null;
        
        return _serializer.Deserialize<T>(bytes);
    }
    
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, 
        CancellationToken cancellationToken = default) where T : class
    {
        var bytes = _serializer.Serialize(value);
        var options = new DistributedCacheEntryOptions();
        
        if (expiration.HasValue)
            options.SetSlidingExpiration(expiration.Value);
        
        await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
    }
    
    // ... other methods
}

// Register distributed cache
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "TuxedoCache";
});

services.AddTuxedoCaching(options =>
{
    options.UseDistributedCache = true;
    options.DistributedCacheFactory = sp => 
        new RedisQueryCache(
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<ISerializer>()
        );
});
```

### Cache Performance Monitoring

Monitor cache performance and hit rates:

```csharp
public class CacheMetrics
{
    private long _hits;
    private long _misses;
    private long _evictions;
    
    public double HitRate => _hits / (double)(_hits + _misses);
    
    public void RecordHit() => Interlocked.Increment(ref _hits);
    public void RecordMiss() => Interlocked.Increment(ref _misses);
    public void RecordEviction() => Interlocked.Increment(ref _evictions);
}

public class InstrumentedQueryCache : IQueryCache
{
    private readonly IQueryCache _innerCache;
    private readonly CacheMetrics _metrics;
    private readonly ILogger<InstrumentedQueryCache> _logger;
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) 
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _innerCache.GetAsync<T>(key, cancellationToken);
        
        if (result != null)
        {
            _metrics.RecordHit();
            _logger.LogDebug("Cache hit for {Key} in {ElapsedMs}ms", 
                key, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _metrics.RecordMiss();
            _logger.LogDebug("Cache miss for {Key}", key);
        }
        
        return result;
    }
    
    // ... other methods with instrumentation
}
```

### Caching Best Practices

1. **Cache Key Strategy**
   - Use consistent, predictable key patterns
   - Include version numbers for cache busting
   - Avoid sensitive data in cache keys

2. **Expiration Policies**
   - Use sliding expiration for frequently accessed data
   - Shorter TTL for volatile data
   - Longer TTL for reference data

3. **Memory Management**
   - Set appropriate cache size limits
   - Monitor memory usage
   - Implement cache eviction policies

4. **Invalidation Strategy**
   - Invalidate on write operations
   - Use tags for bulk invalidation
   - Consider eventual consistency

5. **Performance Considerations**
   - Cache serializable data only
   - Avoid caching large objects
   - Use compression for large values
   - Monitor cache hit rates

### Cache Configuration Examples

```csharp
// Development environment
services.AddTuxedoCaching(options =>
{
    options.DefaultCacheDuration = TimeSpan.FromSeconds(30);
    options.MaxCacheSize = 100;
    options.EnableSlidingExpiration = false;
});

// Production environment
services.AddTuxedoCaching(options =>
{
    options.DefaultCacheDuration = TimeSpan.FromMinutes(15);
    options.MaxCacheSize = 10000;
    options.EnableSlidingExpiration = true;
});

// High-traffic scenarios
services.AddTuxedoCaching(options =>
{
    options.DefaultCacheDuration = TimeSpan.FromHours(1);
    options.MaxCacheSize = 50000;
    options.EnableSlidingExpiration = true;
    options.UseDistributedCache = true;
});
```

## Diagnostics and Monitoring

Tuxedo provides comprehensive diagnostics and health monitoring capabilities for production environments.

### Health Checks

Monitor database connectivity and performance:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tuxedo.DependencyInjection;

// Add health checks
services.AddHealthChecks()
    .AddTuxedoHealthCheck("database", tags: new[] { "db", "sql" });

// Configure in ASP.NET Core
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db")
});

// Custom health check implementation
public class DetailedDbHealthCheck : IHealthCheck
{
    private readonly IDbConnection _connection;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Test connection
            if (_connection.State != ConnectionState.Open)
                await Task.Run(() => _connection.Open(), cancellationToken);
            
            // Test query execution
            var result = await _connection.ExecuteScalarAsync<int>("SELECT 1");
            
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database responding slowly: {stopwatch.ElapsedMilliseconds}ms");
            }
            
            return HealthCheckResult.Healthy(
                $"Database responding normally: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection failed", 
                exception: ex);
        }
    }
}
```

### Diagnostics Events

Track and monitor all database operations:

```csharp
using Tuxedo.Diagnostics;

// Register diagnostics
services.AddSingleton<ITuxedoDiagnostics, TuxedoDiagnostics>();

// Subscribe to events
public class DatabaseMonitor
{
    private readonly ITuxedoDiagnostics _diagnostics;
    
    public DatabaseMonitor(ITuxedoDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
        
        // Subscribe to events
        _diagnostics.QueryExecuted += OnQueryExecuted;
        _diagnostics.CommandExecuted += OnCommandExecuted;
        _diagnostics.ErrorOccurred += OnError;
        _diagnostics.ConnectionOpened += OnConnectionOpened;
        _diagnostics.TransactionCommitted += OnTransactionCommitted;
        _diagnostics.TransactionRolledBack += OnTransactionRolledBack;
    }
    
    private void OnQueryExecuted(object sender, QueryExecutedEventArgs e)
    {
        if (e.Duration.TotalSeconds > 5)
        {
            // Log slow query
            _logger.LogWarning("Slow query detected: {Query} ({Duration}ms)", 
                e.Query, e.Duration.TotalMilliseconds);
            
            // Send metric to monitoring system
            _metrics.RecordSlowQuery(e.Query, e.Duration);
        }
        
        // Track query patterns
        _metrics.IncrementQueryCount(e.QueryType);
    }
    
    private void OnError(object sender, ErrorEventArgs e)
    {
        // Log error with context
        _logger.LogError(e.Exception, 
            "Database error in {Context}: {Query}",
            e.Context, e.Query);
        
        // Alert if critical
        if (IsCriticalError(e.Exception))
        {
            _alerting.SendDatabaseAlert(e.Exception);
        }
    }
    
    private void OnTransactionRolledBack(object sender, TransactionEventArgs e)
    {
        _logger.LogWarning(
            "Transaction rolled back: {TransactionId} after {Duration}ms",
            e.TransactionId, e.Duration?.TotalMilliseconds);
        
        _metrics.IncrementRollbackCount();
    }
}
```

### Performance Metrics Collection

```csharp
// Integrate with Application Insights, Prometheus, or custom metrics
public class TuxedoMetricsCollector
{
    private readonly ITuxedoDiagnostics _diagnostics;
    private readonly IMetricsClient _metrics;
    
    public TuxedoMetricsCollector(ITuxedoDiagnostics diagnostics, IMetricsClient metrics)
    {
        _diagnostics = diagnostics;
        _metrics = metrics;
        
        _diagnostics.QueryExecuted += (s, e) =>
        {
            _metrics.RecordHistogram("db.query.duration", e.Duration.TotalMilliseconds,
                new[] { ("query_type", e.QueryType), ("table", e.TableName) });
        };
        
        _diagnostics.CommandExecuted += (s, e) =>
        {
            _metrics.RecordHistogram("db.command.duration", e.Duration.TotalMilliseconds,
                new[] { ("command_type", e.CommandType.ToString()) });
            _metrics.RecordGauge("db.rows_affected", e.RowsAffected);
        };
        
        _diagnostics.ConnectionOpened += (s, e) =>
        {
            _metrics.Increment("db.connections.opened");
            if (e.OpenDuration.HasValue)
            {
                _metrics.RecordHistogram("db.connection.open_time", 
                    e.OpenDuration.Value.TotalMilliseconds);
            }
        };
        
        _diagnostics.ErrorOccurred += (s, e) =>
        {
            _metrics.Increment("db.errors",
                new[] { ("error_type", e.Exception.GetType().Name) });
        };
    }
}
```

### Query Performance Analysis

```csharp
// Automatic slow query detection
services.Configure<TuxedoDiagnosticsOptions>(options =>
{
    options.SlowQueryThresholdMs = 1000; // Queries over 1 second
    options.LogSlowQueries = true;
    options.IncludeQueryParameters = false; // For security
    options.SanitizeConnectionStrings = true;
});

// Custom query analyzer
public class QueryAnalyzer
{
    private readonly ConcurrentDictionary<string, QueryStats> _queryStats = new();
    
    public QueryAnalyzer(ITuxedoDiagnostics diagnostics)
    {
        diagnostics.QueryExecuted += AnalyzeQuery;
    }
    
    private void AnalyzeQuery(object sender, QueryExecutedEventArgs e)
    {
        var stats = _queryStats.AddOrUpdate(
            e.Query,
            new QueryStats { Query = e.Query },
            (key, existing) =>
            {
                existing.ExecutionCount++;
                existing.TotalDuration += e.Duration;
                existing.MaxDuration = Math.Max(existing.MaxDuration, e.Duration.TotalMilliseconds);
                existing.MinDuration = Math.Min(existing.MinDuration, e.Duration.TotalMilliseconds);
                return existing;
            });
        
        // Alert on problematic patterns
        if (stats.ExecutionCount > 1000 && stats.AverageDuration > 500)
        {
            _logger.LogWarning(
                "Frequent slow query detected: {Query} " +
                "Executions: {Count}, Avg: {Avg}ms, Max: {Max}ms",
                e.Query, stats.ExecutionCount, 
                stats.AverageDuration, stats.MaxDuration);
        }
    }
    
    public IEnumerable<QueryStats> GetTopSlowQueries(int count = 10)
    {
        return _queryStats.Values
            .OrderByDescending(q => q.AverageDuration)
            .Take(count);
    }
}
```

### Connection Pool Monitoring

```csharp
// Monitor connection pool health
public class ConnectionPoolMonitor
{
    private int _activeConnections;
    private int _pooledConnections;
    
    public ConnectionPoolMonitor(ITuxedoDiagnostics diagnostics)
    {
        diagnostics.ConnectionOpened += (s, e) =>
        {
            Interlocked.Increment(ref _activeConnections);
            UpdateMetrics();
        };
        
        diagnostics.ConnectionClosed += (s, e) =>
        {
            Interlocked.Decrement(ref _activeConnections);
            UpdateMetrics();
        };
    }
    
    private void UpdateMetrics()
    {
        _metrics.RecordGauge("db.connections.active", _activeConnections);
        
        // Alert if connection leak detected
        if (_activeConnections > 100)
        {
            _logger.LogError("Potential connection leak: {Count} active connections",
                _activeConnections);
        }
    }
}
```

### Diagnostics Best Practices

1. **Enable in Production**: Use diagnostics to monitor real-world performance
2. **Set Thresholds**: Configure appropriate slow query thresholds
3. **Sanitize Logs**: Never log passwords or sensitive data
4. **Use Sampling**: For high-traffic apps, sample diagnostics to reduce overhead
5. **Integrate Monitoring**: Connect to APM tools (Application Insights, DataDog, New Relic)
6. **Track Trends**: Monitor query performance over time
7. **Alert on Anomalies**: Set up alerts for unusual patterns

## Dependency Injection

Tuxedo includes lightweight DI helpers. Register a connection factory:

```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Microsoft.Data.SqlClient;
using Tuxedo.DependencyInjection;

var services = new ServiceCollection();
services.AddTuxedo(_ => new SqlConnection(cs));          // scoped by default

// With options
services.AddSingleton(new DbOptions { ConnectionString = cs });
services.AddTuxedoWithOptions<DbOptions>((sp, opt) => new SqlConnection(opt.ConnectionString));

// In-memory SQLite for testing
services.AddTuxedoSqliteInMemory("TestDb");

var provider = services.BuildServiceProvider();
var conn = provider.GetRequiredService<IDbConnection>();

public record DbOptions { public string ConnectionString { get; init; } = string.Empty; }
```

### Provider helpers

```csharp
using Tuxedo.DependencyInjection;

// SQL Server
services.AddTuxedoSqlServer("Server=.;Database=Demo;Integrated Security=true;TrustServerCertificate=true");

// PostgreSQL
services.AddTuxedoPostgres("Host=localhost;Database=demo;Username=demo;Password=pass");

// MySQL
services.AddTuxedoMySql("Server=localhost;Database=demo;Uid=demo;Pwd=pass;");

// SQLite (file-based)
services.AddTuxedoSqlite("demo.db");

// SQLite (in-memory shared)
services.AddTuxedoSqliteInMemory("TestDb");
```

## Notes

- Use provider‑specific connection strings and types (SqlConnection, NpgsqlConnection, MySqlConnection, SqliteConnection).
- `[ExplicitKey]` marks non‑identity keys; `[Computed]` properties are ignored on write.
- Async CRUD helpers are available as `GetAsync`/`SelectAsync`, `GetAllAsync`, `InsertAsync`, `UpdateAsync`, `DeleteAsync`, `DeleteAllAsync`.
- Partial update functionality is built into `Update` and `UpdateAsync` methods with optional property filtering.

## Status

This README documents the APIs present in the codebase today. The library includes comprehensive query building, CRUD operations, partial updates, dependency injection helpers, and multi-database support.
