using System;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Tuxedo.DependencyInjection
{
    public static class TuxedoServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a scoped IDbConnection using provided options. Typical scope is one per web request.
        /// </summary>
        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            Action<TuxedoOptions> configure)
        {
            services.Configure(configure);

            services.TryAddScoped<IDbConnection>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                var conn = opts.ConnectionFactory(sp);
                if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                    conn.Open();
                return conn;
            });

            // Expose options downstream as needed
            services.TryAddScoped<ITuxedoConnectionFactory>(sp => 
                new TuxedoConnectionFactory(() => sp.GetRequiredService<IDbConnection>()));

            // Register health check
            services.AddHealthChecks()
                .AddTypeActivatedCheck<TuxedoHealthCheck>("tuxedo_db");

            return services;
        }

        // Legacy overloads for backwards compatibility
        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            Func<IServiceProvider, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            services.Configure<TuxedoOptions>(opts =>
            {
                opts.ConnectionFactory = connectionFactory;
                opts.OpenOnResolve = false; // Legacy behavior
            });

            if (lifetime == ServiceLifetime.Scoped)
            {
                services.TryAddScoped<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }
            else if (lifetime == ServiceLifetime.Transient)
            {
                services.TryAddTransient<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }
            else
            {
                services.TryAddSingleton<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }

            services.TryAddScoped<ITuxedoConnectionFactory>(sp =>
                new TuxedoConnectionFactory(() => sp.GetRequiredService<IDbConnection>()));

            return services;
        }

        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            string connectionString,
            Func<string, IDbConnection> connectionBuilder,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            
            if (connectionBuilder == null)
                throw new ArgumentNullException(nameof(connectionBuilder));

            return services.AddTuxedo(
                _ => connectionBuilder(connectionString),
                lifetime);
        }

        public static IServiceCollection AddTuxedoWithOptions<TOptions>(
            this IServiceCollection services,
            Func<IServiceProvider, TOptions, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TOptions : class
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            services.Configure<TuxedoOptions>(opts =>
            {
                opts.ConnectionFactory = sp =>
                {
                    var options = sp.GetRequiredService<TOptions>();
                    return connectionFactory(sp, options);
                };
                opts.OpenOnResolve = false; // Legacy behavior
            });

            if (lifetime == ServiceLifetime.Scoped)
            {
                services.TryAddScoped<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }
            else if (lifetime == ServiceLifetime.Transient)
            {
                services.TryAddTransient<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }
            else
            {
                services.TryAddSingleton<IDbConnection>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<TuxedoOptions>>().Value;
                    var conn = opts.ConnectionFactory(sp);
                    if (opts.OpenOnResolve && conn.State != ConnectionState.Open)
                        conn.Open();
                    return conn;
                });
            }

            services.TryAddScoped<ITuxedoConnectionFactory>(sp =>
                new TuxedoConnectionFactory(() => sp.GetRequiredService<IDbConnection>()));

            return services;
        }
    }
}