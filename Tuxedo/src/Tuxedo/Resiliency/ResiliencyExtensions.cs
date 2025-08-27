using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tuxedo.Resiliency
{
    public static class ResiliencyExtensions
    {
        public static IServiceCollection AddTuxedoResiliency(
            this IServiceCollection services,
            Action<ResiliencyOptions>? configure = null)
        {
            var options = new ResiliencyOptions();
            configure?.Invoke(options);

            services.TryAddSingleton<IRetryPolicy>(sp => 
                new ExponentialBackoffRetryPolicy(
                    options.MaxRetryAttempts,
                    options.BaseDelay));

            // Register factory for creating resilient connections
            services.TryAddTransient<Func<IDbConnection, IDbConnection>>(sp =>
            {
                var retryPolicy = sp.GetRequiredService<IRetryPolicy>();
                return connection => new ResilientDbConnection(connection, retryPolicy);
            });
            
            return services;
        }

        /// <summary>
        /// Executes a database operation with retry policy
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            this IDbConnection connection,
            Func<IDbConnection, Task<T>> operation,
            IRetryPolicy? retryPolicy = null,
            CancellationToken cancellationToken = default)
        {
            retryPolicy ??= new ExponentialBackoffRetryPolicy();
            
            return await retryPolicy.ExecuteAsync(
                () => operation(connection),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a database operation with retry policy
        /// </summary>
        public static T ExecuteWithRetry<T>(
            this IDbConnection connection,
            Func<IDbConnection, T> operation,
            IRetryPolicy? retryPolicy = null)
        {
            retryPolicy ??= new ExponentialBackoffRetryPolicy();
            
            return retryPolicy.Execute(() => operation(connection));
        }
    }

    public class ResiliencyOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool EnableCircuitBreaker { get; set; } = false;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}