# Bowtie - Getting Started Guide

This guide will walk you through setting up and using Bowtie to manage database schemas with your Tuxedo ORM models.

## Prerequisites

- .NET 6.0, 8.0, or 9.0
- Tuxedo ORM library
- Database provider packages (Microsoft.Data.SqlClient, Npgsql, MySqlConnector, Microsoft.Data.Sqlite)

## Installation

### 1. Install Bowtie Library

```bash
# Add Bowtie to your project
dotnet add package Bowtie

# Add your database provider
dotnet add package Microsoft.Data.SqlClient  # SQL Server
# or
dotnet add package Npgsql                    # PostgreSQL
# or  
dotnet add package MySqlConnector            # MySQL
# or
dotnet add package Microsoft.Data.Sqlite    # SQLite
```

### 2. Install Bowtie CLI (Optional)

```bash
# Install globally
dotnet tool install -g Bowtie.CLI

# Or install locally
dotnet new tool-manifest
dotnet tool install Bowtie.CLI
```

## Quick Start

### Step 1: Define Your Models

Create your model classes using Tuxedo and Bowtie attributes:

```csharp
using Tuxedo.Contrib;
using Bowtie.Attributes;

[Table("Users")]
public class User
{
    [Key]  // Tuxedo attribute for primary key
    public int Id { get; set; }

    [Column(MaxLength = 100)]  // Bowtie attribute for column specifications
    [Index("IX_Users_Username")]  // Bowtie attribute for indexes
    [Unique("UQ_Users_Username")]  // Bowtie attribute for unique constraints
    public string Username { get; set; } = string.Empty;

    [Column(MaxLength = 200)]
    public string Email { get; set; } = string.Empty;

    [DefaultValue(true)]  // Bowtie attribute for default values
    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}

[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Column(MaxLength = 200)]
    [Index("IX_Products_Name")]
    public string Name { get; set; } = string.Empty;

    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("Price > 0")]  // Bowtie attribute for check constraints
    public decimal Price { get; set; }

    [ForeignKey("Categories")]  // Bowtie attribute for foreign keys
    public int CategoryId { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; }
}
```

### Step 2: Generate DDL Scripts

#### Using the Programmatic API

```csharp
using Bowtie.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBowtie();

var app = builder.Build();

// Generate DDL scripts for different providers
await app.Services.GenerateDdlScriptsAsync(
    provider: DatabaseProvider.SqlServer,
    outputPath: "schema_sqlserver.sql"
);

await app.Services.GenerateDdlScriptsAsync(
    provider: DatabaseProvider.PostgreSQL,
    outputPath: "schema_postgresql.sql"
);
```

#### Using the CLI Tool

```bash
# Generate DDL scripts
bowtie generate \
  --assembly ./bin/Debug/net8.0/MyApp.dll \
  --provider SqlServer \
  --output schema.sql

# Validate models for target database
bowtie validate \
  --assembly ./bin/Debug/net8.0/MyApp.dll \
  --provider PostgreSQL
```

### Step 3: Synchronize Database (Development)

#### ASP.NET Core Integration

```csharp
using Bowtie.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Bowtie services
builder.Services.AddBowtie();

var app = builder.Build();

// Synchronize database schema in development
if (app.Environment.IsDevelopment())
{
    await app.Services.SynchronizeDatabaseAsync(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        provider: DatabaseProvider.SqlServer,
        dryRun: false  // Set to true to generate SQL without executing
    );
}

app.Run();
```

#### Console Application

```csharp
using Bowtie.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBowtie();

var app = builder.Build();

// Synchronize with database
await app.Services.SynchronizeDatabaseAsync(
    connectionString: "Data Source=myapp.db",
    provider: DatabaseProvider.SQLite,
    dryRun: false
);
```

## Advanced Features

### Database-Specific Index Types

#### PostgreSQL JSONB with GIN Indexes

```csharp
[Table("Documents")]
public class Document
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "jsonb")]
    [Index("IX_Documents_Content_GIN", IndexType = IndexType.GIN)]
    public string Content { get; set; } = "{}";

    [Column(TypeName = "text[]")]
    [Index("IX_Documents_Tags_GIN", IndexType = IndexType.GIN)]  
    public string[] Tags { get; set; } = Array.Empty<string>();
}
```

Generated PostgreSQL DDL:
```sql
CREATE TABLE "Documents" (
  "Id" INTEGER NOT NULL GENERATED ALWAYS AS IDENTITY,
  "Content" jsonb NOT NULL,
  "Tags" text[] NOT NULL,
  CONSTRAINT "PK_Documents" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Documents_Content_GIN" ON "Documents" USING GIN ("Content");
CREATE INDEX "IX_Documents_Tags_GIN" ON "Documents" USING GIN ("Tags");
```

#### SQL Server Clustered Indexes

```csharp
[Table("Analytics")]
public class Analytics
{
    [Key]
    public long Id { get; set; }

    [Index("IX_Analytics_Date_Clustered", IndexType = IndexType.Clustered)]
    public DateTime Date { get; set; }

    [Column(MaxLength = 50)]
    [Index("IX_Analytics_EventType", IndexType = IndexType.NonClustered)]
    public string EventType { get; set; } = string.Empty;
}
```

Generated SQL Server DDL:
```sql
CREATE TABLE [Analytics] (
  [Id] BIGINT NOT NULL IDENTITY(1,1),
  [Date] DATETIME2 NOT NULL,
  [EventType] NVARCHAR(50) NOT NULL,
  CONSTRAINT [PK_Analytics] PRIMARY KEY ([Id])
);

CREATE CLUSTERED INDEX [IX_Analytics_Date_Clustered] ON [Analytics] ([Date] ASC);
CREATE NONCLUSTERED INDEX [IX_Analytics_EventType] ON [Analytics] ([EventType] ASC);
```

### Complex Constraints

#### Composite Primary Keys

```csharp
[Table("OrderItems")]
public class OrderItem
{
    [PrimaryKey(Order = 1)]
    [ForeignKey("Orders")]
    public int OrderId { get; set; }

    [PrimaryKey(Order = 2)]
    [ForeignKey("Products")]
    public int ProductId { get; set; }

    [CheckConstraint("Quantity > 0")]
    public int Quantity { get; set; }
}
```

#### Foreign Keys with Referential Actions

```csharp
[Table("Comments")]
public class Comment
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Posts", OnDelete = ReferentialAction.Cascade)]
    public int PostId { get; set; }

    [ForeignKey("Users", OnDelete = ReferentialAction.SetNull)]
    public int? AuthorId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; } = string.Empty;
}
```

### Multi-Column Indexes

```csharp
[Table("Products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Index("IX_Products_Category_Price", Group = "CategoryPrice", Order = 1)]
    [Column(MaxLength = 50)]
    public string Category { get; set; } = string.Empty;

    [Index("IX_Products_Category_Price", Group = "CategoryPrice", Order = 2)]
    [Column(Precision = 18, Scale = 2)]
    public decimal Price { get; set; }

    [Column(MaxLength = 200)]
    public string Name { get; set; } = string.Empty;
}
```

## Provider Feature Matrix

| Feature | SQL Server | PostgreSQL | MySQL | SQLite |
|---------|------------|------------|-------|---------|
| **Basic DDL** | ✅ | ✅ | ✅ | ✅ |
| **Schemas** | ✅ | ✅ | ❌ | ❌ |
| **Index Types** |
| B-Tree | ✅ | ✅ | ✅ | ✅ |
| Hash | ❌ | ✅ | ✅ | ❌ |
| GIN | ❌ | ✅ | ❌ | ❌ |
| GiST | ❌ | ✅ | ❌ | ❌ |
| BRIN | ❌ | ✅ | ❌ | ❌ |
| Clustered | ✅ | ❌ | ❌ | ❌ |
| ColumnStore | ✅ | ❌ | ❌ | ❌ |
| FullText | ✅ | ❌ | ✅ | ❌ |
| Spatial | ✅ | ✅ | ✅ | ❌ |
| **Constraints** |
| Primary Key | ✅ | ✅ | ✅ | ✅ |
| Foreign Key | ✅ | ✅ | ✅ | ⚠️ Limited |
| Unique | ✅ | ✅ | ✅ | ✅ |
| Check | ✅ | ✅ | ✅ | ✅ |
| **Operations** |
| Create Table | ✅ | ✅ | ✅ | ✅ |
| Drop Table | ✅ | ✅ | ✅ | ✅ |
| Add Column | ✅ | ✅ | ✅ | ✅ |
| Drop Column | ✅ | ✅ | ✅ | ⚠️ Requires rebuild |
| Alter Column | ✅ | ✅ | ✅ | ⚠️ Requires rebuild |

## Build Integration

### MSBuild Target

Add this to your `.csproj` file to run Bowtie during build:

```xml
<PropertyGroup>
  <BowtieConnectionString>Data Source=myapp.db</BowtieConnectionString>
  <BowtieProvider>SQLite</BowtieProvider>
</PropertyGroup>

<Target Name="GenerateSchema" BeforeTargets="Build" Condition="'$(Configuration)' == 'Debug'">
  <Message Text="Generating database schema with Bowtie..." Importance="high" />
  <Exec Command="bowtie generate --assembly $(OutputPath)$(AssemblyName).dll --provider $(BowtieProvider) --output $(OutputPath)schema.sql" 
        ContinueOnError="true" 
        Condition="Exists('$(OutputPath)$(AssemblyName).dll')" />
</Target>

<Target Name="SyncDatabase" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug' AND '$(BowtieAutoSync)' == 'true'">
  <Message Text="Synchronizing database schema..." Importance="high" />
  <Exec Command="bowtie sync --assembly $(OutputPath)$(AssemblyName).dll --connection-string &quot;$(BowtieConnectionString)&quot; --provider $(BowtieProvider)" 
        ContinueOnError="false"
        Condition="Exists('$(OutputPath)$(AssemblyName).dll')" />
</Target>
```

Enable auto-sync by setting:
```xml
<PropertyGroup>
  <BowtieAutoSync>true</BowtieAutoSync>
</PropertyGroup>
```

### Directory.Build.props Integration

Create a `Directory.Build.props` file at your solution root:

```xml
<Project>
  <PropertyGroup>
    <BowtieConnectionString Condition="'$(BowtieConnectionString)' == ''">Data Source=dev.db</BowtieConnectionString>
    <BowtieProvider Condition="'$(BowtieProvider)' == ''">SQLite</BowtieProvider>
    <BowtieAutoSync Condition="'$(BowtieAutoSync)' == ''">false</BowtieAutoSync>
  </PropertyGroup>

  <Target Name="BowtieGenerate" BeforeTargets="Build" Condition="'$(Configuration)' == 'Debug' AND Exists('$(OutputPath)$(AssemblyName).dll')">
    <Exec Command="dotnet tool run bowtie generate --assembly $(OutputPath)$(AssemblyName).dll --provider $(BowtieProvider) --output schema_$(BowtieProvider.ToLower()).sql" 
          ContinueOnError="true" />
  </Target>
</Project>
```

## Troubleshooting

### Common Issues

1. **Assembly Loading Errors**
   - Ensure all dependencies are in the output directory
   - Use absolute paths when possible
   - Check that the target assembly contains Tuxedo models

2. **Connection String Issues**
   - Test connection strings separately
   - Escape special characters in MSBuild
   - Use environment variables for sensitive data

3. **Provider Compatibility**
   - Check the feature matrix above
   - Use `bowtie validate` to check model compatibility
   - Some index types are provider-specific

### Debug Tips

```bash
# Use verbose logging
bowtie sync --assembly ./MyApp.dll --connection-string "..." --provider SqlServer --verbose

# Generate script only (dry run)
bowtie sync --assembly ./MyApp.dll --connection-string "..." --provider SqlServer --dry-run --output debug.sql

# Validate models before deployment
bowtie validate --assembly ./MyApp.dll --provider PostgreSQL
```

### Performance Tips

1. **Index Strategy**
   - Use appropriate index types for your database
   - Consider composite indexes for common query patterns
   - Use partial indexes (WHERE clauses) when applicable

2. **Schema Design**
   - Keep table names simple and consistent
   - Use meaningful constraint names
   - Consider database-specific features (JSONB, spatial data)

3. **Migration Strategy**
   - Always test with `--dry-run` first
   - Backup databases before running migrations
   - Use staging environments for testing

## Best Practices

### Model Design

```csharp
[Table("Orders")]
public class Order
{
    // Always use explicit key attribute
    [Key]
    public int Id { get; set; }

    // Use descriptive constraint names
    [Column(MaxLength = 50)]
    [Index("IX_Orders_OrderNumber")]
    [Unique("UQ_Orders_OrderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    // Use check constraints for business rules
    [Column(Precision = 18, Scale = 2)]
    [CheckConstraint("TotalAmount >= 0")]
    public decimal TotalAmount { get; set; }

    // Use foreign keys with appropriate referential actions
    [ForeignKey("Customers", OnDelete = ReferentialAction.Restrict)]
    [Index("IX_Orders_Customer")]
    public int CustomerId { get; set; }

    // Use default values for audit fields
    [DefaultValue("GETUTCDATE()", IsRawSql = true)]
    public DateTime CreatedDate { get; set; }

    // Mark computed properties
    [Computed]
    public string DisplayName => $"Order #{OrderNumber}";
}
```

### Environment Configuration

```json
{
  "ConnectionStrings": {
    "Development": "Data Source=dev.db",
    "Staging": "Server=staging;Database=MyApp;Integrated Security=true",
    "Production": "Server=prod;Database=MyApp;Integrated Security=true"
  },
  "Bowtie": {
    "Provider": "SqlServer",
    "DefaultSchema": "dbo",
    "AutoMigrate": false,
    "GenerateScripts": true,
    "OutputDirectory": "./migrations"
  }
}
```

### CI/CD Integration

#### GitHub Actions

```yaml
name: Database Migration

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  migrate:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Install Bowtie CLI
      run: dotnet tool install -g Bowtie.CLI
    
    - name: Build Application
      run: dotnet build --configuration Release
    
    - name: Generate Migration Script
      run: |
        bowtie generate \
          --assembly ./bin/Release/net8.0/MyApp.dll \
          --provider SqlServer \
          --output migration.sql
    
    - name: Upload Migration Script
      uses: actions/upload-artifact@v3
      with:
        name: migration-script
        path: migration.sql
    
    # For development/staging environments
    - name: Apply Migration (Staging)
      if: github.ref == 'refs/heads/develop'
      run: |
        bowtie sync \
          --assembly ./bin/Release/net8.0/MyApp.dll \
          --connection-string "${{ secrets.STAGING_CONNECTION_STRING }}" \
          --provider SqlServer
```

#### Azure DevOps

```yaml
trigger:
  branches:
    include:
    - main
    - develop

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

steps:
- task: UseDotNet@2
  displayName: 'Use .NET 8 SDK'
  inputs:
    packageType: 'sdk'
    version: '8.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Install Bowtie CLI'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'install -g Bowtie.CLI'

- task: DotNetCoreCLI@2
  displayName: 'Build Application'
  inputs:
    command: 'build'
    arguments: '--configuration $(buildConfiguration)'

- task: PowerShell@2
  displayName: 'Generate Migration Script'
  inputs:
    targetType: 'inline'
    script: |
      bowtie generate `
        --assembly ./bin/$(buildConfiguration)/net8.0/MyApp.dll `
        --provider SqlServer `
        --output $(Build.ArtifactStagingDirectory)/migration.sql

- task: PublishBuildArtifacts@1
  displayName: 'Publish Migration Script'
  inputs:
    pathToPublish: '$(Build.ArtifactStagingDirectory)'
    artifactName: 'migration'
```

## Next Steps

1. **Start Simple**: Begin with basic models and SQLite
2. **Add Complexity**: Gradually introduce indexes and constraints
3. **Test Thoroughly**: Use `--dry-run` and validate generated SQL
4. **Automate**: Integrate with your build/deployment pipeline
5. **Monitor**: Track schema changes in version control

## Examples Repository

For complete working examples, see:
- `samples/Bowtie.Samples.Console` - Console application examples
- `samples/Bowtie.Samples.WebApi` - ASP.NET Core Web API integration
- `tests/Bowtie.Tests` - Unit tests demonstrating all features

## Support

- 📝 **Documentation**: See README.md for complete API reference
- 🐛 **Issues**: Report bugs and feature requests on GitHub
- 💡 **Discussions**: Join community discussions
- 📧 **Support**: Contact the Tuxedo team for enterprise support