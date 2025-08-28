using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Tuxedo.Resiliency
{
    public interface IResiliencyProvider : IRetryPolicy
    {
        IDbConnection WrapConnection(IDbConnection connection);
    }

    public class PollyResiliencyProvider : IResiliencyProvider
    {
        private readonly ResiliencyOptions _options;
        private readonly ILogger<PollyResiliencyProvider>? _logger;
        private readonly IAsyncPolicy _asyncPolicy;
        private readonly ISyncPolicy _syncPolicy;

        public PollyResiliencyProvider(ResiliencyOptions options, ILogger<PollyResiliencyProvider>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            
            _asyncPolicy = BuildAsyncPolicy();
            _syncPolicy = BuildSyncPolicy();
        }

        public IDbConnection WrapConnection(IDbConnection connection)
        {
            return new ResilientDbConnection(connection, this);
        }

        public T Execute<T>(Func<T> operation)
        {
            return _syncPolicy.Execute(operation);
        }

        // IRetryPolicy implementation
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return await _asyncPolicy.ExecuteAsync(async (ct) => await operation(), cancellationToken);
        }

        public void Execute(Action operation)
        {
            _syncPolicy.Execute(operation);
        }

        public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await _asyncPolicy.ExecuteAsync(async (ct) => await operation(), cancellationToken);
        }

        private IAsyncPolicy BuildAsyncPolicy()
        {
            var policies = new List<IAsyncPolicy>();

            // Add retry policy
            if (_options.MaxRetryAttempts > 0)
            {
                var retryPolicy = Policy
                    .Handle<DbException>(IsTransientException)
                    .Or<TimeoutException>()
                    .WaitAndRetryAsync(
                        _options.MaxRetryAttempts,
                        retryAttempt => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            _logger?.LogWarning(
                                "Retry {RetryCount} after {Delay}ms",
                                retryCount,
                                timespan.TotalMilliseconds);
                        });
                
                policies.Add(retryPolicy);
            }

            // Add circuit breaker policy
            if (_options.EnableCircuitBreaker)
            {
                var circuitBreakerPolicy = Policy
                    .Handle<DbException>(IsTransientException)
                    .Or<TimeoutException>()
                    .CircuitBreakerAsync(
                        _options.CircuitBreakerThreshold,
                        _options.CircuitBreakerTimeout,
                        onBreak: (result, duration) =>
                        {
                            _logger?.LogError(
                                "Circuit breaker opened for {Duration}ms",
                                duration.TotalMilliseconds);
                        },
                        onReset: () =>
                        {
                            _logger?.LogInformation("Circuit breaker reset");
                        },
                        onHalfOpen: () =>
                        {
                            _logger?.LogInformation("Circuit breaker is half-open");
                        });
                
                policies.Add(circuitBreakerPolicy);
            }

            // Add default timeout policy if needed
            // Note: OperationTimeout could be added to ResiliencyOptions if needed

            // Combine policies
            return policies.Count > 0 
                ? Policy.WrapAsync(policies.ToArray()) 
                : Policy.NoOpAsync();
        }

        private ISyncPolicy BuildSyncPolicy()
        {
            var policies = new List<ISyncPolicy>();

            // Add retry policy
            if (_options.MaxRetryAttempts > 0)
            {
                var retryPolicy = Policy
                    .Handle<DbException>(IsTransientException)
                    .Or<TimeoutException>()
                    .WaitAndRetry(
                        _options.MaxRetryAttempts,
                        retryAttempt => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            _logger?.LogWarning(
                                "Retry {RetryCount} after {Delay}ms due to: {Exception}",
                                retryCount,
                                timespan.TotalMilliseconds,
                                outcome.Message);
                        });
                
                policies.Add(retryPolicy);
            }

            // Add circuit breaker policy
            if (_options.EnableCircuitBreaker)
            {
                var circuitBreakerPolicy = Policy
                    .Handle<DbException>(IsTransientException)
                    .Or<TimeoutException>()
                    .CircuitBreaker(
                        _options.CircuitBreakerThreshold,
                        _options.CircuitBreakerTimeout,
                        onBreak: (result, duration) =>
                        {
                            _logger?.LogError(
                                "Circuit breaker opened for {Duration}ms due to: {Exception}",
                                duration.TotalMilliseconds,
                                result.Message);
                        },
                        onReset: () =>
                        {
                            _logger?.LogInformation("Circuit breaker reset");
                        },
                        onHalfOpen: () =>
                        {
                            _logger?.LogInformation("Circuit breaker is half-open");
                        });
                
                policies.Add(circuitBreakerPolicy);
            }

            // Add default timeout policy if needed
            // Note: OperationTimeout could be added to ResiliencyOptions if needed

            // Combine policies
            return policies.Count > 0 
                ? Policy.Wrap(policies.ToArray()) 
                : Policy.NoOp();
        }

        private bool IsTransientException(DbException exception)
        {
            // Check if the exception is transient based on the error code
            // This is database-specific; you might need to customize this
            
            if (exception == null)
                return false;

            // SQL Server transient error codes
            var sqlServerTransientErrors = new HashSet<int>
            {
                49918, 49919, 49920, 4060, 40501, 40613,
                49918, 49919, 49920, 11001, 10060, 10061,
                10053, 10054, 10928, 10929, 40197, 40540,
                40143, 233, 64, -2, 20, 121
            };

            // PostgreSQL transient error codes
            var postgresTransientErrors = new HashSet<string>
            {
                "08000", "08003", "08006", "08001", "08004",
                "57P01", "57P02", "57P03", "58000", "58030",
                "40001", "40P01"
            };

            // MySQL transient error codes
            var mysqlTransientErrors = new HashSet<int>
            {
                1213, 1205, 1040, 1041, 2002, 2003, 2006,
                2013, 1158, 1159, 1160, 1161
            };

            // Check for SQL Server errors
            if (exception.GetType().Name.Contains("SqlException"))
            {
                var errorCodeProperty = exception.GetType().GetProperty("Number");
                if (errorCodeProperty != null)
                {
                    var errorCode = (int)errorCodeProperty.GetValue(exception)!;
                    return sqlServerTransientErrors.Contains(errorCode);
                }
            }

            // Check for PostgreSQL errors
            if (exception.GetType().Name.Contains("NpgsqlException") || 
                exception.GetType().Name.Contains("PostgresException"))
            {
                var sqlStateProperty = exception.GetType().GetProperty("SqlState");
                if (sqlStateProperty != null)
                {
                    var sqlState = sqlStateProperty.GetValue(exception)?.ToString();
                    if (!string.IsNullOrEmpty(sqlState))
                    {
                        return postgresTransientErrors.Contains(sqlState);
                    }
                }
            }

            // Check for MySQL errors
            if (exception.GetType().Name.Contains("MySqlException"))
            {
                var errorCodeProperty = exception.GetType().GetProperty("Number") ?? 
                                      exception.GetType().GetProperty("ErrorCode");
                if (errorCodeProperty != null)
                {
                    var errorCode = Convert.ToInt32(errorCodeProperty.GetValue(exception));
                    return mysqlTransientErrors.Contains(errorCode);
                }
            }

            // Default: treat connection-related exceptions as transient
            var message = exception.Message.ToLowerInvariant();
            return message.Contains("timeout") ||
                   message.Contains("deadlock") ||
                   message.Contains("connection") ||
                   message.Contains("network") ||
                   message.Contains("transport");
        }
    }

}