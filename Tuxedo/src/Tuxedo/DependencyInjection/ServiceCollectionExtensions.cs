using System;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
#if NET6_0
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Microsoft.Data.Sqlite;
using Npgsql;
using MySqlConnector;

namespace Tuxedo.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {
        // Core registrations
        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            Func<IServiceProvider, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (connectionFactory is null) throw new ArgumentNullException(nameof(connectionFactory));

            // Register ITuxedoConnectionFactory using the provided factory
            services.Add(new ServiceDescriptor(
                typeof(ITuxedoConnectionFactory),
                sp => new TuxedoConnectionFactory(() => connectionFactory(sp)),
                lifetime));

            // Register IDbConnection using the provided factory
            services.Add(new ServiceDescriptor(
                typeof(IDbConnection),
                sp => connectionFactory(sp),
                lifetime));

            return services;
        }

        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            string connectionString,
            Func<string, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            if (connectionFactory is null) throw new ArgumentNullException(nameof(connectionFactory));
            return services.AddTuxedo(_ => connectionFactory(connectionString), lifetime);
        }

        public static IServiceCollection AddTuxedoWithOptions<TOptions>(
            this IServiceCollection services,
            Func<IServiceProvider, TOptions, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TOptions : class
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (connectionFactory is null) throw new ArgumentNullException(nameof(connectionFactory));

            return services.AddTuxedo(sp =>
            {
                var options = sp.GetRequiredService<TOptions>();
                return connectionFactory(sp, options);
            }, lifetime);
        }

        // Provider convenience: SQL Server
        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            return services.AddTuxedo(_ => new SqlConnection(connectionString), lifetime);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (connectionStringFactory is null) throw new ArgumentNullException(nameof(connectionStringFactory));
            return services.AddTuxedo(sp => new SqlConnection(connectionStringFactory(sp)), lifetime);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            Action<SqlConnection> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            if (configure is null) throw new ArgumentNullException(nameof(configure));
            return services.AddTuxedo(_ =>
            {
                var conn = new SqlConnection(connectionString);
                configure(conn);
                return conn;
            });
        }

        // Provider convenience: PostgreSQL
        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            return services.AddTuxedo(_ => new NpgsqlConnection(connectionString), lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (connectionStringFactory is null) throw new ArgumentNullException(nameof(connectionStringFactory));
            return services.AddTuxedo(sp => new NpgsqlConnection(connectionStringFactory(sp)), lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            Action<NpgsqlConnection> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            if (configure is null) throw new ArgumentNullException(nameof(configure));
            return services.AddTuxedo(_ =>
            {
                var conn = new NpgsqlConnection(connectionString);
                configure(conn);
                return conn;
            });
        }

        // Provider convenience: MySQL (MySqlConnector)
        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            return services.AddTuxedo(_ => new MySqlConnection(connectionString), lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (connectionStringFactory is null) throw new ArgumentNullException(nameof(connectionStringFactory));
            return services.AddTuxedo(sp => new MySqlConnection(connectionStringFactory(sp)), lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            Action<MySqlConnection> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required", nameof(connectionString));
            if (configure is null) throw new ArgumentNullException(nameof(configure));
            return services.AddTuxedo(_ =>
            {
                var conn = new MySqlConnection(connectionString);
                configure(conn);
                return conn;
            });
        }

        public static IServiceCollection AddTuxedoSqliteInMemory(
            this IServiceCollection services,
            string name = "TuxedoInMemory")
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(name)) name = "TuxedoInMemory";

            // Use a shared in-memory database; keep the connection open for the app lifetime
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = name,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            var sqliteConnection = new SqliteConnection(connectionString);
            sqliteConnection.Open();

            // Register singletons so the same in-memory database is shared
            services.AddSingleton<ITuxedoConnectionFactory>(sp => new TuxedoConnectionFactory(() => sqliteConnection));
            services.AddSingleton<IDbConnection>(sp => sqliteConnection);
            services.AddSingleton<SqliteConnection>(sp => sqliteConnection);

            return services;
        }

        // Provider convenience: SQLite (file-based)
        public static IServiceCollection AddTuxedoSqlite(
            this IServiceCollection services,
            string filePath,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required", nameof(filePath));

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            };
            return services.AddTuxedo(_ => new SqliteConnection(builder.ConnectionString), lifetime);
        }

        public static IServiceCollection AddTuxedoSqlite(
            this IServiceCollection services,
            string filePath,
            Action<SqliteConnection> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required", nameof(filePath));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            };
            return services.AddTuxedo(_ =>
            {
                var conn = new SqliteConnection(builder.ConnectionString);
                configure(conn);
                return conn;
            });
        }

        // WithOptions helpers
        public static IServiceCollection AddTuxedoSqlServerWithOptions(
            this IServiceCollection services,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            string sectionName = "TuxedoSqlServer")
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var section = configuration.GetSection(sectionName);
            var cs = section["ConnectionString"];
            if (string.IsNullOrWhiteSpace(cs))
            {
                services.AddSingleton<IDbConnection>(_ => throw new InvalidOperationException("TuxedoSqlServer:ConnectionString is required"));
                return services;
            }
            var builder = new SqlConnectionStringBuilder(cs);
            if (bool.TryParse(section["MultipleActiveResultSets"], out var mars)) builder.MultipleActiveResultSets = mars;
            if (bool.TryParse(section["TrustServerCertificate"], out var tsc)) builder.TrustServerCertificate = tsc;
            if (int.TryParse(section["ConnectTimeout"], out var cto)) builder.ConnectTimeout = cto;
            // CommandTimeout is per-command; not part of SqlConnectionStringBuilder; ignored for connection string.

            return services.AddTuxedo(_ => new SqlConnection(builder.ConnectionString));
        }

        public static IServiceCollection AddTuxedoPostgresWithOptions(
            this IServiceCollection services,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            string sectionName = "TuxedoPostgres")
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var section = configuration.GetSection(sectionName);
            var cs = section["ConnectionString"];
            if (string.IsNullOrWhiteSpace(cs))
            {
                services.AddSingleton<IDbConnection>(_ => throw new InvalidOperationException("TuxedoPostgres:ConnectionString is required"));
                return services;
            }
            var builder = new NpgsqlConnectionStringBuilder(cs);
            if (bool.TryParse(section["Pooling"], out var pooling)) builder.Pooling = pooling;
            if (int.TryParse(section["MinPoolSize"], out var minPool)) builder.MinPoolSize = minPool;
            if (int.TryParse(section["MaxPoolSize"], out var maxPool)) builder.MaxPoolSize = maxPool;
            if (int.TryParse(section["CommandTimeout"], out var cmdTimeout)) builder.CommandTimeout = cmdTimeout;

            return services.AddTuxedo(_ => new NpgsqlConnection(builder.ConnectionString));
        }

        public static IServiceCollection AddTuxedoMySqlWithOptions(
            this IServiceCollection services,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            string sectionName = "TuxedoMySql")
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var section = configuration.GetSection(sectionName);
            var cs = section["ConnectionString"];
            if (string.IsNullOrWhiteSpace(cs))
            {
                services.AddSingleton<IDbConnection>(_ => throw new InvalidOperationException("TuxedoMySql:ConnectionString is required"));
                return services;
            }
            var builder = new MySqlConnectionStringBuilder(cs);
            if (bool.TryParse(section["AllowUserVariables"], out var allowUserVars)) builder.AllowUserVariables = allowUserVars;
            if (bool.TryParse(section["UseCompression"], out var useCompression)) builder.UseCompression = useCompression;
            if (bool.TryParse(section["ConvertZeroDateTime"], out var convertZero)) builder.ConvertZeroDateTime = convertZero;
            if (uint.TryParse(section["ConnectionLifeTime"], out var life)) builder.ConnectionLifeTime = life;
            if (uint.TryParse(section["CommandTimeout"], out var defaultCmdTimeout)) builder.DefaultCommandTimeout = defaultCmdTimeout;

            return services.AddTuxedo(_ => new MySqlConnection(builder.ConnectionString));
        }
    }
}
