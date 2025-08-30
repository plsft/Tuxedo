# Bowtie MSBuild Integration Examples

This directory contains examples of integrating Bowtie with MSBuild for automated database schema management.

## Directory.Build.props

The `Directory.Build.props` file provides solution-wide Bowtie configuration that applies to all projects in your solution.

### Features:

- **Automatic Model Validation**: Validates models during build
- **DDL Script Generation**: Generates migration scripts automatically
- **Multi-Provider Support**: Generate scripts for multiple database providers
- **Environment-Aware**: Different behavior for Debug/Release configurations
- **Configurable**: Override settings per project

### Usage:

1. Place `Directory.Build.props` at your solution root
2. Configure default settings for your environment
3. Override in individual projects as needed

## Project-Level Integration

### Basic Configuration

```xml
<PropertyGroup>
  <BowtieProvider>SqlServer</BowtieProvider>
  <BowtieConnectionString>Server=.;Database=MyApp;Integrated Security=true</BowtieConnectionString>
  <BowtieAutoSync>false</BowtieAutoSync>
</PropertyGroup>
```

### Development Environment

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <BowtieProvider>SQLite</BowtieProvider>
  <BowtieConnectionString>Data Source=./dev.db</BowtieConnectionString>
  <BowtieAutoSync>true</BowtieAutoSync>
  <BowtieVerbose>true</BowtieVerbose>
</PropertyGroup>
```

### Production Environment

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <BowtieProvider>SqlServer</BowtieProvider>
  <BowtieConnectionString>$(PRODUCTION_CONNECTION_STRING)</BowtieConnectionString>
  <BowtieAutoSync>false</BowtieAutoSync>
  <BowtieGenerateScripts>true</BowtieGenerateScripts>
</PropertyGroup>
```

## Custom MSBuild Targets

### Environment-Specific Deployment

Create custom targets for different environments:

```xml
<!-- Development - use SQLite -->
<Target Name="DeployDev">
  <PropertyGroup>
    <DevConnectionString>Data Source=./dev.db</DevConnectionString>
  </PropertyGroup>
  
  <Exec Command="dotnet bowtie sync --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --connection-string &quot;$(DevConnectionString)&quot; --provider SQLite" />
</Target>

<!-- Staging - generate script only -->
<Target Name="DeployStaging">
  <Exec Command="dotnet bowtie sync --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --connection-string &quot;$(STAGING_CONNECTION_STRING)&quot; --provider SqlServer --dry-run --output staging_migration.sql" />
</Target>

<!-- Production - validate only, never auto-sync -->
<Target Name="ValidateProduction">
  <Exec Command="dotnet bowtie validate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider SqlServer" />
</Target>
```

### Usage:

```bash
# Deploy to development
dotnet build -t:DeployDev

# Generate staging migration
dotnet build -t:DeployStaging

# Validate for production
dotnet build -t:ValidateProduction
```

## Docker Integration

### Dockerfile with Bowtie

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Install Bowtie CLI
RUN dotnet tool install -g Bowtie.CLI

# Copy project files
COPY *.csproj ./
RUN dotnet restore

# Copy source
COPY . ./
RUN dotnet build -c Release

# Generate migration scripts
RUN dotnet bowtie generate \
    --assembly ./bin/Release/net8.0/MyApp.dll \
    --provider SqlServer \
    --output migration.sql

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /src/bin/Release/net8.0 ./
COPY --from=build /src/migration.sql ./

ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### Docker Compose with Database Migration

```yaml
version: '3.8'

services:
  database:
    image: postgres:15
    environment:
      POSTGRES_DB: myapp
      POSTGRES_USER: myapp
      POSTGRES_PASSWORD: password123
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  migration:
    build: .
    depends_on:
      - database
    environment:
      CONNECTION_STRING: "Host=database;Database=myapp;Username=myapp;Password=password123"
    command: >
      sh -c "
        dotnet tool install -g Bowtie.CLI &&
        dotnet bowtie sync 
          --assembly ./MyApp.dll 
          --connection-string \"$$CONNECTION_STRING\" 
          --provider PostgreSQL
      "
    volumes:
      - ./migrations:/app/migrations

  app:
    build: .
    depends_on:
      - migration
    environment:
      ConnectionStrings__DefaultConnection: "Host=database;Database=myapp;Username=myapp;Password=password123"
    ports:
      - "8080:80"

volumes:
  postgres_data:
```

## Advanced Scenarios

### Multi-Tenant Applications

```xml
<Target Name="BowtieMultiTenant" Condition="'$(MultiTenant)' == 'true'">
  <ItemGroup>
    <TenantSchema Include="tenant1" />
    <TenantSchema Include="tenant2" />
    <TenantSchema Include="tenant3" />
  </ItemGroup>

  <Exec Command="dotnet bowtie sync --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --connection-string &quot;$(BowtieConnectionString)&quot; --provider $(BowtieProvider) --schema %(TenantSchema.Identity)" 
        ContinueOnError="false" />
</Target>
```

### Microservices with Shared Schema

```xml
<!-- In each microservice project -->
<PropertyGroup>
  <SharedSchemaAssembly>../Shared/bin/$(Configuration)/net8.0/Shared.dll</SharedSchemaAssembly>
</PropertyGroup>

<Target Name="BowtieSharedSchema" 
        Condition="Exists('$(SharedSchemaAssembly)')">
  
  <!-- Generate shared schema -->
  <Exec Command="dotnet bowtie generate --assembly &quot;$(SharedSchemaAssembly)&quot; --provider $(BowtieProvider) --output shared_schema.sql" />
  
  <!-- Generate service-specific schema -->
  <Exec Command="dotnet bowtie generate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider $(BowtieProvider) --output service_schema.sql" />
</Target>
```

### Blue-Green Deployments

```xml
<Target Name="BowtieBlueGreenDeploy">
  <PropertyGroup>
    <BlueConnectionString>$(BLUE_CONNECTION_STRING)</BlueConnectionString>
    <GreenConnectionString>$(GREEN_CONNECTION_STRING)</GreenConnectionString>
  </PropertyGroup>

  <!-- Validate against both environments -->
  <Exec Command="dotnet bowtie validate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider $(BowtieProvider)" />
  
  <!-- Deploy to blue environment first -->
  <Exec Command="dotnet bowtie sync --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --connection-string &quot;$(BlueConnectionString)&quot; --provider $(BowtieProvider)" />
  
  <!-- If successful, deploy to green environment -->
  <Exec Command="dotnet bowtie sync --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --connection-string &quot;$(GreenConnectionString)&quot; --provider $(BowtieProvider)" 
        Condition="'$(DeployGreen)' == 'true'" />
</Target>
```

## Configuration Examples

### Per-Environment Configuration

**Development (Debug)**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <BowtieProvider>SQLite</BowtieProvider>
  <BowtieConnectionString>Data Source=./dev.db</BowtieConnectionString>
  <BowtieAutoSync>true</BowtieAutoSync>
  <BowtieVerbose>true</BowtieVerbose>
</PropertyGroup>
```

**Staging (Release)**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release' AND '$(Environment)' == 'Staging'">
  <BowtieProvider>SqlServer</BowtieProvider>
  <BowtieConnectionString>$(STAGING_CONNECTION_STRING)</BowtieConnectionString>
  <BowtieAutoSync>false</BowtieAutoSync>
  <BowtieGenerateScripts>true</BowtieGenerateScripts>
</PropertyGroup>
```

**Production (Release)**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release' AND '$(Environment)' == 'Production'">
  <BowtieProvider>SqlServer</BowtieProvider>
  <BowtieAutoSync>false</BowtieAutoSync>
  <BowtieGenerateScripts>true</BowtieGenerateScripts>
  <!-- Never auto-sync in production -->
</PropertyGroup>
```

### Provider-Specific Settings

```xml
<!-- SQL Server with schema -->
<PropertyGroup Condition="'$(BowtieProvider)' == 'SqlServer'">
  <BowtieDefaultSchema>dbo</BowtieDefaultSchema>
</PropertyGroup>

<!-- PostgreSQL with schema -->
<PropertyGroup Condition="'$(BowtieProvider)' == 'PostgreSQL'">
  <BowtieDefaultSchema>public</BowtieDefaultSchema>
</PropertyGroup>

<!-- SQLite - no schema support -->
<PropertyGroup Condition="'$(BowtieProvider)' == 'SQLite'">
  <BowtieDefaultSchema></BowtieDefaultSchema>
</PropertyGroup>
```

## Command Line Usage

### Running MSBuild Targets

```bash
# Build with Bowtie script generation
dotnet build

# Build with auto-sync enabled
dotnet build -p:BowtieAutoSync=true

# Build for specific provider
dotnet build -p:BowtieProvider=PostgreSQL

# Build with verbose output
dotnet build -p:BowtieVerbose=true

# Generate all provider scripts
dotnet build -p:BowtieGenerateAllProviders=true

# Run custom migration target
dotnet build -t:BowtieCustomMigration -p:BowtieRunCustom=true

# Deploy to specific environment
dotnet build -t:DeployDev
dotnet build -t:DeployStaging -p:Environment=Staging
```

### Environment Variables

Set these environment variables for sensitive data:

```bash
# Windows
set STAGING_CONNECTION_STRING="Server=staging;Database=MyApp;Integrated Security=true"
set PRODUCTION_CONNECTION_STRING="Server=prod;Database=MyApp;Integrated Security=true"

# Linux/Mac
export STAGING_CONNECTION_STRING="Host=staging;Database=myapp;Username=myapp;Password=***"
export PRODUCTION_CONNECTION_STRING="Host=prod;Database=myapp;Username=myapp;Password=***"
```

## Best Practices

### 1. Version Control Integration

```xml
<!-- Always generate scripts during CI/CD -->
<Target Name="BowtieCI" Condition="'$(CI)' == 'true'" AfterTargets="Build">
  <Exec Command="dotnet bowtie generate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider $(BowtieProvider) --output $(Build.ArtifactStagingDirectory)/schema.sql" />
</Target>
```

### 2. Rollback Strategy

```xml
<Target Name="BowtieGenerateRollback">
  <!-- Generate current schema snapshot -->
  <Exec Command="dotnet bowtie export --connection-string &quot;$(BowtieConnectionString)&quot; --provider $(BowtieProvider) --output current_schema.sql" />
  
  <!-- Generate new schema -->
  <Exec Command="dotnet bowtie generate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider $(BowtieProvider) --output new_schema.sql" />
</Target>
```

### 3. Database Backup Integration

```xml
<Target Name="BowtieWithBackup" BeforeTargets="BowtieSyncDatabase">
  <!-- Backup database before migration -->
  <Exec Command="sqlcmd -S $(DatabaseServer) -d $(DatabaseName) -Q &quot;BACKUP DATABASE [$(DatabaseName)] TO DISK = '$(BackupPath)/$(DatabaseName)_$(MigrationTimestamp).bak'&quot;" 
        Condition="'$(BowtieProvider)' == 'SqlServer'" />
</Target>
```

## Troubleshooting MSBuild Integration

### Common Issues

1. **Assembly Not Found**
   ```
   Error: Could not load file or assembly 'MyApp.dll'
   ```
   **Solution**: Ensure the target executes after build
   ```xml
   <Target Name="BowtieTarget" AfterTargets="Build" Condition="Exists('$(OutputPath)$(AssemblyName).dll')">
   ```

2. **Connection String Escaping**
   ```
   Error: Invalid connection string
   ```
   **Solution**: Properly escape quotes
   ```xml
   <Exec Command="dotnet bowtie sync --connection-string &quot;$(BowtieConnectionString)&quot;" />
   ```

3. **Tool Not Found**
   ```
   Error: dotnet bowtie command not found
   ```
   **Solution**: Install tool in project
   ```xml
   <ItemGroup>
     <PackageReference Include="Bowtie.CLI" Version="0.1.0" PrivateAssets="all" />
   </ItemGroup>
   ```

### Debug Tips

1. **Enable Verbose Logging**
   ```bash
   dotnet build -p:BowtieVerbose=true
   ```

2. **Run Targets Individually**
   ```bash
   dotnet build -t:BowtieValidateModels
   dotnet build -t:BowtieGenerateScripts
   ```

3. **Check Generated Files**
   ```bash
   # Generated scripts are in:
   ls ./bin/Debug/net8.0/migrations/
   ```

## Integration with Popular Tools

### Entity Framework Migration Comparison

Unlike Entity Framework migrations, Bowtie:
- ✅ Works with any ORM (not just EF)
- ✅ Generates complete schema from models
- ✅ Supports multiple database providers
- ❌ No migration history tracking
- ❌ No automatic rollback generation

### FluentMigrator Integration

Use alongside FluentMigrator for complex scenarios:

```xml
<Target Name="BowtieAndFluentMigrator">
  <!-- Generate base schema with Bowtie -->
  <Exec Command="dotnet bowtie generate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider $(BowtieProvider) --output base_schema.sql" />
  
  <!-- Run data migrations with FluentMigrator -->
  <Exec Command="dotnet fm migrate -p $(BowtieProvider) -c &quot;$(BowtieConnectionString)&quot;" />
</Target>
```

### Database Project Integration

For Visual Studio Database Projects:

```xml
<Target Name="BowtieToDbProject">
  <PropertyGroup>
    <DbProjectPath>../Database/Database.sqlproj</DbProjectPath>
    <DbProjectSchemaPath>../Database/Schema</DbProjectSchemaPath>
  </PropertyGroup>

  <!-- Generate scripts for database project -->
  <Exec Command="dotnet bowtie generate --assembly &quot;$(OutputPath)$(AssemblyName).dll&quot; --provider SqlServer --output &quot;$(DbProjectSchemaPath)/GeneratedSchema.sql&quot;" />
  
  <!-- Build database project -->
  <MSBuild Projects="$(DbProjectPath)" Targets="Build" Condition="Exists('$(DbProjectPath)')" />
</Target>
```

## Example Project Structure

```
MySolution/
├── Directory.Build.props           # Solution-wide Bowtie config
├── src/
│   ├── MyApp.Api/
│   │   ├── MyApp.Api.csproj       # API project with Bowtie integration
│   │   ├── Models/
│   │   │   ├── User.cs
│   │   │   ├── Product.cs
│   │   │   └── Order.cs
│   │   └── Program.cs
│   └── MyApp.Core/
│       ├── MyApp.Core.csproj      # Core models library
│       └── Models/
├── migrations/                     # Generated migration scripts
│   ├── schema_sqlserver.sql
│   ├── schema_postgresql.sql
│   └── schema_sqlite.sql
└── scripts/
    ├── deploy-dev.ps1
    ├── deploy-staging.ps1
    └── validate-production.ps1
```

This structure allows for:
- Centralized configuration
- Environment-specific overrides
- Automated script generation
- Manual deployment control