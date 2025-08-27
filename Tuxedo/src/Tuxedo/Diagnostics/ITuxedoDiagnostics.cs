using System;
using System.Data;

namespace Tuxedo.Diagnostics
{
    public interface ITuxedoDiagnostics
    {
        event EventHandler<QueryExecutedEventArgs>? QueryExecuted;
        event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
        event EventHandler<TransactionEventArgs>? TransactionStarted;
        event EventHandler<TransactionEventArgs>? TransactionCommitted;
        event EventHandler<TransactionEventArgs>? TransactionRolledBack;
        event EventHandler<ConnectionEventArgs>? ConnectionOpened;
        event EventHandler<ConnectionEventArgs>? ConnectionClosed;
        event EventHandler<ErrorEventArgs>? ErrorOccurred;

        void OnQueryExecuted(QueryExecutedEventArgs args);
        void OnCommandExecuted(CommandExecutedEventArgs args);
        void OnTransactionStarted(TransactionEventArgs args);
        void OnTransactionCommitted(TransactionEventArgs args);
        void OnTransactionRolledBack(TransactionEventArgs args);
        void OnConnectionOpened(ConnectionEventArgs args);
        void OnConnectionClosed(ConnectionEventArgs args);
        void OnError(ErrorEventArgs args);
    }

    public class QueryExecutedEventArgs : EventArgs
    {
        public string Query { get; set; } = string.Empty;
        public object? Parameters { get; set; }
        public TimeSpan Duration { get; set; }
        public int RowsAffected { get; set; }
        public string? ConnectionString { get; set; }
    }

    public class CommandExecutedEventArgs : EventArgs
    {
        public string CommandText { get; set; } = string.Empty;
        public CommandType CommandType { get; set; }
        public object? Parameters { get; set; }
        public TimeSpan Duration { get; set; }
        public int RowsAffected { get; set; }
    }

    public class TransactionEventArgs : EventArgs
    {
        public string TransactionId { get; set; } = string.Empty;
        public IsolationLevel IsolationLevel { get; set; }
        public TimeSpan? Duration { get; set; }
    }

    public class ConnectionEventArgs : EventArgs
    {
        public string ConnectionString { get; set; } = string.Empty;
        public ConnectionState State { get; set; }
        public TimeSpan? OpenDuration { get; set; }
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; } = null!;
        public string? Query { get; set; }
        public object? Parameters { get; set; }
        public string? Context { get; set; }
    }
}