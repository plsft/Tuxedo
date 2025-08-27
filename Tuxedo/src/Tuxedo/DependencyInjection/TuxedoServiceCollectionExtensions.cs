using System;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tuxedo.DependencyInjection
{
    public static class TuxedoServiceCollectionExtensions
    {
        public static IServiceCollection AddTuxedo(
            this IServiceCollection services,
            Func<IServiceProvider, IDbConnection> connectionFactory,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            services.TryAdd(new ServiceDescriptor(
                typeof(IDbConnection),
                connectionFactory,
                lifetime));

            services.TryAdd(new ServiceDescriptor(
                typeof(ITuxedoConnectionFactory),
                provider => new TuxedoConnectionFactory(() => connectionFactory(provider)),
                lifetime));

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

            services.TryAdd(new ServiceDescriptor(
                typeof(IDbConnection),
                provider =>
                {
                    var options = provider.GetRequiredService<TOptions>();
                    return connectionFactory(provider, options);
                },
                lifetime));

            services.TryAdd(new ServiceDescriptor(
                typeof(ITuxedoConnectionFactory),
                provider => new TuxedoConnectionFactory(() => provider.GetRequiredService<IDbConnection>()),
                lifetime));

            return services;
        }
    }
}