using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#if NET6_0
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Npgsql;
using MySqlConnector;

namespace Tuxedo.DependencyInjection
{
    public static class TuxedoOptionsExtensions
    {
        public static IServiceCollection AddTuxedoSqlServerWithOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoSqlServer",
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<TuxedoSqlServerOptions>(configuration.GetSection(sectionName));

            return services.AddTuxedoWithOptions<IOptions<TuxedoSqlServerOptions>>(
                (provider, options) =>
                {
                    var config = options.Value;
                    if (string.IsNullOrWhiteSpace(config.ConnectionString))
                        throw new InvalidOperationException($"Connection string not configured in section '{sectionName}'");

                    var builder = new SqlConnectionStringBuilder(config.ConnectionString);
                    
                    if (config.MultipleActiveResultSets)
                        builder.MultipleActiveResultSets = true;
                    
                    if (config.TrustServerCertificate)
                        builder.TrustServerCertificate = true;
                    
                    if (config.ConnectTimeout.HasValue)
                        builder.ConnectTimeout = config.ConnectTimeout.Value;

                    return new SqlConnection(builder.ConnectionString);
                },
                lifetime);
        }

        public static IServiceCollection AddTuxedoPostgresWithOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoPostgres",
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<TuxedoPostgresOptions>(configuration.GetSection(sectionName));

            return services.AddTuxedoWithOptions<IOptions<TuxedoPostgresOptions>>(
                (provider, options) =>
                {
                    var config = options.Value;
                    if (string.IsNullOrWhiteSpace(config.ConnectionString))
                        throw new InvalidOperationException($"Connection string not configured in section '{sectionName}'");

                    var builder = new NpgsqlConnectionStringBuilder(config.ConnectionString)
                    {
                        Pooling = config.Pooling
                    };
                    
                    if (config.MinPoolSize.HasValue)
                        builder.MinPoolSize = config.MinPoolSize.Value;
                    
                    if (config.MaxPoolSize.HasValue)
                        builder.MaxPoolSize = config.MaxPoolSize.Value;
                    
                    if (config.CommandTimeout.HasValue)
                        builder.CommandTimeout = config.CommandTimeout.Value;

                    var connection = new NpgsqlConnection(builder.ConnectionString);
                    return connection;
                },
                lifetime);
        }

        public static IServiceCollection AddTuxedoMySqlWithOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoMySql",
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<TuxedoMySqlOptions>(configuration.GetSection(sectionName));

            return services.AddTuxedoWithOptions<IOptions<TuxedoMySqlOptions>>(
                (provider, options) =>
                {
                    var config = options.Value;
                    if (string.IsNullOrWhiteSpace(config.ConnectionString))
                        throw new InvalidOperationException($"Connection string not configured in section '{sectionName}'");

                    var builder = new MySqlConnectionStringBuilder(config.ConnectionString)
                    {
                        AllowUserVariables = config.AllowUserVariables,
                        UseCompression = config.UseCompression,
                        ConvertZeroDateTime = config.ConvertZeroDateTime
                    };
                    
                    if (config.ConnectionLifeTime.HasValue)
                        builder.ConnectionLifeTime = config.ConnectionLifeTime.Value;
                    
                    if (config.CommandTimeout.HasValue)
                        builder.DefaultCommandTimeout = (uint)config.CommandTimeout.Value;

                    return new MySqlConnection(builder.ConnectionString);
                },
                lifetime);
        }
    }
}