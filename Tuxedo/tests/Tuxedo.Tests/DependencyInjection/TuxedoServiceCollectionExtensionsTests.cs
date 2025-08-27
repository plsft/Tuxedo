using System;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public class TuxedoServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddTuxedo_WithConnectionFactory_RegistersServices()
        {
            var services = new ServiceCollection();
            var testConnection = new TestDbConnection();
            
            services.AddTuxedo(_ => testConnection);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            var factory = provider.GetService<ITuxedoConnectionFactory>();
            
            Assert.NotNull(connection);
            Assert.NotNull(factory);
            Assert.Same(testConnection, connection);
        }

        [Fact]
        public void AddTuxedo_WithConnectionString_RegistersServices()
        {
            var services = new ServiceCollection();
            var connectionString = "Server=localhost;Database=test;";
            
            services.AddTuxedo(connectionString, cs => new TestDbConnection { ConnectionString = cs });
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>() as TestDbConnection;
            
            Assert.NotNull(connection);
            Assert.Equal(connectionString, connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedo_ScopedLifetime_CreatesDifferentInstancesPerScope()
        {
            var services = new ServiceCollection();
            var instanceCount = 0;
            
            services.AddTuxedo(_ => new TestDbConnection { InstanceId = ++instanceCount }, ServiceLifetime.Scoped);
            
            var provider = services.BuildServiceProvider();
            
            IDbConnection connection1, connection2, connection3;
            
            using (var scope1 = provider.CreateScope())
            {
                connection1 = scope1.ServiceProvider.GetRequiredService<IDbConnection>();
                connection2 = scope1.ServiceProvider.GetRequiredService<IDbConnection>();
            }
            
            using (var scope2 = provider.CreateScope())
            {
                connection3 = scope2.ServiceProvider.GetRequiredService<IDbConnection>();
            }
            
            Assert.Same(connection1, connection2);
            Assert.NotSame(connection1, connection3);
        }

        [Fact]
        public void AddTuxedo_TransientLifetime_CreatesNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            var instanceCount = 0;
            
            services.AddTuxedo(_ => new TestDbConnection { InstanceId = ++instanceCount }, ServiceLifetime.Transient);
            
            var provider = services.BuildServiceProvider();
            var connection1 = provider.GetRequiredService<IDbConnection>();
            var connection2 = provider.GetRequiredService<IDbConnection>();
            
            Assert.NotSame(connection1, connection2);
            Assert.Equal(1, ((TestDbConnection)connection1).InstanceId);
            Assert.Equal(2, ((TestDbConnection)connection2).InstanceId);
        }

        [Fact]
        public void AddTuxedo_SingletonLifetime_CreatesSingleInstance()
        {
            var services = new ServiceCollection();
            var instanceCount = 0;
            
            services.AddTuxedo(_ => new TestDbConnection { InstanceId = ++instanceCount }, ServiceLifetime.Singleton);
            
            var provider = services.BuildServiceProvider();
            var connection1 = provider.GetRequiredService<IDbConnection>();
            var connection2 = provider.GetRequiredService<IDbConnection>();
            
            Assert.Same(connection1, connection2);
            Assert.Equal(1, ((TestDbConnection)connection1).InstanceId);
            Assert.Equal(1, ((TestDbConnection)connection2).InstanceId);
        }

        [Fact]
        public void AddTuxedoWithOptions_RegistersServicesWithConfiguration()
        {
            var services = new ServiceCollection();
            var testOptions = new TestConnectionOptions { ConnectionString = "Server=test;" };
            
            services.AddSingleton(testOptions);
            services.AddTuxedoWithOptions<TestConnectionOptions>((provider, options) =>
            {
                return new TestDbConnection { ConnectionString = options.ConnectionString };
            });
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetRequiredService<IDbConnection>() as TestDbConnection;
            
            Assert.NotNull(connection);
            Assert.Equal(testOptions.ConnectionString, connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedo_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedo(_ => new TestDbConnection()));
        }

        [Fact]
        public void AddTuxedo_ThrowsArgumentNullException_WhenConnectionFactoryIsNull()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentNullException>(() =>
                services.AddTuxedo(null!));
        }

        [Fact]
        public void AddTuxedo_ThrowsArgumentException_WhenConnectionStringIsEmpty()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentException>(() =>
                services.AddTuxedo("", cs => new TestDbConnection()));
        }

        [Fact]
        public void TuxedoConnectionFactory_CreatesConnection()
        {
            var testConnection = new TestDbConnection();
            var factory = new TuxedoConnectionFactory(() => testConnection);
            
            var connection = factory.CreateConnection();
            
            Assert.Same(testConnection, connection);
        }

        [Fact]
        public void TuxedoConnectionFactory_ThrowsArgumentNullException_WhenFactoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TuxedoConnectionFactory(null!));
        }

        private class TestDbConnection : IDbConnection
        {
            public string ConnectionString { get; set; } = "";
            public int ConnectionTimeout => 30;
            public string Database => "TestDb";
            public ConnectionState State { get; set; }
            public int InstanceId { get; set; }
            
            public IDbTransaction BeginTransaction() => throw new NotImplementedException();
            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
            public void ChangeDatabase(string databaseName) => throw new NotImplementedException();
            public void Close() => State = ConnectionState.Closed;
            public IDbCommand CreateCommand() => throw new NotImplementedException();
            public void Dispose() { }
            public void Open() => State = ConnectionState.Open;
        }

        private class TestConnectionOptions
        {
            public string? ConnectionString { get; set; }
        }
    }
}