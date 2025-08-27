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
        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoSqlServer(connectionString, null, lifetime);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            string connectionString,
            Action<SqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            return services.AddTuxedo(_ =>
            {
                var connection = new SqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoSqlServer(connectionStringFactory, null, lifetime);
        }

        public static IServiceCollection AddTuxedoSqlServer(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<SqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionStringFactory == null)
                throw new ArgumentNullException(nameof(connectionStringFactory));

            return services.AddTuxedo(provider =>
            {
                var connectionString = connectionStringFactory(provider);
                var connection = new SqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoPostgres(connectionString, null, lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            string connectionString,
            Action<NpgsqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            return services.AddTuxedo(_ =>
            {
                var connection = new NpgsqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoPostgres(connectionStringFactory, null, lifetime);
        }

        public static IServiceCollection AddTuxedoPostgres(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<NpgsqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionStringFactory == null)
                throw new ArgumentNullException(nameof(connectionStringFactory));

            return services.AddTuxedo(provider =>
            {
                var connectionString = connectionStringFactory(provider);
                var connection = new NpgsqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoMySql(connectionString, null, lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            string connectionString,
            Action<MySqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            return services.AddTuxedo(_ =>
            {
                var connection = new MySqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddTuxedoMySql(connectionStringFactory, null, lifetime);
        }

        public static IServiceCollection AddTuxedoMySql(
            this IServiceCollection services,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<MySqlConnection>? configureConnection,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionStringFactory == null)
                throw new ArgumentNullException(nameof(connectionStringFactory));

            return services.AddTuxedo(provider =>
            {
                var connectionString = connectionStringFactory(provider);
                var connection = new MySqlConnection(connectionString);
                configureConnection?.Invoke(connection);
                return connection;
            }, lifetime);
        }
    }
}