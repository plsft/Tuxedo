# Tuxedo

Tuxedo is a modernized .NET data access library that merges Dapper and Dapper.Contrib functionality into a single, unified package optimized for .NET 6+. It provides high-performance object mapping with built-in support for SQL Server, PostgreSQL, and MySQL, along with enterprise-grade features for resilience, caching, and advanced query patterns.

## Features

### Core Features
- **High Performance**: Built on Dapper's proven performance characteristics
- **Multi-Database Support**: Native adapters for SQL Server, PostgreSQL, and MySQL with dialect-aware SQL generation
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

Build complex queries with a type-safe, fluent API:

```csharp
using Tuxedo.QueryBuilder;

public class ProductQueryService
{
    private readonly IDbConnection _connection;
    
    public async Task<IEnumerable<Product>> SearchProductsAsync(
        string? category = null, 
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null)
    {
        var query = QueryBuilderExtensions.Query<Product>()
            .SelectAll();
        
        if (!string.IsNullOrEmpty(category))
            query.Where(p => p.Category == category);
            
        if (minPrice.HasValue)
            query.Where($"Price >= {minPrice.Value}");
            
        if (maxPrice.HasValue)
            query.Where($"Price <= {maxPrice.Value}");
            
        if (sortBy == "price_asc")
            query.OrderBy(p => p.Price);
        else if (sortBy == "price_desc")
            query.OrderByDescending(p => p.Price);
        else
            query.OrderBy(p => p.Name);
            
        return await query.ToListAsync(_connection);
    }
    
    public async Task<PagedResult<Product>> GetPagedProductsAsync(
        int page, 
        int pageSize,
        string? category = null)
    {
        var query = QueryBuilderExtensions.Query<Product>()
            .SelectAll();
            
        if (!string.IsNullOrEmpty(category))
            query.Where(p => p.Category == category);
            
        query.Page(page, pageSize);
        
        var items = await query.ToListAsync(_connection);
        var total = await query.CountAsync(_connection);
        
        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
```

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
5. Leverage enterprise features (resiliency, caching, etc.) as needed

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.

## Acknowledgments

Tuxedo builds upon the excellent work of the Dapper and Dapper.Contrib teams, modernizing and unifying their functionality for contemporary .NET development with enterprise-grade features.