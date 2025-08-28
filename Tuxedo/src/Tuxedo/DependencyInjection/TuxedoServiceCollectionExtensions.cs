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

    }
}