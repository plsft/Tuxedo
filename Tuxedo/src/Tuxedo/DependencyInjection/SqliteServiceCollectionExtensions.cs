using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Tuxedo.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring SQLite with Tuxedo
    /// </summary>
    public static class SqliteServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Tuxedo with SQLite support using a connection string
        /// </summary>
        public static IServiceCollection AddTuxedoSqlite(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoSqlite(
                connectionString,
                configure: null,
                lifetime);
        }

        /// <summary>
        /// Adds Tuxedo with SQLite support using a connection string and configuration
        /// </summary>
        public static IServiceCollection AddTuxedoSqlite(
            this IServiceCollection services,
            string connectionString,
            Action<SqliteConnection>? configure,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
            }

            services.AddTuxedo(options =>
            {
                options.ConnectionFactory = _ =>
                {
                    var connection = new SqliteConnection(connectionString);
                    configure?.Invoke(connection);
                    return connection;
                };
                options.Dialect = TuxedoDialect.Sqlite;
                options.OpenOnResolve = true;
            });

            // Register SQLite-specific connection
            services.TryAdd(new ServiceDescriptor(
                typeof(SqliteConnection),
                provider => (SqliteConnection)provider.GetRequiredService<IDbConnection>(),
                lifetime));

            return services;
        }

        /// <summary>
        /// Adds Tuxedo with SQLite support using configuration
        /// </summary>
        public static IServiceCollection AddTuxedoSqliteWithOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoSqlite",
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            services.Configure<SqliteOptions>(configuration.GetSection(sectionName));

            services.AddTuxedo(options =>
            {
                var sqliteOptions = configuration.GetSection(sectionName).Get<SqliteOptions>() 
                    ?? new SqliteOptions();
                
                options.ConnectionFactory = _ =>
                {
                    var builder = new SqliteConnectionStringBuilder(sqliteOptions.ConnectionString)
                    {
                        Mode = sqliteOptions.Mode,
                        Cache = sqliteOptions.Cache,
                        DefaultTimeout = sqliteOptions.DefaultTimeout,
                        Pooling = sqliteOptions.Pooling,
                        ForeignKeys = sqliteOptions.ForeignKeys,
                        RecursiveTriggers = sqliteOptions.RecursiveTriggers
                    };
                    
                    return new SqliteConnection(builder.ConnectionString);
                };
                options.Dialect = TuxedoDialect.Sqlite;
                options.OpenOnResolve = sqliteOptions.OpenOnResolve;
                options.DefaultCommandTimeoutSeconds = sqliteOptions.DefaultTimeout;
            });

            // Register SQLite-specific connection
            services.TryAdd(new ServiceDescriptor(
                typeof(SqliteConnection),
                provider => (SqliteConnection)provider.GetRequiredService<IDbConnection>(),
                lifetime));

            return services;
        }

        /// <summary>
        /// Adds Tuxedo with in-memory SQLite database for testing
        /// </summary>
        public static IServiceCollection AddTuxedoSqliteInMemory(
            this IServiceCollection services,
            string? databaseName = null,
            ServiceLifetime lifetime = ServiceLifetime.Singleton) // Singleton for in-memory to maintain state
        {
            var connectionString = string.IsNullOrEmpty(databaseName) 
                ? "Data Source=:memory:" 
                : $"Data Source={databaseName};Mode=Memory;Cache=Shared";

            return services.AddTuxedoSqlite(
                connectionString,
                connection =>
                {
                    // Keep the connection alive for in-memory databases
                    if (lifetime == ServiceLifetime.Singleton)
                    {
                        connection.Open();
                    }
                },
                lifetime);
        }

        /// <summary>
        /// Adds Tuxedo with file-based SQLite database
        /// </summary>
        public static IServiceCollection AddTuxedoSqliteFile(
            this IServiceCollection services,
            string databasePath,
            bool createIfNotExists = true,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path cannot be null or whitespace.", nameof(databasePath));
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = createIfNotExists ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true,
                Pooling = true
            };

            return services.AddTuxedoSqlite(builder.ConnectionString, lifetime: lifetime);
        }
    }

    /// <summary>
    /// Configuration options for SQLite
    /// </summary>
    public class SqliteOptions
    {
        public string ConnectionString { get; set; } = "Data Source=tuxedo.db";
        public SqliteOpenMode Mode { get; set; } = SqliteOpenMode.ReadWriteCreate;
        public SqliteCacheMode Cache { get; set; } = SqliteCacheMode.Default;
        public int DefaultTimeout { get; set; } = 30;
        public bool Pooling { get; set; } = true;
        public bool ForeignKeys { get; set; } = true;
        public bool RecursiveTriggers { get; set; } = false;
        public bool OpenOnResolve { get; set; } = true;
    }
}