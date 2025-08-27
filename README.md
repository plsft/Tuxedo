# Tuxedo

Tuxedo is a modernized .NET data access library that merges Dapper and Dapper.Contrib functionality into a single, unified package optimized for .NET 6+. It provides high-performance object mapping with built-in support for SQL Server, PostgreSQL, and MySQL.

## Features

- **High Performance**: Built on Dapper's proven performance characteristics
- **Multi-Database Support**: Native adapters for SQL Server, PostgreSQL, and MySQL
- **Modern .NET**: Optimized for .NET 6, .NET 8, and .NET 9
- **SQL-Aligned CRUD Operations**: Native Insert, Update, Delete, and Select methods matching SQL verbs
- **Query Support**: Full Dapper query capabilities with async support
- **Type Safety**: Strongly-typed mapping with nullable reference type support
- **Dual API**: Supports both legacy Dapper methods (Query, Get) and SQL-aligned methods (Select)
- **Dependency Injection**: Built-in IServiceCollection extensions for modern DI patterns

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

### Advanced DI Configuration

#### Custom Connection Factory

```csharp
// Register with custom factory
builder.Services.AddTuxedo(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<SqlConnection>>();
    
    var connection = new SqlConnection(config.GetConnectionString("Default"));
    // Custom initialization logic
    connection.InfoMessage += (sender, e) => logger.LogInformation(e.Message);
    
    return connection;
}, ServiceLifetime.Scoped);
```

#### Configuration-Based Setup

```csharp
// appsettings.json
{
  "TuxedoSqlServer": {
    "ConnectionString": "Server=localhost;Database=MyApp;...",
    "CommandTimeout": 30,
    "MultipleActiveResultSets": true,
    "TrustServerCertificate": true
  },
  "TuxedoPostgres": {
    "ConnectionString": "Host=localhost;Database=myapp;...",
    "Pooling": true,
    "MinPoolSize": 5,
    "MaxPoolSize": 100
  }
}

// Program.cs
builder.Services.AddTuxedoSqlServerWithOptions(
    builder.Configuration, 
    "TuxedoSqlServer");

builder.Services.AddTuxedoPostgresWithOptions(
    builder.Configuration, 
    "TuxedoPostgres");
```

#### Provider-Specific Configuration

```csharp
// SQL Server with configuration
builder.Services.AddTuxedoSqlServer(
    connectionString,
    connection =>
    {
        // Configure connection properties
        connection.FireInfoMessageEventOnUserErrors = true;
        connection.StatisticsEnabled = true;
    },
    ServiceLifetime.Scoped);

// PostgreSQL with configuration
builder.Services.AddTuxedoPostgres(
    connectionString,
    connection =>
    {
        // Configure Npgsql-specific settings
        connection.UserCertificateValidationCallback = ValidateCertificate;
    },
    ServiceLifetime.Scoped);

// MySQL with configuration
builder.Services.AddTuxedoMySql(
    connectionString,
    connection =>
    {
        // Configure MySQL-specific settings
        connection.IgnorePrepare = false;
    },
    ServiceLifetime.Scoped);
```

#### Using with Options Pattern

```csharp
// Define your options
public class DatabaseOptions
{
    public string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetries { get; set; } = true;
}

// Configure options
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

// Register with options
builder.Services.AddTuxedoWithOptions<IOptions<DatabaseOptions>>(
    (provider, options) =>
    {
        var dbOptions = options.Value;
        var connection = new SqlConnection(dbOptions.ConnectionString);
        // Apply configuration from options
        return connection;
    },
    ServiceLifetime.Scoped);
```

### Connection Lifetime Best Practices

By default, Tuxedo registers connections with **Scoped** lifetime, which is the recommended approach:

- **Scoped (Default)**: One connection per request - ideal for web applications
- **Transient**: New connection for each injection - useful for parallel operations
- **Singleton**: Single connection for application lifetime - use with caution

```csharp
// Scoped - Recommended for web apps (default)
builder.Services.AddTuxedoSqlServer(connectionString);

// Transient - For parallel operations
builder.Services.AddTuxedoSqlServer(connectionString, ServiceLifetime.Transient);

// Singleton - Use carefully, ensure thread safety
builder.Services.AddTuxedoSqlServer(connectionString, ServiceLifetime.Singleton);
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

### Bulk Operations

```csharp
// Bulk insert using TVP (SQL Server)
var products = GetLargeProductList(); // 10000+ products

var table = new DataTable();
table.Columns.Add("Name", typeof(string));
table.Columns.Add("Price", typeof(decimal));
table.Columns.Add("Category", typeof(string));

foreach (var product in products)
{
    table.Rows.Add(product.Name, product.Price, product.Category);
}

var tvp = table.AsTableValuedParameter("ProductTableType");
connection.Execute("sp_BulkInsertProducts", new { products = tvp }, 
    commandType: CommandType.StoredProcedure);
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

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.

## Acknowledgments

Tuxedo builds upon the excellent work of the Dapper and Dapper.Contrib teams, modernizing and unifying their functionality for contemporary .NET development.