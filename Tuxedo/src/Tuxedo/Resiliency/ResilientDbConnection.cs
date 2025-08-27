using System;
using System.Data;

namespace Tuxedo.Resiliency
{
    public class ResilientDbConnection : IDbConnection
    {
        private readonly IDbConnection _innerConnection;
        private readonly IRetryPolicy _retryPolicy;

        public ResilientDbConnection(IDbConnection innerConnection, IRetryPolicy retryPolicy)
        {
            _innerConnection = innerConnection ?? throw new ArgumentNullException(nameof(innerConnection));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        }

        public string ConnectionString
        {
            get => _innerConnection.ConnectionString;
            set => _innerConnection.ConnectionString = value;
        }

        public int ConnectionTimeout => _innerConnection.ConnectionTimeout;
        public string Database => _innerConnection.Database;
        public ConnectionState State => _innerConnection.State;

        public IDbTransaction BeginTransaction()
        {
            return _retryPolicy.Execute(() => _innerConnection.BeginTransaction());
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return _retryPolicy.Execute(() => _innerConnection.BeginTransaction(il));
        }

        public void ChangeDatabase(string databaseName)
        {
            _retryPolicy.Execute(() => _innerConnection.ChangeDatabase(databaseName));
        }

        public void Close()
        {
            _innerConnection.Close();
        }

        public IDbCommand CreateCommand()
        {
            var command = _innerConnection.CreateCommand();
            return new ResilientDbCommand(command, _retryPolicy);
        }

        public void Dispose()
        {
            _innerConnection.Dispose();
        }

        public void Open()
        {
            _retryPolicy.Execute(() => _innerConnection.Open());
        }
    }

    public class ResilientDbCommand : IDbCommand
    {
        private readonly IDbCommand _innerCommand;
        private readonly IRetryPolicy _retryPolicy;

        public ResilientDbCommand(IDbCommand innerCommand, IRetryPolicy retryPolicy)
        {
            _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        }

        public string CommandText
        {
            get => _innerCommand.CommandText;
            set => _innerCommand.CommandText = value;
        }

        public int CommandTimeout
        {
            get => _innerCommand.CommandTimeout;
            set => _innerCommand.CommandTimeout = value;
        }

        public CommandType CommandType
        {
            get => _innerCommand.CommandType;
            set => _innerCommand.CommandType = value;
        }

        public IDbConnection? Connection
        {
            get => _innerCommand.Connection;
            set => _innerCommand.Connection = value;
        }

        public IDataParameterCollection Parameters => _innerCommand.Parameters;

        public IDbTransaction? Transaction
        {
            get => _innerCommand.Transaction;
            set => _innerCommand.Transaction = value;
        }

        public UpdateRowSource UpdatedRowSource
        {
            get => _innerCommand.UpdatedRowSource;
            set => _innerCommand.UpdatedRowSource = value;
        }

        public void Cancel()
        {
            _innerCommand.Cancel();
        }

        public IDbDataParameter CreateParameter()
        {
            return _innerCommand.CreateParameter();
        }

        public void Dispose()
        {
            _innerCommand.Dispose();
        }

        public int ExecuteNonQuery()
        {
            return _retryPolicy.Execute(() => _innerCommand.ExecuteNonQuery());
        }

        public IDataReader ExecuteReader()
        {
            return _retryPolicy.Execute(() => _innerCommand.ExecuteReader());
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            return _retryPolicy.Execute(() => _innerCommand.ExecuteReader(behavior));
        }

        public object? ExecuteScalar()
        {
            return _retryPolicy.Execute(() => _innerCommand.ExecuteScalar());
        }

        public void Prepare()
        {
            _innerCommand.Prepare();
        }
    }
}