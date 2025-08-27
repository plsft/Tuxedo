using System;
using System.Data;
using Microsoft.Extensions.DependencyInjection;

#if NET6_0
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Npgsql;
using MySqlConnector;

namespace Tuxedo.DependencyInjection
{
    public static class TuxedoProviderExtensions
    {
        /// <summary>
        /// Adds Tuxedo with SQL Server support using the new options pattern.
        /// </summary>
        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            Action<SqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.SqlServer;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = _ =>
                {
                    var connection = new SqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        /// <summary>
        /// Adds Tuxedo with SQL Server support using a connection string factory.
        /// </summary>
        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<SqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.SqlServer;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = sp =>
                {
                    var connectionString = connectionStringFactory(sp);
                    var connection = new SqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        /// <summary>
        /// Adds Tuxedo with PostgreSQL support using the new options pattern.
        /// </summary>
        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            Action<NpgsqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.Postgres;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = _ =>
                {
                    var connection = new NpgsqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        /// <summary>
        /// Adds Tuxedo with PostgreSQL support using a connection string factory.
        /// </summary>
        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<NpgsqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.Postgres;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = sp =>
                {
                    var connectionString = connectionStringFactory(sp);
                    var connection = new NpgsqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        /// <summary>
        /// Adds Tuxedo with MySQL support using the new options pattern.
        /// </summary>
        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            Action<MySqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.MySql;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = _ =>
                {
                    var connection = new MySqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        /// <summary>
        /// Adds Tuxedo with MySQL support using a connection string factory.
        /// </summary>
        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<MySqlConnection>? configureConnection = null,
            bool openOnResolve = true,
            int? commandTimeoutSeconds = null)
        {
            return services.AddTuxedo(opts =>
            {
                opts.Dialect = TuxedoDialect.MySql;
                opts.OpenOnResolve = openOnResolve;
                opts.DefaultCommandTimeoutSeconds = commandTimeoutSeconds;
                opts.ConnectionFactory = sp =>
                {
                    var connectionString = connectionStringFactory(sp);
                    var connection = new MySqlConnection(connectionString);
                    configureConnection?.Invoke(connection);
                    return connection;
                };
            });
        }

        #region Legacy overloads for backwards compatibility

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoSqlServer(connectionString, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            Action<SqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoSqlServer(connectionString, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoSqlServer(connectionStringFactory, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<SqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoSqlServer(connectionStringFactory, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoPostgres(connectionString, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            Action<NpgsqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoPostgres(connectionString, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoPostgres(connectionStringFactory, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<NpgsqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoPostgres(connectionStringFactory, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoMySql(connectionString, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            Action<MySqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoMySql(connectionString, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoMySql(connectionStringFactory, null, lifetime == ServiceLifetime.Scoped);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<MySqlConnection>? configureConnection,
            ServiceLifetime lifetime)
        {
            return services.AddTuxedoMySql(connectionStringFactory, configureConnection, lifetime == ServiceLifetime.Scoped);
        }

        #endregion
    }
}