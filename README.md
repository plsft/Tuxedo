# Tuxedo

Tuxedo merges Dapper and Dapper.Contrib functionality into a single package for modern .NET. It provides high‑performance ADO.NET mapping and CRUD helpers that work with SQL Server, PostgreSQL, MySQL, and SQLite.

## Features

- High performance query/execute APIs (`Query`, `Execute`, async variants)
- Contrib‑style CRUD: `Get`/`GetAll`, `Insert`, `Update`, `Delete` (+ async)
- **Partial Updates**: `UpdatePartial`/`UpdatePartialAsync` for selective column updates
- Attribute mapping: `[Table]`, `[Key]`, `[ExplicitKey]`, `[Computed]`
- Works with any ADO.NET provider (SqlClient, Npgsql, MySqlConnector, Sqlite)
- Optional DI helpers for registering a connection, including in‑memory SQLite

## Installation

- Library: `dotnet add package Tuxedo`
- Providers as needed: `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`

## Quick Start

```csharp
using System.Data;
using Tuxedo;           // Dapper APIs
using Tuxedo.Contrib;   // CRUD helpers
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
db.UpdatePartial(product, new[] { "Name" }); // Only updates Name column

// Partial Updates with key/value objects
db.UpdatePartial<Product>(
    keyValues: new { Id = id },
    updateValues: new { Name = "New Name", Price = 29.99m }
);

// Partial Updates (async)
await db.UpdatePartialAsync(product, new[] { "Name", "Category" });
```

## Partial Updates

Tuxedo supports efficient partial updates that only modify specified columns:

```csharp
// Update only specific properties by name
var product = db.Get<Product>(1);
product.Name = "Updated Product";
product.LastModified = DateTime.Now;
// Price remains unchanged in database
db.UpdatePartial(product, new[] { "Name", "LastModified" });

// Update using separate key and value objects
db.UpdatePartial<Product>(
    keyValues: new { Id = 1 },
    updateValues: new { Name = "Direct Update", Price = 39.99m }
);

// Async versions available
await db.UpdatePartialAsync(product, new[] { "Name" });
await db.UpdatePartialAsync<Product>(
    keyValues: new { Id = 1 },
    updateValues: new { Category = "Updated Category" }
);
```

**Partial Update Features:**
- Updates only specified columns, leaving others unchanged
- Supports both property name arrays and key/value object patterns
- Case-insensitive property name matching
- Validates property names and prevents key column updates
- Ignores `[Computed]` properties automatically
- Full async support with `UpdatePartialAsync` methods

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
- Partial update helpers are available as `UpdatePartial` and `UpdatePartialAsync` with property name filtering.

## Status

This README documents the APIs present in the codebase today. Features like query builders, repositories, specifications, or advanced pagination are not included.
