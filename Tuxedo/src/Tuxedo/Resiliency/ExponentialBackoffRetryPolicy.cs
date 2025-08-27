using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Tuxedo.Resiliency
{
    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly ILogger<ExponentialBackoffRetryPolicy>? _logger;

        public ExponentialBackoffRetryPolicy(
            int maxRetryAttempts = 3,
            TimeSpan? baseDelay = null,
            ILogger<ExponentialBackoffRetryPolicy>? logger = null)
        {
            _maxRetryAttempts = maxRetryAttempts;
            _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    attempt++;
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < _maxRetryAttempts && IsTransient(ex))
                {
                    var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    _logger?.LogWarning(ex, "Transient error on attempt {Attempt} of {MaxAttempts}. Retrying in {Delay}ms", 
                        attempt, _maxRetryAttempts, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }

        public T Execute<T>(Func<T> operation)
        {
            return ExecuteAsync(() => Task.FromResult(operation())).GetAwaiter().GetResult();
        }

        public void Execute(Action operation)
        {
            ExecuteAsync(() =>
            {
                operation();
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is DbException dbEx)
            {
                // Common transient error codes
                return dbEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                       dbEx.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                       dbEx.Message.Contains("transport", StringComparison.OrdinalIgnoreCase) ||
                       dbEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
            }

            return ex is TimeoutException || 
                   ex is OperationCanceledException;
        }
    }
}