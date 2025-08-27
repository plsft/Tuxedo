# Tuxedo

Tuxedo is a modernized .NET data access library that merges Dapper and Dapper.Contrib functionality into a single, unified package optimized for .NET 6+. It provides high-performance object mapping with built-in support for SQL Server, PostgreSQL, MySQL, and SQLite, along with enterprise-grade features for resilience, caching, and advanced query patterns.

## Features

### Core Features
- **High Performance**: Built on Dapper's proven performance characteristics
- **Multi-Database Support**: Native adapters for SQL Server, PostgreSQL, MySQL, and SQLite with dialect-aware SQL generation
- **Modern .NET**: Optimized for .NET 6, .NET 8, and .NET 9
- **SQL-Aligned CRUD Operations**: Native Insert, Update, Delete, and Select methods matching SQL verbs
- **Query Support**: Full Dapper query capabilities with async support
- **Type Safety**: Strongly-typed mapping with nullable reference type support
- **Dual API**: Supports both legacy Dapper methods (Query, Get) and SQL-aligned methods (Select)
- **Dependency Injection**: Built-in IServiceCollection extensions for modern DI patterns

### Enterprise Features (New!)
- **Connection Resiliency**: Automatic retry policies with exponential backoff for transient errors
- **Query Caching**: High-performance in-memory caching with tag-based invalidation
- **Distributed Tracing**: OpenTelemetry integration for monitoring and observability
- **Fluent Query Builder**: Type-safe, chainable API for building complex queries
- **Bulk Operations**: Efficient batch insert, update, delete, and merge operations
- **Repository Pattern**: Generic repository implementation with async support
- **Unit of Work Pattern**: Transaction management across multiple repositories
- **Specification Pattern**: Express complex queries as reusable business rules
- **Logging & Diagnostics**: Comprehensive event-based monitoring and performance tracking
- **Health Checks**: Built-in health check support for monitoring database connectivity
- **Advanced Pagination**: Comprehensive pagination support with multiple implementation patterns

## Installation

```bash
dotnet add package Tuxedo
```

For database-specific providers:
```bash
# SQL Server
dotnet add package Microsoft.Data.SqlClient

# PostgreSQL
dotnet add package Npgsql

# MySQL
dotnet add package MySqlConnector

# SQLite
dotnet add package Microsoft.Data.Sqlite
```

## Quick Start

### Basic Setup

```csharp
using Tuxedo;
using Tuxedo.Contrib;
```

### Define Your Models

```csharp
[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    [Computed]
    public DateTime LastModified { get; set; }
}

[Table("Customers")]
public class Customer
{
    [ExplicitKey]
    public Guid CustomerId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedDate { get; set; }
}

[Table("Orders")]
public class Order
{
    [Key]
    public long OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }
}
```

## Pagination Support

Tuxedo provides comprehensive pagination support through multiple patterns, suitable for different use cases and performance requirements.

### Basic Pagination with Extension Methods

```csharp
using Tuxedo.Pagination;
using Tuxedo.Patterns;

// Simple paginated query
var pagedProducts = await connection.QueryPagedAsync<Product>(
    "SELECT * FROM Products WHERE Category = @category ORDER BY Name",
    pageIndex: 0,
    pageSize: 20,
    param: new { category = "Electronics" }
);

Console.WriteLine($"Page {pagedProducts.PageIndex + 1} of {pagedProducts.TotalPages}");
Console.WriteLine($"Showing {pagedProducts.Items.Count} of {pagedProducts.TotalCount} total items");
Console.WriteLine($"Has next page: {pagedProducts.HasNextPage}");
Console.WriteLine($"Has previous page: {pagedProducts.HasPreviousPage}");
```

### Pagination with Separate Count Query

For better performance with complex queries, use a separate optimized count query:

```csharp
// Optimized pagination with separate count query
var pagedResults = await connection.QueryPagedAsync<Product>(
    selectSql: @"
        SELECT p.*, c.CategoryName 
        FROM Products p
        INNER JOIN Categories c ON p.CategoryId = c.Id
        WHERE p.Price > @minPrice
        ORDER BY p.Price DESC",
    countSql: @"
        SELECT COUNT(*) 
        FROM Products 
        WHERE Price > @minPrice",
    pageIndex: 0,
    pageSize: 25,
    param: new { minPrice = 100 }
);
```

### Pagination with Repository Pattern

```csharp
using Tuxedo.Patterns;

public interface IProductRepository : IRepository<Product>
{
    Task<PagedResult<Product>> GetPagedByCategoryAsync(
        string category, 
        int pageIndex, 
        int pageSize);
}

public class ProductRepository : DapperRepository<Product>, IProductRepository
{
    public ProductRepository(IDbConnection connection) : base(connection)
    {
    }
    
    public async Task<PagedResult<Product>> GetPagedByCategoryAsync(
        string category, 
        int pageIndex, 
        int pageSize)
    {
        // Use built-in GetPagedAsync method
        return await GetPagedAsync(
            pageIndex,
            pageSize,
            predicate: p => p.Category == category,
            orderBy: q => q.OrderBy(p => p.Name)
        );
    }
}

// Usage
var repository = new ProductRepository(connection);
var pagedProducts = await repository.GetPagedByCategoryAsync(
    "Electronics", 
    pageIndex: 2, 
    pageSize: 10
);
```

### Pagination with Fluent Query Builder

```csharp
using Tuxedo.QueryBuilder;

public class ProductService
{
    private readonly IDbConnection _connection;
    
    public async Task<PagedResult<Product>> GetProductsPagedAsync(
        int pageIndex,
        int pageSize,
        string? category = null,
        decimal? minPrice = null,
        string? sortBy = null)
    {
        var query = QueryBuilderExtensions.Query<Product>();
        
        // Apply filters
        if (!string.IsNullOrEmpty(category))
            query.Where(p => p.Category == category);
            
        if (minPrice.HasValue)
            query.Where($"Price >= {minPrice.Value}");
        
        // Apply sorting
        switch (sortBy?.ToLower())
        {
            case "price":
                query.OrderBy(p => p.Price);
                break;
            case "price_desc":
                query.OrderByDescending(p => p.Price);
                break;
            case "name":
                query.OrderBy(p => p.Name);
                break;
            default:
                query.OrderBy(p => p.Id);
                break;
        }
        
        // Apply pagination
        query.Page(pageIndex, pageSize);
        
        // Execute queries
        var countQuery = QueryBuilderExtensions.Query<Product>();
        if (!string.IsNullOrEmpty(category))
            countQuery.Where(p => p.Category == category);
        if (minPrice.HasValue)
            countQuery.Where($"Price >= {minPrice.Value}");
        
        var items = await query.ToListAsync(_connection);
        var totalCount = await countQuery.CountAsync(_connection);
        
        return new PagedResult<Product>(
            items.ToList(), 
            pageIndex, 
            pageSize, 
            totalCount
        );
    }
}
```

### Advanced Pagination Scenarios

#### 1. Cursor-Based Pagination (for real-time data)

```csharp
public async Task<CursorPagedResult<Product>> GetProductsCursorPaginatedAsync(
    string? cursor,
    int pageSize = 20)
{
    var sql = @"
        SELECT * FROM Products 
        WHERE (@cursor IS NULL OR Id > @cursor)
        ORDER BY Id
        LIMIT @pageSize + 1"; // Get one extra to check if there's a next page
    
    var items = (await connection.QueryAsync<Product>(
        sql, 
        new { cursor = cursor, pageSize = pageSize + 1 }
    )).ToList();
    
    var hasNextPage = items.Count > pageSize;
    if (hasNextPage)
    {
        items = items.Take(pageSize).ToList();
    }
    
    var nextCursor = hasNextPage ? items.Last().Id.ToString() : null;
    
    return new CursorPagedResult<Product>
    {
        Items = items,
        NextCursor = nextCursor,
        HasNextPage = hasNextPage
    };
}

public class CursorPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
    public bool HasNextPage { get; set; }
}
```

#### 2. Keyset Pagination (for stable sorting)

```csharp
public async Task<PagedResult<Product>> GetProductsKeysetPaginatedAsync(
    decimal? lastPrice,
    int? lastId,
    int pageSize = 20)
{
    var sql = @"
        SELECT * FROM Products 
        WHERE (Price, Id) > (@lastPrice, @lastId)
        ORDER BY Price DESC, Id DESC
        LIMIT @pageSize";
    
    var items = await connection.QueryAsync<Product>(
        sql,
        new { lastPrice = lastPrice ?? decimal.MaxValue, lastId = lastId ?? 0, pageSize }
    );
    
    // Get total count separately
    var totalCount = await connection.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM Products"
    );
    
    return new PagedResult<Product>(
        items.ToList(),
        0, // Page index not applicable for keyset pagination
        pageSize,
        totalCount
    );
}
```

#### 3. Pagination with Search and Filtering

```csharp
public class ProductSearchRequest : PageRequest
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
}

public async Task<PagedResult<Product>> SearchProductsAsync(
    ProductSearchRequest request)
{
    // Validate pagination parameters
    request.Validate(new PaginationOptions 
    { 
        MaxPageSize = 50,
        DefaultPageSize = 20 
    });
    
    var builder = new StringBuilder("SELECT * FROM Products WHERE 1=1");
    var parameters = new DynamicParameters();
    
    // Build dynamic query
    if (!string.IsNullOrWhiteSpace(request.SearchTerm))
    {
        builder.Append(" AND (Name LIKE @search OR Description LIKE @search)");
        parameters.Add("search", $"%{request.SearchTerm}%");
    }
    
    if (!string.IsNullOrWhiteSpace(request.Category))
    {
        builder.Append(" AND Category = @category");
        parameters.Add("category", request.Category);
    }
    
    if (request.MinPrice.HasValue)
    {
        builder.Append(" AND Price >= @minPrice");
        parameters.Add("minPrice", request.MinPrice.Value);
    }
    
    if (request.MaxPrice.HasValue)
    {
        builder.Append(" AND Price <= @maxPrice");
        parameters.Add("maxPrice", request.MaxPrice.Value);
    }
    
    if (request.InStock.HasValue)
    {
        builder.Append(" AND StockQuantity " + (request.InStock.Value ? "> 0" : "= 0"));
    }
    
    // Apply sorting
    builder.Append($" ORDER BY {request.SortBy ?? "Id"} ");
    builder.Append(request.SortDescending ? "DESC" : "ASC");
    
    var sql = builder.ToString();
    
    // Execute paginated query
    return await connection.QueryPagedAsync<Product>(
        sql,
        request.PageIndex,
        request.PageSize,
        parameters
    );
}
```

#### 4. Pagination with Aggregations

```csharp
public class ProductPageWithStats<T> : PagedResult<T>
{
    public decimal AveragePrice { get; set; }
    public decimal TotalValue { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    
    public ProductPageWithStats(
        IReadOnlyList<T> items, 
        int pageIndex, 
        int pageSize, 
        int totalCount) 
        : base(items, pageIndex, pageSize, totalCount)
    {
    }
}

public async Task<ProductPageWithStats<Product>> GetProductPageWithStatsAsync(
    int pageIndex,
    int pageSize)
{
    using var multi = await connection.QueryMultipleAsync(@"
        -- Get paged products
        SELECT * FROM Products 
        ORDER BY Id
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        
        -- Get total count
        SELECT COUNT(*) FROM Products;
        
        -- Get statistics
        SELECT 
            AVG(Price) as AveragePrice,
            SUM(Price * StockQuantity) as TotalValue
        FROM Products;
        
        -- Get category distribution
        SELECT Category, COUNT(*) as Count
        FROM Products
        GROUP BY Category;",
        new { offset = pageIndex * pageSize, pageSize }
    );
    
    var items = (await multi.ReadAsync<Product>()).ToList();
    var totalCount = await multi.ReadFirstAsync<int>();
    var stats = await multi.ReadFirstAsync<dynamic>();
    var categoryCounts = await multi.ReadAsync<dynamic>();
    
    var result = new ProductPageWithStats<Product>(
        items, 
        pageIndex, 
        pageSize, 
        totalCount
    )
    {
        AveragePrice = stats.AveragePrice ?? 0,
        TotalValue = stats.TotalValue ?? 0,
        CategoryCounts = categoryCounts.ToDictionary(
            c => (string)c.Category, 
            c => (int)c.Count
        )
    };
    
    return result;
}
```

#### 5. Server-Side Pagination for DataTables/Grids

```csharp
public class DataTableRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public List<Column> Columns { get; set; } = new();
    public List<Order> Order { get; set; } = new();
    public Search Search { get; set; } = new();
}

public class DataTableResponse<T>
{
    public int Draw { get; set; }
    public int RecordsTotal { get; set; }
    public int RecordsFiltered { get; set; }
    public List<T> Data { get; set; } = new();
}

public async Task<DataTableResponse<Product>> GetProductsForDataTableAsync(
    DataTableRequest request)
{
    var searchValue = request.Search?.Value;
    var orderColumn = request.Order?.FirstOrDefault();
    var orderDir = orderColumn?.Dir ?? "asc";
    var columnName = request.Columns[orderColumn?.Column ?? 0].Data;
    
    var whereClause = string.IsNullOrEmpty(searchValue) 
        ? "" 
        : "WHERE Name LIKE @search OR Category LIKE @search";
    
    var sql = $@"
        SELECT * FROM Products {whereClause}
        ORDER BY {columnName} {orderDir}
        OFFSET @start ROWS FETCH NEXT @length ROWS ONLY;
        
        SELECT COUNT(*) FROM Products {whereClause};
        
        SELECT COUNT(*) FROM Products;";
    
    using var multi = await connection.QueryMultipleAsync(
        sql,
        new 
        { 
            search = $"%{searchValue}%",
            start = request.Start,
            length = request.Length
        }
    );
    
    var products = await multi.ReadAsync<Product>();
    var filteredCount = await multi.ReadFirstAsync<int>();
    var totalCount = await multi.ReadFirstAsync<int>();
    
    return new DataTableResponse<Product>
    {
        Draw = request.Draw,
        Data = products.ToList(),
        RecordsFiltered = filteredCount,
        RecordsTotal = totalCount
    };
}
```

### Pagination Best Practices

1. **Always include ORDER BY**: Ensure consistent pagination results
2. **Use appropriate page sizes**: Balance between performance and user experience
3. **Consider total count performance**: Use separate count queries for complex joins
4. **Implement maximum page size limits**: Prevent excessive data retrieval
5. **Cache count results**: For relatively static data, cache total counts
6. **Use cursor pagination for real-time data**: Prevents missing or duplicate items
7. **Index appropriately**: Ensure columns used in WHERE and ORDER BY are indexed

### Pagination Performance Tips

```csharp
// 1. Use covering indexes for better performance
await connection.ExecuteAsync(@"
    CREATE INDEX IX_Products_Category_Price_Name 
    ON Products(Category, Price DESC) 
    INCLUDE (Name, StockQuantity)");

// 2. Use approximate counts for large tables
public async Task<long> GetApproximateProductCountAsync()
{
    // For SQL Server
    var sql = @"
        SELECT SUM(p.rows) 
        FROM sys.partitions p 
        WHERE p.object_id = OBJECT_ID('Products') 
        AND p.index_id < 2";
    
    return await connection.ExecuteScalarAsync<long>(sql);
}

// 3. Use materialized views for complex aggregations
await connection.ExecuteAsync(@"
    CREATE MATERIALIZED VIEW ProductCategoryStats AS
    SELECT 
        Category,
        COUNT(*) as ProductCount,
        AVG(Price) as AvgPrice,
        MIN(Price) as MinPrice,
        MAX(Price) as MaxPrice
    FROM Products
    GROUP BY Category");

// 4. Implement smart caching for pagination
public class CachedPaginationService
{
    private readonly IMemoryCache _cache;
    private readonly IDbConnection _connection;
    
    public async Task<PagedResult<Product>> GetCachedPageAsync(
        string cacheKey,
        int pageIndex,
        int pageSize,
        TimeSpan? cacheDuration = null)
    {
        var fullKey = $"{cacheKey}:{pageIndex}:{pageSize}";
        
        return await _cache.GetOrCreateAsync(fullKey, async entry =>
        {
            entry.SlidingExpiration = cacheDuration ?? TimeSpan.FromMinutes(5);
            
            return await _connection.GetPagedAsync<Product>(
                pageIndex,
                pageSize,
                orderBy: "Name"
            );
        });
    }
}
```

## SQL Server Examples

### Connection Setup

```csharp
using Microsoft.Data.SqlClient;

var connectionString = "Server=localhost;Database=TuxedoDemo;Integrated Security=true;TrustServerCertificate=true";
using var connection = new SqlConnection(connectionString);
```

### CRUD Operations

```csharp
// Insert
var product = new Product 
{ 
    Name = "Gaming Laptop", 
    Price = 1299.99m, 
    Category = "Electronics" 
};
var id = connection.Insert(product);
product.Id = (int)id;

// Insert with explicit key
var customer = new Customer
{
    CustomerId = Guid.NewGuid(),
    FirstName = "John",
    LastName = "Doe",
    Email = "john.doe@example.com",
    CreatedDate = DateTime.UtcNow
};
connection.Insert(customer);

// Select by ID (SQL-aligned syntax)
var retrievedProduct = connection.Select<Product>(id);
// Or use legacy syntax
var sameProduct = connection.Get<Product>(id);

// Update
product.Price = 1199.99m;
connection.Update(product);

// Delete
connection.Delete(product);

// Select All (SQL-aligned syntax)
var allProducts = connection.SelectAll<Product>();
// Or use legacy syntax
var sameProducts = connection.GetAll<Product>();
```

### Query Examples

```csharp
// Simple query with SQL-aligned Select method
var electronics = connection.Select<Product>(
    "SELECT * FROM Products WHERE Category = @category",
    new { category = "Electronics" }
);

// Or use legacy Query syntax
var sameElectronics = connection.Query<Product>(
    "SELECT * FROM Products WHERE Category = @category",
    new { category = "Electronics" }
);

// Query with joins using Select
var orderDetails = connection.Query<Order, Customer, Order>(
    @"SELECT o.*, c.* 
      FROM Orders o 
      INNER JOIN Customers c ON o.CustomerId = c.CustomerId 
      WHERE o.OrderDate >= @startDate",
    (order, customer) => 
    {
        order.CustomerInfo = customer;
        return order;
    },
    new { startDate = DateTime.UtcNow.AddDays(-30) },
    splitOn: "CustomerId"
);

// Multiple result sets
using (var multi = connection.QueryMultiple(@"
    SELECT COUNT(*) FROM Products;
    SELECT COUNT(*) FROM Customers;
    SELECT COUNT(*) FROM Orders;"))
{
    var productCount = multi.Read<int>().Single();
    var customerCount = multi.Read<int>().Single();
    var orderCount = multi.Read<int>().Single();
}

// Stored procedures
var results = connection.Query<Product>(
    "sp_GetProductsByCategory",
    new { CategoryName = "Electronics" },
    commandType: CommandType.StoredProcedure
);
```

### Async Operations

```csharp
// Async insert
var newId = await connection.InsertAsync(product);

// Async select (SQL-aligned syntax)
var products = await connection.SelectAsync<Product>(
    "SELECT * FROM Products WHERE Price > @minPrice",
    new { minPrice = 100 }
);

// Async select by ID
var product = await connection.SelectAsync<Product>(productId);

// Async select all
var allProducts = await connection.SelectAllAsync<Product>();

// Async update
await connection.UpdateAsync(product);

// Async delete
await connection.DeleteAsync(product);

// Async with transaction
using var transaction = connection.BeginTransaction();
try
{
    await connection.InsertAsync(order, transaction);
    await connection.UpdateAsync(customer, transaction);
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## PostgreSQL Examples

### Connection Setup

```csharp
using Npgsql;

var connectionString = "Host=localhost;Database=tuxedo_demo;Username=postgres;Password=yourpassword";
using var connection = new NpgsqlConnection(connectionString);
```

### PostgreSQL-Specific Features

```csharp
// Using PostgreSQL arrays
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string[] Keywords { get; set; }
}

var tag = new Tag 
{ 
    Name = "Technology",
    Keywords = new[] { "software", "hardware", "innovation" }
};
connection.Insert(tag);

// Query with array operations
var techTags = connection.Query<Tag>(
    "SELECT * FROM tags WHERE keywords @> ARRAY[@keyword]",
    new { keyword = "software" }
);

// Using JSONB
public class UserProfile
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string ProfileData { get; set; } // JSONB column
}

var profile = connection.Query<UserProfile>(
    "SELECT * FROM user_profiles WHERE profile_data->>'city' = @city",
    new { city = "New York" }
);

// PostgreSQL-specific upsert
await connection.ExecuteAsync(@"
    INSERT INTO products (name, price, category) 
    VALUES (@name, @price, @category)
    ON CONFLICT (name) 
    DO UPDATE SET price = EXCLUDED.price, category = EXCLUDED.category",
    new { name = "Laptop", price = 999.99m, category = "Electronics" }
);
```

### PostgreSQL Transactions with Savepoints

```csharp
using var transaction = connection.BeginTransaction();
try
{
    await connection.InsertAsync(order, transaction);
    
    // Create savepoint
    await transaction.SaveAsync("before_items");
    
    try
    {
        foreach (var item in orderItems)
        {
            await connection.InsertAsync(item, transaction);
        }
    }
    catch
    {
        // Rollback to savepoint
        await transaction.RollbackAsync("before_items");
        throw;
    }
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## MySQL Examples

### Connection Setup

```csharp
using MySqlConnector;

var connectionString = "Server=localhost;Database=tuxedo_demo;Uid=root;Pwd=yourpassword;";
using var connection = new MySqlConnection(connectionString);
```

### MySQL-Specific Features

```csharp
// Using MySQL-specific data types
public class Article
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; } // TEXT column
    public DateTime PublishedAt { get; set; }
    public TimeSpan ReadTime { get; set; } // TIME column
}

// Insert with last insert ID
var article = new Article 
{ 
    Title = "Getting Started with Tuxedo",
    Content = "Long article content...",
    PublishedAt = DateTime.UtcNow,
    ReadTime = TimeSpan.FromMinutes(5)
};

var id = connection.Insert(article);
Console.WriteLine($"Inserted article with ID: {id}");

// Full-text search
var searchResults = connection.Query<Article>(@"
    SELECT * FROM articles 
    WHERE MATCH(title, content) AGAINST(@search IN NATURAL LANGUAGE MODE)",
    new { search = "Tuxedo tutorial" }
);

// Using MySQL JSON functions
public class Setting
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ConfigJson { get; set; }
}

var settings = connection.Query<Setting>(@"
    SELECT * FROM settings 
    WHERE JSON_EXTRACT(config_json, '$.enabled') = true"
);

// Bulk insert with MySQL-specific syntax
var products = new List<Product>
{
    new Product { Name = "Mouse", Price = 29.99m, Category = "Accessories" },
    new Product { Name = "Keyboard", Price = 79.99m, Category = "Accessories" },
    new Product { Name = "Monitor", Price = 299.99m, Category = "Displays" }
};

await connection.ExecuteAsync(@"
    INSERT INTO products (name, price, category) VALUES 
    (@Name1, @Price1, @Category1),
    (@Name2, @Price2, @Category2),
    (@Name3, @Price3, @Category3)",
    new 
    {
        Name1 = products[0].Name, Price1 = products[0].Price, Category1 = products[0].Category,
        Name2 = products[1].Name, Price2 = products[1].Price, Category2 = products[1].Category,
        Name3 = products[2].Name, Price3 = products[2].Price, Category3 = products[2].Category
    }
);
```

### MySQL Stored Procedures and Functions

```csharp
// Call stored procedure with output parameters
var p = new DynamicParameters();
p.Add("@category", "Electronics");
p.Add("@avg_price", dbType: DbType.Decimal, direction: ParameterDirection.Output);
p.Add("@product_count", dbType: DbType.Int32, direction: ParameterDirection.Output);

connection.Execute("sp_GetCategoryStats", p, commandType: CommandType.StoredProcedure);

var avgPrice = p.Get<decimal>("@avg_price");
var productCount = p.Get<int>("@product_count");
```

## Dependency Injection Support

Tuxedo provides comprehensive dependency injection support through IServiceCollection extensions, following best practices for connection lifetime management.

### Basic DI Setup

```csharp
using Tuxedo.DependencyInjection;

// In your Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Add Tuxedo with SQL Server
builder.Services.AddTuxedoSqlServer(
    builder.Configuration.GetConnectionString("SqlServer"));

// Or with PostgreSQL
builder.Services.AddTuxedoPostgres(
    builder.Configuration.GetConnectionString("Postgres"));

// Or with MySQL
builder.Services.AddTuxedoMySql(
    builder.Configuration.GetConnectionString("MySql"));

// Or with SQLite
builder.Services.AddTuxedoSqlite(
    builder.Configuration.GetConnectionString("Sqlite"));

var app = builder.Build();
```

### Advanced DI with SQL Dialects and Health Checks

```csharp
// Register with dialect and health checks
builder.Services.AddTuxedo(options =>
{
    options.ConnectionFactory = provider => 
        new SqlConnection(connectionString);
    options.Dialect = TuxedoDialect.SqlServer;
    options.OpenOnResolve = true;
    options.DefaultCommandTimeoutSeconds = 30;
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<TuxedoHealthCheck>("database");
```

### Using in Controllers/Services

```csharp
public class ProductService
{
    private readonly IDbConnection _connection;
    
    public ProductService(IDbConnection connection)
    {
        _connection = connection;
    }
    
    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        return await _connection.SelectAllAsync<Product>();
    }
    
    public async Task<Product> GetProductAsync(int id)
    {
        return await _connection.SelectAsync<Product>(id);
    }
    
    public async Task<int> CreateProductAsync(Product product)
    {
        return await _connection.InsertAsync(product);
    }
}
```

### Using ITuxedoConnectionFactory

For scenarios where you need to create connections on demand:

```csharp
public class BatchProcessor
{
    private readonly ITuxedoConnectionFactory _connectionFactory;
    
    public BatchProcessor(ITuxedoConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task ProcessBatchAsync(List<Product> products)
    {
        // Create a new connection for batch operation
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            foreach (var product in products)
            {
                await connection.InsertAsync(product, transaction);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

## SQLite Support

Tuxedo provides comprehensive SQLite support with specialized features for both production and testing scenarios:

### SQLite Setup Options

```csharp
// Standard SQLite with connection string
builder.Services.AddTuxedoSqlite("Data Source=app.db");

// File-based SQLite with auto-creation
builder.Services.AddTuxedoSqliteFile(
    databasePath: "data/app.db",
    createIfNotExists: true);

// In-memory SQLite for testing
builder.Services.AddTuxedoSqliteInMemory(
    databaseName: "TestDb", // Optional: named in-memory database
    lifetime: ServiceLifetime.Singleton); // Keep connection alive

// Configuration-based setup
builder.Services.AddTuxedoSqliteWithOptions(
    configuration,
    sectionName: "TuxedoSqlite");
```

### SQLite Configuration Options

```json
{
  "TuxedoSqlite": {
    "ConnectionString": "Data Source=app.db",
    "Mode": "ReadWriteCreate",
    "Cache": "Shared",
    "DefaultTimeout": 30,
    "Pooling": true,
    "ForeignKeys": true,
    "RecursiveTriggers": false,
    "OpenOnResolve": true
  }
}
```

### SQLite-Specific Features

```csharp
public class SqliteService
{
    private readonly SqliteConnection _connection;
    
    public SqliteService(SqliteConnection connection)
    {
        _connection = connection;
    }
    
    public async Task InitializeDatabaseAsync()
    {
        // Create tables with SQLite-specific syntax
        await _connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Category TEXT,
                LastModified TEXT DEFAULT CURRENT_TIMESTAMP
            )
        ");
        
        // Enable foreign keys (required per connection in SQLite)
        await _connection.ExecuteAsync("PRAGMA foreign_keys = ON");
    }
    
    public async Task<long> InsertWithLastRowIdAsync(Product product)
    {
        await _connection.InsertAsync(product);
        return _connection.LastInsertRowId;
    }
}
```

### Testing with In-Memory SQLite

```csharp
public class ProductServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IDbConnection _connection;
    
    public ProductServiceTests()
    {
        var services = new ServiceCollection();
        
        // Use in-memory SQLite for fast, isolated tests
        services.AddTuxedoSqliteInMemory("TestDb");
        
        _provider = services.BuildServiceProvider();
        _connection = _provider.GetRequiredService<IDbConnection>();
        
        // Initialize test schema
        InitializeTestDatabase();
    }
    
    private void InitializeTestDatabase()
    {
        _connection.Execute(@"
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL
            )
        ");
    }
    
    [Fact]
    public async Task Should_Insert_Product()
    {
        // Arrange
        var product = new Product { Name = "Test", Price = 9.99m };
        
        // Act
        var id = await _connection.InsertAsync(product);
        
        // Assert
        var retrieved = await _connection.SelectAsync<Product>(id);
        Assert.Equal("Test", retrieved.Name);
    }
    
    public void Dispose()
    {
        _connection?.Dispose();
        _provider?.Dispose();
    }
}
```

### SQLite Bulk Operations

SQLite supports efficient bulk operations with Tuxedo's bulk extensions:

```csharp
var bulkOps = new BulkOperations(TuxedoDialect.Sqlite);

// Bulk insert with SQLite
await bulkOps.BulkInsertAsync(connection, products, batchSize: 500);

// Bulk merge (upsert) using SQLite's ON CONFLICT
await bulkOps.BulkMergeAsync(connection, products);
```

## Enterprise Features

### Connection Resiliency

Automatically retry database operations on transient failures:

```csharp
using Tuxedo.Resiliency;

// Add resiliency with configuration
services.AddTuxedoResiliency(options =>
{
    options.MaxRetryAttempts = 3;
    options.BaseDelay = TimeSpan.FromSeconds(1);
    options.EnableCircuitBreaker = true;
    options.CircuitBreakerThreshold = 5;
});

// Use resilient connections
public class ResilientService
{
    private readonly Func<IDbConnection, IDbConnection> _wrapConnection;
    
    public ResilientService(Func<IDbConnection, IDbConnection> wrapConnection)
    {
        _wrapConnection = wrapConnection;
    }
    
    public async Task<Product> GetProductWithRetryAsync(int id)
    {
        using var connection = new SqlConnection(connectionString);
        using var resilientConnection = _wrapConnection(connection);
        
        // This will automatically retry on transient failures
        return await resilientConnection.SelectAsync<Product>(id);
    }
}
```

### Query Caching

Cache query results to improve performance:

```csharp
using Tuxedo.Caching;

// Add caching support
services.AddTuxedoCaching(options =>
{
    options.DefaultCacheDuration = TimeSpan.FromMinutes(5);
    options.MaxCacheSize = 1000;
});

// Use cached queries
public class CachedProductService
{
    private readonly IDbConnection _connection;
    private readonly IQueryCache _cache;
    
    public CachedProductService(IDbConnection connection, IQueryCache cache)
    {
        _connection = connection;
        _cache = cache;
    }
    
    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        var cacheKey = $"products_category_{category}";
        
        return await _cache.GetOrAddAsync(
            cacheKey,
            async () => await _connection.SelectAsync<Product>(
                "SELECT * FROM Products WHERE Category = @category",
                new { category }
            ),
            TimeSpan.FromMinutes(10),
            tags: new[] { "products", $"category:{category}" }
        );
    }
    
    public async Task InvalidateCategoryCache(string category)
    {
        await _cache.InvalidateByTagAsync($"category:{category}");
    }
}
```

### Fluent Query Builder

Build complex queries with a type-safe, fluent API. See the Pagination section above for comprehensive examples.

### Bulk Operations

Efficiently handle large data sets:

```csharp
using Tuxedo.BulkOperations;

public class BulkDataService
{
    private readonly IBulkOperations _bulkOps;
    private readonly IDbConnection _connection;
    
    public BulkDataService(IBulkOperations bulkOps, IDbConnection connection)
    {
        _bulkOps = bulkOps;
        _connection = connection;
    }
    
    public async Task<int> ImportProductsAsync(IEnumerable<Product> products)
    {
        // Bulk insert with automatic batching
        return await _bulkOps.BulkInsertAsync(
            _connection,
            products,
            batchSize: 1000,
            commandTimeout: 60
        );
    }
    
    public async Task<int> UpdatePricesAsync(IEnumerable<Product> products)
    {
        // Bulk update
        return await _bulkOps.BulkUpdateAsync(
            _connection,
            products,
            batchSize: 500
        );
    }
    
    public async Task<int> MergeProductsAsync(IEnumerable<Product> products)
    {
        // Upsert operation (insert or update)
        return await _bulkOps.BulkMergeAsync(
            _connection,
            products
        );
    }
}
```

### Repository Pattern

Use generic repositories for clean architecture:

```csharp
using Tuxedo.Patterns;

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<IEnumerable<Product>> GetTopSellingAsync(int count);
}

public class ProductRepository : DapperRepository<Product>, IProductRepository
{
    public ProductRepository(IDbConnection connection) : base(connection)
    {
    }
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var sql = "SELECT * FROM Products WHERE Category = @category";
        return await Connection.QueryAsync<Product>(sql, new { category });
    }
    
    public async Task<IEnumerable<Product>> GetTopSellingAsync(int count)
    {
        var sql = @"
            SELECT TOP(@count) p.* 
            FROM Products p
            INNER JOIN OrderItems oi ON p.Id = oi.ProductId
            GROUP BY p.Id, p.Name, p.Price, p.Category
            ORDER BY COUNT(oi.Id) DESC";
            
        return await Connection.QueryAsync<Product>(sql, new { count });
    }
}

// Usage with DI
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repository;
    
    public ProductController(IProductRepository repository)
    {
        _repository = repository;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null)
            return NotFound();
            
        return product;
    }
    
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<Product>>> GetByCategory(string category)
    {
        return Ok(await _repository.GetByCategoryAsync(category));
    }
}
```

### Unit of Work Pattern

Manage transactions across multiple repositories:

```csharp
using Tuxedo.Patterns;

public class OrderService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<Order> CreateOrderAsync(
        Guid customerId, 
        List<OrderItem> items)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Create order
            var orderRepo = _unitOfWork.Repository<Order>();
            var order = new Order
            {
                CustomerId = customerId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = items.Sum(i => i.Quantity * i.Price),
                Status = "Pending"
            };
            await orderRepo.AddAsync(order);
            
            // Add order items
            var itemRepo = _unitOfWork.Repository<OrderItem>();
            foreach (var item in items)
            {
                item.OrderId = order.OrderId;
                await itemRepo.AddAsync(item);
            }
            
            // Update inventory
            var productRepo = _unitOfWork.Repository<Product>();
            foreach (var item in items)
            {
                var product = await productRepo.GetByIdAsync(item.ProductId);
                product.StockQuantity -= item.Quantity;
                await productRepo.UpdateAsync(product);
            }
            
            await _unitOfWork.CommitAsync();
            return order;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
```

### Specification Pattern

Express complex business rules as specifications:

```csharp
using Tuxedo.Specifications;

public class ActiveProductSpecification : Specification<Product>
{
    public ActiveProductSpecification() 
        : base(p => p.IsActive && p.StockQuantity > 0)
    {
    }
}

public class PremiumProductSpecification : Specification<Product>
{
    public PremiumProductSpecification(decimal minPrice) 
        : base(p => p.Price >= minPrice)
    {
        ApplyOrderByDescending(p => p.Price);
    }
}

public class CategorySpecification : Specification<Product>
{
    public CategorySpecification(string category)
        : base(p => p.Category == category)
    {
    }
}

// Combine specifications
public class PremiumElectronicsSpecification : Specification<Product>
{
    public PremiumElectronicsSpecification()
    {
        AddCriteria(p => p.Category == "Electronics");
        AddCriteria(p => p.Price >= 1000);
        AddCriteria(p => p.IsActive);
        ApplyOrderByDescending(p => p.Price);
        ApplyPaging(0, 10);
    }
}

// Usage
public class SpecificationService
{
    private readonly IDbConnection _connection;
    
    public async Task<IEnumerable<Product>> GetPremiumElectronicsAsync()
    {
        var spec = new PremiumElectronicsSpecification();
        return await SpecificationEvaluator<Product>
            .GetQueryAsync(_connection, spec);
    }
}
```

### Logging and Diagnostics

Monitor and debug database operations:

```csharp
using Tuxedo.Diagnostics;

// Register diagnostics
services.AddSingleton<ITuxedoDiagnostics, TuxedoDiagnostics>();

// Use diagnostics
public class DiagnosticsExample
{
    private readonly ITuxedoDiagnostics _diagnostics;
    
    public DiagnosticsExample(ITuxedoDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
        
        // Subscribe to events
        _diagnostics.QueryExecuted += OnQueryExecuted;
        _diagnostics.ErrorOccurred += OnError;
    }
    
    private void OnQueryExecuted(object sender, QueryExecutedEventArgs e)
    {
        if (e.Duration.TotalSeconds > 1)
        {
            Console.WriteLine($"Slow query detected: {e.Query}");
            Console.WriteLine($"Duration: {e.Duration.TotalMilliseconds}ms");
        }
    }
    
    private void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"Database error: {e.Exception.Message}");
        Console.WriteLine($"Query: {e.Query}");
    }
}
```

## Advanced Features

### Custom Type Handlers

```csharp
public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value)
    {
        return Guid.Parse((string)value);
    }
}

// Register the handler
SqlMapper.AddTypeHandler(new GuidTypeHandler());
```

### Performance Optimization

```csharp
// Buffered vs non-buffered queries
// Buffered (default) - loads all results into memory
var allProducts = connection.Query<Product>("SELECT * FROM Products").ToList();

// Non-buffered - streams results
var largeResultSet = connection.Query<Product>(
    "SELECT * FROM Products", 
    buffered: false
);

foreach (var product in largeResultSet)
{
    // Process one at a time, minimal memory usage
    ProcessProduct(product);
}

// Async enumeration (.NET Core 3.0+)
await foreach (var product in connection.QueryUnbufferedAsync<Product>("SELECT * FROM Products"))
{
    await ProcessProductAsync(product);
}
```

## Best Practices

1. **Connection Management**: Always use `using` statements or dispose connections properly
2. **Parameterized Queries**: Always use parameters to prevent SQL injection
3. **Async Operations**: Use async methods for I/O bound operations
4. **Transaction Scope**: Use transactions for related operations
5. **Connection Pooling**: Rely on built-in connection pooling
6. **Batch Operations**: Use bulk operations for large data sets
7. **Caching Strategy**: Cache frequently accessed, rarely changing data
8. **Retry Policies**: Configure appropriate retry policies for production
9. **Health Checks**: Implement health checks for monitoring
10. **Specifications**: Use specifications for complex, reusable query logic
11. **Pagination**: Always paginate large result sets with appropriate page sizes
12. **Indexing**: Ensure proper indexes for pagination ORDER BY and WHERE clauses

## SQL-Aligned API

Tuxedo provides SQL verb-aligned methods that complement the traditional Dapper API:

| SQL Verb | Tuxedo Method | Legacy Method | Description |
|----------|---------------|---------------|-------------|
| SELECT | `Select<T>()` | `Query<T>()` | Execute a SELECT query |
| SELECT | `Select<T>(id)` | `Get<T>(id)` | Select by primary key |
| SELECT | `SelectAll<T>()` | `GetAll<T>()` | Select all records |
| INSERT | `Insert<T>()` | - | Insert a record |
| UPDATE | `Update<T>()` | - | Update a record |
| DELETE | `Delete<T>()` | - | Delete a record |

All methods have async variants (`SelectAsync`, `InsertAsync`, etc.). The SQL-aligned methods are aliases that provide a more intuitive, SQL-like API while maintaining full backward compatibility with existing Dapper code.

## Migration from Dapper/Dapper.Contrib

Tuxedo is designed to be a drop-in replacement. Simply:

1. Replace `using Dapper;` with `using Tuxedo;`
2. Replace `using Dapper.Contrib.Extensions;` with `using Tuxedo.Contrib;`
3. Update package references from `Dapper` and `Dapper.Contrib` to `Tuxedo`
4. Optionally, adopt the SQL-aligned methods (`Select`, `Insert`, `Update`, `Delete`) for new code
5. Leverage enterprise features (resiliency, caching, pagination, etc.) as needed

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.

## Acknowledgments

Tuxedo builds upon the excellent work of the Dapper and Dapper.Contrib teams, modernizing and unifying their functionality for contemporary .NET development with enterprise-grade features.