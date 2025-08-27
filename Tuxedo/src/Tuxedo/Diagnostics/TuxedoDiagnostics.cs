using System;
using Microsoft.Extensions.Logging;

namespace Tuxedo.Diagnostics
{
    public class TuxedoDiagnostics : ITuxedoDiagnostics
    {
        private readonly ILogger<TuxedoDiagnostics>? _logger;

        public event EventHandler<QueryExecutedEventArgs>? QueryExecuted;
        public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
        public event EventHandler<TransactionEventArgs>? TransactionStarted;
        public event EventHandler<TransactionEventArgs>? TransactionCommitted;
        public event EventHandler<TransactionEventArgs>? TransactionRolledBack;
        public event EventHandler<ConnectionEventArgs>? ConnectionOpened;
        public event EventHandler<ConnectionEventArgs>? ConnectionClosed;
        public event EventHandler<ErrorEventArgs>? ErrorOccurred;

        public TuxedoDiagnostics(ILogger<TuxedoDiagnostics>? logger = null)
        {
            _logger = logger;
        }

        public void OnQueryExecuted(QueryExecutedEventArgs args)
        {
            _logger?.LogDebug(
                "Query executed in {Duration}ms: {Query}",
                args.Duration.TotalMilliseconds,
                args.Query);

            if (args.Duration.TotalSeconds > 5)
            {
                _logger?.LogWarning(
                    "Slow query detected ({Duration}ms): {Query}",
                    args.Duration.TotalMilliseconds,
                    args.Query);
            }

            QueryExecuted?.Invoke(this, args);
        }

        public void OnCommandExecuted(CommandExecutedEventArgs args)
        {
            _logger?.LogDebug(
                "Command executed in {Duration}ms: {CommandText} (Type: {CommandType}, Rows: {RowsAffected})",
                args.Duration.TotalMilliseconds,
                args.CommandText,
                args.CommandType,
                args.RowsAffected);

            CommandExecuted?.Invoke(this, args);
        }

        public void OnTransactionStarted(TransactionEventArgs args)
        {
            _logger?.LogDebug(
                "Transaction started: {TransactionId} (IsolationLevel: {IsolationLevel})",
                args.TransactionId,
                args.IsolationLevel);

            TransactionStarted?.Invoke(this, args);
        }

        public void OnTransactionCommitted(TransactionEventArgs args)
        {
            _logger?.LogDebug(
                "Transaction committed: {TransactionId} (Duration: {Duration}ms)",
                args.TransactionId,
                args.Duration?.TotalMilliseconds ?? 0);

            TransactionCommitted?.Invoke(this, args);
        }

        public void OnTransactionRolledBack(TransactionEventArgs args)
        {
            _logger?.LogWarning(
                "Transaction rolled back: {TransactionId} (Duration: {Duration}ms)",
                args.TransactionId,
                args.Duration?.TotalMilliseconds ?? 0);

            TransactionRolledBack?.Invoke(this, args);
        }

        public void OnConnectionOpened(ConnectionEventArgs args)
        {
            _logger?.LogDebug(
                "Connection opened: {ConnectionString} (Duration: {Duration}ms)",
                SanitizeConnectionString(args.ConnectionString),
                args.OpenDuration?.TotalMilliseconds ?? 0);

            ConnectionOpened?.Invoke(this, args);
        }

        public void OnConnectionClosed(ConnectionEventArgs args)
        {
            _logger?.LogDebug(
                "Connection closed: {ConnectionString}",
                SanitizeConnectionString(args.ConnectionString));

            ConnectionClosed?.Invoke(this, args);
        }

        public void OnError(ErrorEventArgs args)
        {
            _logger?.LogError(
                args.Exception,
                "Error in Tuxedo operation. Context: {Context}, Query: {Query}",
                args.Context,
                args.Query);

            ErrorOccurred?.Invoke(this, args);
        }

        private static string SanitizeConnectionString(string connectionString)
        {
            // Remove sensitive information from connection string for logging
            var sanitized = connectionString;
            var passwordPatterns = new[] { "password=", "pwd=", "pass=" };
            
            foreach (var pattern in passwordPatterns)
            {
                var index = sanitized.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var start = index + pattern.Length;
                    var end = sanitized.IndexOf(';', start);
                    if (end < 0) end = sanitized.Length;
                    sanitized = sanitized.Substring(0, start) + "****" + sanitized.Substring(end);
                }
            }

            return sanitized;
        }
    }
}