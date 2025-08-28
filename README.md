# Tuxedo

Tuxedo merges Dapper and Dapper.Contrib functionality into a single package for modern .NET. It provides high‑performance ADO.NET mapping and CRUD helpers that work with SQL Server, PostgreSQL, MySQL, and SQLite.

## Features

- High performance query/execute APIs (`Query`, `Execute`, async variants)
- **Dynamic Query Builder**: Fluent LINQ-style query building with expression support
- Contrib‑style CRUD: `Get`/`GetAll`, `Insert`, `Update`, `Delete` (+ async)
- **Partial Updates**: Enhanced `Update`/`UpdateAsync` methods support selective column updates
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
