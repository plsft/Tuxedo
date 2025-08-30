using Bowtie.Extensions;
using Bowtie.Samples.WebApi.Models;
using Bowtie.Core;
using Microsoft.Data.Sqlite;
using System.Data;
using Tuxedo;
using Tuxedo.Contrib;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Bowtie services
builder.Services.AddBowtie();

// Add database connection
builder.Services.AddScoped<IDbConnection>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                          ?? "Data Source=webapi_demo.db";
    return new SqliteConnection(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Synchronize database schema in development
    try
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                              ?? "Data Source=webapi_demo.db";
        
        app.Logger.LogInformation("Synchronizing database schema...");
        
        await app.Services.SynchronizeDatabaseAsync(
            connectionString: connectionString,
            provider: DatabaseProvider.SQLite,
            dryRun: false
        );
        
        app.Logger.LogInformation("Database schema synchronized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to synchronize database schema");
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Add some sample endpoints
app.MapGet("/", () => "Bowtie Web API Sample - Navigate to /swagger to see the API documentation");

app.MapGet("/schema/info", async (IServiceProvider services) =>
{
    var modelAnalyzer = services.GetRequiredService<Bowtie.Analysis.ModelAnalyzer>();
    
    var modelTypes = new[]
    {
        typeof(Product),
        typeof(Category),
        typeof(Order),
        typeof(Customer)
    };
    
    var tables = modelAnalyzer.AnalyzeTypes(modelTypes);
    
    return new
    {
        TableCount = tables.Count,
        Tables = tables.Select(t => new
        {
            t.Name,
            t.Schema,
            ColumnCount = t.Columns.Count,
            IndexCount = t.Indexes.Count,
            ConstraintCount = t.Constraints.Count,
            Columns = t.Columns.Select(c => new
            {
                c.Name,
                c.DataType,
                c.IsNullable,
                c.IsPrimaryKey,
                c.IsIdentity,
                DefaultValue = c.DefaultValue?.ToString()
            }),
            Indexes = t.Indexes.Select(i => new
            {
                i.Name,
                i.IsUnique,
                IndexType = i.IndexType.ToString(),
                Columns = i.Columns.Select(c => c.ColumnName)
            })
        })
    };
});

app.MapGet("/schema/generate", async (IServiceProvider services, string provider = "sqlite") =>
{
    try
    {
        var dbProvider = Enum.Parse<DatabaseProvider>(provider, true);
        var outputPath = $"schema_{provider.ToLower()}_{DateTime.Now:yyyyMMddHHmmss}.sql";
        
        await services.GenerateDdlScriptsAsync(
            provider: dbProvider,
            outputPath: outputPath
        );
        
        var script = await File.ReadAllTextAsync(outputPath);
        
        return Results.Ok(new
        {
            Provider = provider,
            Script = script,
            GeneratedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.MapGet("/schema/validate", (IServiceProvider services, string provider = "sqlite") =>
{
    try
    {
        var dbProvider = Enum.Parse<DatabaseProvider>(provider, true);
        var isValid = services.ValidateModels(dbProvider);
        
        return Results.Ok(new
        {
            Provider = provider,
            IsValid = isValid,
            ValidatedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Sample data endpoints
app.MapGet("/api/products", async (IDbConnection db) =>
{
    if (db.State != ConnectionState.Open) db.Open();
    var products = await db.QueryAsync<Product>("SELECT * FROM Products ORDER BY Name");
    return Results.Ok(products);
});

app.MapPost("/api/products", async (Product product, IDbConnection db) =>
{
    if (db.State != ConnectionState.Open) db.Open();
    product.CreatedDate = DateTime.UtcNow;
    var id = await db.InsertAsync(product);
    product.Id = id;
    return Results.Created($"/api/products/{id}", product);
});

app.MapGet("/api/categories", async (IDbConnection db) =>
{
    if (db.State != ConnectionState.Open) db.Open();
    var categories = await db.QueryAsync<Category>("SELECT * FROM Categories ORDER BY Name");
    return Results.Ok(categories);
});

app.MapPost("/api/categories", async (Category category, IDbConnection db) =>
{
    if (db.State != ConnectionState.Open) db.Open();
    category.CreatedDate = DateTime.UtcNow;
    var id = await db.InsertAsync(category);
    category.Id = id;
    return Results.Created($"/api/categories/{id}", category);
});

app.Run();