using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tuxedo.DependencyInjection;
using Xunit;

#if NET6_0
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Npgsql;
using MySqlConnector;

namespace Tuxedo.Tests.DependencyInjection
{
    public class TuxedoOptionsExtensionsTests
    {
        [Fact]
        public void AddTuxedoSqlServerWithOptions_ConfiguresFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TuxedoSqlServer:ConnectionString"] = "Server=localhost;Database=TestDb;",
                    ["TuxedoSqlServer:MultipleActiveResultSets"] = "true",
                    ["TuxedoSqlServer:TrustServerCertificate"] = "true",
                    ["TuxedoSqlServer:ConnectTimeout"] = "60",
                    ["TuxedoSqlServer:CommandTimeout"] = "120"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoSqlServerWithOptions(configuration);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<SqlConnection>(connection);
            
            var sqlConnection = (SqlConnection)connection;
            Assert.Contains("MultipleActiveResultSets=True", sqlConnection.ConnectionString);
            Assert.Contains("TrustServerCertificate=True", sqlConnection.ConnectionString);
            Assert.Contains("Connect Timeout=60", sqlConnection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoSqlServerWithOptions_ThrowsWhenConnectionStringMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TuxedoSqlServer:MultipleActiveResultSets"] = "true"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoSqlServerWithOptions(configuration);
            
            var provider = services.BuildServiceProvider();
            
            Assert.Throws<InvalidOperationException>(() => provider.GetService<IDbConnection>());
        }

        [Fact]
        public void AddTuxedoPostgresWithOptions_ConfiguresFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TuxedoPostgres:ConnectionString"] = "Host=localhost;Database=testdb;Username=test;Password=test;",
                    ["TuxedoPostgres:Pooling"] = "true",
                    ["TuxedoPostgres:MinPoolSize"] = "5",
                    ["TuxedoPostgres:MaxPoolSize"] = "100",
                    ["TuxedoPostgres:CommandTimeout"] = "60"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoPostgresWithOptions(configuration);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<NpgsqlConnection>(connection);
            
            var npgsqlConnection = (NpgsqlConnection)connection;
            Assert.Contains("Pooling=True", npgsqlConnection.ConnectionString);
            Assert.Contains("Min Pool Size=5", npgsqlConnection.ConnectionString);
            Assert.Contains("Max Pool Size=100", npgsqlConnection.ConnectionString);
            Assert.Contains("Command Timeout=60", npgsqlConnection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoPostgresWithOptions_UsesCustomSectionName()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["CustomPostgres:ConnectionString"] = "Host=localhost;Database=customdb;Username=test;Password=test;",
                    ["CustomPostgres:Pooling"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoPostgresWithOptions(configuration, "CustomPostgres");
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<NpgsqlConnection>(connection);
            Assert.Contains("Database=customdb", connection.ConnectionString);
            Assert.Contains("Pooling=False", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoMySqlWithOptions_ConfiguresFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TuxedoMySql:ConnectionString"] = "Server=localhost;Database=testdb;Uid=test;Pwd=test;",
                    ["TuxedoMySql:AllowUserVariables"] = "true",
                    ["TuxedoMySql:UseCompression"] = "true",
                    ["TuxedoMySql:ConvertZeroDateTime"] = "true",
                    ["TuxedoMySql:ConnectionLifeTime"] = "300",
                    ["TuxedoMySql:CommandTimeout"] = "90"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoMySqlWithOptions(configuration);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<MySqlConnection>(connection);
            
            var mySqlConnection = (MySqlConnection)connection;
            Assert.Contains("AllowUserVariables=True", mySqlConnection.ConnectionString);
            Assert.Contains("UseCompression=True", mySqlConnection.ConnectionString);
            Assert.Contains("ConvertZeroDateTime=True", mySqlConnection.ConnectionString);
            Assert.Contains("ConnectionLifeTime=300", mySqlConnection.ConnectionString);
            Assert.Contains("DefaultCommandTimeout=90", mySqlConnection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoMySqlWithOptions_ThrowsWhenConnectionStringMissing()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TuxedoMySql:AllowUserVariables"] = "true"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTuxedoMySqlWithOptions(configuration);
            
            var provider = services.BuildServiceProvider();
            
            Assert.Throws<InvalidOperationException>(() => provider.GetService<IDbConnection>());
        }

        [Fact]
        public void AddTuxedoSqlServerWithOptions_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            var configuration = new ConfigurationBuilder().Build();
            
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoSqlServerWithOptions(configuration));
        }

        [Fact]
        public void AddTuxedoSqlServerWithOptions_ThrowsArgumentNullException_WhenConfigurationIsNull()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentNullException>(() =>
                services.AddTuxedoSqlServerWithOptions(null!));
        }

        [Fact]
        public void AddTuxedoPostgresWithOptions_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            var configuration = new ConfigurationBuilder().Build();
            
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoPostgresWithOptions(configuration));
        }

        [Fact]
        public void AddTuxedoPostgresWithOptions_ThrowsArgumentNullException_WhenConfigurationIsNull()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentNullException>(() =>
                services.AddTuxedoPostgresWithOptions(null!));
        }

        [Fact]
        public void AddTuxedoMySqlWithOptions_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            var configuration = new ConfigurationBuilder().Build();
            
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoMySqlWithOptions(configuration));
        }

        [Fact]
        public void AddTuxedoMySqlWithOptions_ThrowsArgumentNullException_WhenConfigurationIsNull()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentNullException>(() =>
                services.AddTuxedoMySqlWithOptions(null!));
        }

        [Fact]
        public void TuxedoOptions_AllPropertiesCanBeSet()
        {
            var testConnection = new TestDbConnection();
            var options = new TuxedoOptions
            {
                ConnectionFactory = _ => testConnection,
                DefaultCommandTimeoutSeconds = 30,
                OpenOnResolve = false,
                Dialect = TuxedoDialect.SqlServer
            };
            
            Assert.NotNull(options.ConnectionFactory);
            Assert.Equal(30, options.DefaultCommandTimeoutSeconds);
            Assert.False(options.OpenOnResolve);
            Assert.Equal(TuxedoDialect.SqlServer, options.Dialect);
            Assert.Same(testConnection, options.ConnectionFactory(null!));
        }

        [Fact]
        public void TuxedoLegacyOptions_AllPropertiesCanBeSet()
        {
            var options = new TuxedoLegacyOptions
            {
                ConnectionString = "TestConnection",
                CommandTimeout = 30,
                EnableSensitiveDataLogging = true,
                RetryPolicy = new RetryPolicy
                {
                    MaxRetryAttempts = 5,
                    RetryDelay = TimeSpan.FromSeconds(2),
                    ExponentialBackoff = false
                }
            };
            
            Assert.Equal("TestConnection", options.ConnectionString);
            Assert.Equal(30, options.CommandTimeout);
            Assert.True(options.EnableSensitiveDataLogging);
            Assert.NotNull(options.RetryPolicy);
            Assert.Equal(5, options.RetryPolicy.MaxRetryAttempts);
            Assert.Equal(TimeSpan.FromSeconds(2), options.RetryPolicy.RetryDelay);
            Assert.False(options.RetryPolicy.ExponentialBackoff);
        }

        [Fact]
        public void TuxedoSqlServerOptions_InheritsFromTuxedoLegacyOptions()
        {
            var options = new TuxedoSqlServerOptions
            {
                ConnectionString = "SqlServerConnection",
                MultipleActiveResultSets = false,
                TrustServerCertificate = true,
                ConnectTimeout = 45
            };
            
            Assert.Equal("SqlServerConnection", options.ConnectionString);
            Assert.False(options.MultipleActiveResultSets);
            Assert.True(options.TrustServerCertificate);
            Assert.Equal(45, options.ConnectTimeout);
        }

        [Fact]
        public void TuxedoPostgresOptions_InheritsFromTuxedoLegacyOptions()
        {
            var options = new TuxedoPostgresOptions
            {
                ConnectionString = "PostgresConnection",
                Pooling = false,
                MinPoolSize = 10,
                MaxPoolSize = 200,
                PrepareStatements = false
            };
            
            Assert.Equal("PostgresConnection", options.ConnectionString);
            Assert.False(options.Pooling);
            Assert.Equal(10, options.MinPoolSize);
            Assert.Equal(200, options.MaxPoolSize);
            Assert.False(options.PrepareStatements);
        }

        [Fact]
        public void TuxedoMySqlOptions_InheritsFromTuxedoLegacyOptions()
        {
            var options = new TuxedoMySqlOptions
            {
                ConnectionString = "MySqlConnection",
                AllowUserVariables = false,
                UseCompression = true,
                ConnectionLifeTime = 600,
                ConvertZeroDateTime = false
            };
            
            Assert.Equal("MySqlConnection", options.ConnectionString);
            Assert.False(options.AllowUserVariables);
            Assert.True(options.UseCompression);
            Assert.Equal(600u, options.ConnectionLifeTime);
            Assert.False(options.ConvertZeroDateTime);
        }

        private class TestDbConnection : IDbConnection
        {
            public string ConnectionString { get; set; } = "";
            public int ConnectionTimeout => 30;
            public string Database => "TestDb";
            public ConnectionState State { get; set; }

            public IDbTransaction BeginTransaction() => throw new NotImplementedException();
            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
            public void ChangeDatabase(string databaseName) => throw new NotImplementedException();
            public void Close() => State = ConnectionState.Closed;
            public IDbCommand CreateCommand() => throw new NotImplementedException();
            public void Dispose() { }
            public void Open() => State = ConnectionState.Open;
        }
    }
}