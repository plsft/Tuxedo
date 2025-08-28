using System;
using System.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tuxedo.BulkOperations;
using Tuxedo.Caching;
using Tuxedo.Diagnostics;
using Tuxedo.Patterns;
using Tuxedo.Resiliency;

namespace Tuxedo.DependencyInjection
{
    public static class TuxedoEnterpriseExtensions
    {
        /// <summary>
        /// Adds Tuxedo resiliency features with Polly integration
        /// </summary>
        public static IServiceCollection AddTuxedoResiliency(
            this IServiceCollection services, 
            Action<ResiliencyOptions>? configureOptions = null)
        {
            var options = new ResiliencyOptions();
            configureOptions?.Invoke(options);
            
            services.AddSingleton(options);
            services.AddSingleton<IResiliencyProvider, PollyResiliencyProvider>();
            
            // Add decorator for IDbConnection
            services.Decorate<IDbConnection>((connection, provider) =>
            {
                var resiliencyProvider = provider.GetRequiredService<IResiliencyProvider>();
                return resiliencyProvider.WrapConnection(connection);
            });
            
            return services;
        }

        /// <summary>
        /// Adds Tuxedo resiliency features with configuration
        /// </summary>
        public static IServiceCollection AddTuxedoResiliency(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoResiliency")
        {
            services.Configure<ResiliencyOptions>(configuration.GetSection(sectionName));
            
            services.AddSingleton<IResiliencyProvider>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResiliencyOptions>>().Value;
                var logger = provider.GetService<ILogger<PollyResiliencyProvider>>();
                return new PollyResiliencyProvider(options, logger);
            });
            
            // Add decorator for IDbConnection
            services.Decorate<IDbConnection>((connection, provider) =>
            {
                var resiliencyProvider = provider.GetRequiredService<IResiliencyProvider>();
                return resiliencyProvider.WrapConnection(connection);
            });
            
            return services;
        }

        /// <summary>
        /// Adds Tuxedo caching support
        /// </summary>
        public static IServiceCollection AddTuxedoCaching(
            this IServiceCollection services,
            Action<CachingOptions>? configureOptions = null)
        {
            var options = new CachingOptions();
            configureOptions?.Invoke(options);
            
            services.AddSingleton(options);
            
            // Add memory cache if not already registered
            services.TryAddSingleton<IMemoryCache>(provider => 
                new MemoryCache(new MemoryCacheOptions
                {
                    SizeLimit = options.MaxCacheSize,
                    ExpirationScanFrequency = TimeSpan.FromMinutes(5)
                }));
            
            services.AddSingleton<IQueryCache, MemoryQueryCache>();
            
            return services;
        }

        /// <summary>
        /// Adds Tuxedo caching with configuration
        /// </summary>
        public static IServiceCollection AddTuxedoCaching(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoCaching")
        {
            services.Configure<CachingOptions>(configuration.GetSection(sectionName));
            
            // Add memory cache if not already registered
            services.TryAddSingleton<IMemoryCache>(provider =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CachingOptions>>().Value;
                return new MemoryCache(new MemoryCacheOptions
                {
                    SizeLimit = options.MaxCacheSize,
                    ExpirationScanFrequency = TimeSpan.FromMinutes(5)
                });
            });
            
            services.AddSingleton<IQueryCache, MemoryQueryCache>();
            
            return services;
        }

        /// <summary>
        /// Adds Tuxedo diagnostics support
        /// </summary>
        public static IServiceCollection AddTuxedoDiagnostics(
            this IServiceCollection services,
            Action<DiagnosticsOptions>? configureOptions = null)
        {
            var options = new DiagnosticsOptions();
            configureOptions?.Invoke(options);
            
            services.AddSingleton(options);
            services.AddSingleton<ITuxedoDiagnostics, TuxedoDiagnostics>();
            
            return services;
        }

        /// <summary>
        /// Adds Tuxedo bulk operations support
        /// </summary>
        public static IServiceCollection AddTuxedoBulkOperations(
            this IServiceCollection services,
            TuxedoDialect? dialect = null)
        {
            services.AddScoped<IBulkOperations>(provider =>
            {
                var dialectToUse = dialect;
                if (!dialectToUse.HasValue)
                {
                    // Try to detect from registered connection
                    var connection = provider.GetService<IDbConnection>();
                    if (connection != null)
                    {
                        var typeName = connection.GetType().Name.ToLowerInvariant();
                        if (typeName.Contains("sqlite"))
                            dialectToUse = TuxedoDialect.Sqlite;
                        else if (typeName.Contains("npgsql") || typeName.Contains("postgres"))
                            dialectToUse = TuxedoDialect.Postgres;
                        else if (typeName.Contains("mysql"))
                            dialectToUse = TuxedoDialect.MySql;
                        else
                            dialectToUse = TuxedoDialect.SqlServer;
                    }
                }
                
                return new BulkOperations.BulkOperations(dialectToUse ?? TuxedoDialect.SqlServer);
            });
            
            return services;
        }

        /// <summary>
        /// Adds repository pattern support
        /// </summary>
        public static IServiceCollection AddTuxedoRepositories(
            this IServiceCollection services,
            TuxedoDialect? dialect = null)
        {
            services.AddScoped(typeof(IRepository<>), typeof(TuxedoRepository<>));
            
            // Register dialect for repositories via options
            if (dialect.HasValue)
            {
                services.AddSingleton(new TuxedoOptions { Dialect = dialect.Value });
            }
            
            return services;
        }

        /// <summary>
        /// Adds Unit of Work pattern support
        /// </summary>
        public static IServiceCollection AddTuxedoUnitOfWork(
            this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            return services;
        }

        /// <summary>
        /// Adds all Tuxedo enterprise features
        /// </summary>
        public static IServiceCollection AddTuxedoEnterprise(
            this IServiceCollection services,
            Action<TuxedoEnterpriseOptions>? configureOptions = null)
        {
            var options = new TuxedoEnterpriseOptions();
            configureOptions?.Invoke(options);
            
            if (options.EnableResiliency)
            {
                services.AddTuxedoResiliency(opt =>
                {
                    opt.MaxRetryAttempts = options.MaxRetryAttempts;
                    opt.BaseDelay = options.BaseDelay;
                    opt.EnableCircuitBreaker = options.EnableCircuitBreaker;
                    opt.CircuitBreakerThreshold = options.CircuitBreakerThreshold;
                    opt.CircuitBreakerTimeout = options.CircuitBreakerTimeout;
                });
            }
            
            if (options.EnableCaching)
            {
                services.AddTuxedoCaching(opt =>
                {
                    opt.DefaultCacheDuration = options.DefaultCacheDuration;
                    opt.MaxCacheSize = options.MaxCacheSize;
                });
            }
            
            if (options.EnableDiagnostics)
            {
                services.AddTuxedoDiagnostics();
            }
            
            if (options.EnableBulkOperations)
            {
                services.AddTuxedoBulkOperations(options.Dialect);
            }
            
            if (options.EnableRepositoryPattern)
            {
                services.AddTuxedoRepositories(options.Dialect);
            }
            
            if (options.EnableUnitOfWork)
            {
                services.AddTuxedoUnitOfWork();
            }
            
            return services;
        }

        /// <summary>
        /// Adds all Tuxedo enterprise features with configuration
        /// </summary>
        public static IServiceCollection AddTuxedoEnterprise(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "TuxedoEnterprise")
        {
            var section = configuration.GetSection(sectionName);
            services.Configure<TuxedoEnterpriseOptions>(section);
            
            var options = section.Get<TuxedoEnterpriseOptions>() ?? new TuxedoEnterpriseOptions();
            
            return services.AddTuxedoEnterprise(opt =>
            {
                opt.EnableResiliency = options.EnableResiliency;
                opt.EnableCaching = options.EnableCaching;
                opt.EnableDiagnostics = options.EnableDiagnostics;
                opt.EnableBulkOperations = options.EnableBulkOperations;
                opt.EnableRepositoryPattern = options.EnableRepositoryPattern;
                opt.EnableUnitOfWork = options.EnableUnitOfWork;
                opt.Dialect = options.Dialect;
                opt.MaxRetryAttempts = options.MaxRetryAttempts;
                opt.BaseDelay = options.BaseDelay;
                opt.EnableCircuitBreaker = options.EnableCircuitBreaker;
                opt.CircuitBreakerThreshold = options.CircuitBreakerThreshold;
                opt.CircuitBreakerTimeout = options.CircuitBreakerTimeout;
                opt.DefaultCacheDuration = options.DefaultCacheDuration;
                opt.MaxCacheSize = options.MaxCacheSize;
            });
        }
    }

    public class TuxedoEnterpriseOptions
    {
        public bool EnableResiliency { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public bool EnableDiagnostics { get; set; } = true;
        public bool EnableBulkOperations { get; set; } = true;
        public bool EnableRepositoryPattern { get; set; } = true;
        public bool EnableUnitOfWork { get; set; } = true;
        public TuxedoDialect? Dialect { get; set; }
        
        // Resiliency options
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
        // Caching options
        public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
        public long MaxCacheSize { get; set; } = 1000;
    }

    public class CachingOptions
    {
        public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
        public long MaxCacheSize { get; set; } = 1000;
        public bool EnableSlidingExpiration { get; set; } = true;
    }

    public class DiagnosticsOptions
    {
        public bool EnableQueryLogging { get; set; } = true;
        public bool EnablePerformanceMetrics { get; set; } = true;
        public bool EnableErrorLogging { get; set; } = true;
        public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Helper extension to decorate services
    /// </summary>
    internal static class ServiceCollectionDecoratorExtensions
    {
        public static IServiceCollection Decorate<TService>(
            this IServiceCollection services,
            Func<TService, IServiceProvider, TService> decorator)
            where TService : class
        {
            var existingService = services.FirstOrDefault(s => s.ServiceType == typeof(TService));
            if (existingService != null)
            {
                var index = services.IndexOf(existingService);
                services[index] = ServiceDescriptor.Describe(
                    typeof(TService),
                    provider =>
                    {
                        var instance = existingService.ImplementationFactory != null
                            ? (TService)existingService.ImplementationFactory(provider)
                            : (TService)ActivatorUtilities.CreateInstance(provider, existingService.ImplementationType!);
                        return decorator(instance, provider);
                    },
                    existingService.Lifetime);
            }
            
            return services;
        }
    }
}