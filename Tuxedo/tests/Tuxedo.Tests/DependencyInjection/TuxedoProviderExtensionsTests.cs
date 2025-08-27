using System;
using System.Data;
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
    public class TuxedoProviderExtensionsTests
    {
        private const string TestSqlServerConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";
        private const string TestPostgresConnectionString = "Host=localhost;Database=testdb;Username=test;Password=test;";
        private const string TestMySqlConnectionString = "Server=localhost;Database=testdb;Uid=test;Pwd=test;";

        [Fact]
        public void AddTuxedoSqlServer_RegistersSqlConnection()
        {
            var services = new ServiceCollection();
            
            services.AddTuxedoSqlServer(TestSqlServerConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<SqlConnection>(connection);
            Assert.Contains("Server=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoSqlServer_WithConfiguration_AppliesConfiguration()
        {
            var services = new ServiceCollection();
            var configurationApplied = false;
            
            services.AddTuxedoSqlServer(
                TestSqlServerConnectionString,
                conn => 
                {
                    configurationApplied = true;
                    Assert.NotNull(conn);
                });
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.True(configurationApplied);
        }

        [Fact]
        public void AddTuxedoSqlServer_WithConnectionStringFactory_UsesFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new ConnectionStringProvider { ConnectionString = TestSqlServerConnectionString });
            
            services.AddTuxedoSqlServer(provider => 
                provider.GetRequiredService<ConnectionStringProvider>().ConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<SqlConnection>(connection);
            Assert.Contains("Server=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoSqlServer_ScopedLifetime_CreatesScopedConnection()
        {
            var services = new ServiceCollection();
            
            services.AddTuxedoSqlServer(TestSqlServerConnectionString, ServiceLifetime.Scoped);
            
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
        public void AddTuxedoPostgres_RegistersNpgsqlConnection()
        {
            var services = new ServiceCollection();
            
            services.AddTuxedoPostgres(TestPostgresConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<NpgsqlConnection>(connection);
            Assert.Contains("Host=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoPostgres_WithConfiguration_AppliesConfiguration()
        {
            var services = new ServiceCollection();
            var configurationApplied = false;
            
            services.AddTuxedoPostgres(
                TestPostgresConnectionString,
                conn => 
                {
                    configurationApplied = true;
                    Assert.NotNull(conn);
                });
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.True(configurationApplied);
        }

        [Fact]
        public void AddTuxedoPostgres_WithConnectionStringFactory_UsesFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new ConnectionStringProvider { ConnectionString = TestPostgresConnectionString });
            
            services.AddTuxedoPostgres(provider => 
                provider.GetRequiredService<ConnectionStringProvider>().ConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<NpgsqlConnection>(connection);
            Assert.Contains("Host=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoMySql_RegistersMySqlConnection()
        {
            var services = new ServiceCollection();
            
            services.AddTuxedoMySql(TestMySqlConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<MySqlConnection>(connection);
            Assert.Contains("Server=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoMySql_WithConfiguration_AppliesConfiguration()
        {
            var services = new ServiceCollection();
            var configurationApplied = false;
            
            services.AddTuxedoMySql(
                TestMySqlConnectionString,
                conn => 
                {
                    configurationApplied = true;
                    Assert.NotNull(conn);
                });
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.True(configurationApplied);
        }

        [Fact]
        public void AddTuxedoMySql_WithConnectionStringFactory_UsesFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new ConnectionStringProvider { ConnectionString = TestMySqlConnectionString });
            
            services.AddTuxedoMySql(provider => 
                provider.GetRequiredService<ConnectionStringProvider>().ConnectionString);
            
            var provider = services.BuildServiceProvider();
            var connection = provider.GetService<IDbConnection>();
            
            Assert.NotNull(connection);
            Assert.IsType<MySqlConnection>(connection);
            Assert.Contains("Server=localhost", connection.ConnectionString);
        }

        [Fact]
        public void AddTuxedoSqlServer_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoSqlServer(TestSqlServerConnectionString));
        }

        [Fact]
        public void AddTuxedoSqlServer_ThrowsArgumentException_WhenConnectionStringIsEmpty()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentException>(() =>
                services.AddTuxedoSqlServer(""));
        }

        [Fact]
        public void AddTuxedoPostgres_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoPostgres(TestPostgresConnectionString));
        }

        [Fact]
        public void AddTuxedoPostgres_ThrowsArgumentException_WhenConnectionStringIsEmpty()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentException>(() =>
                services.AddTuxedoPostgres(""));
        }

        [Fact]
        public void AddTuxedoMySql_ThrowsArgumentNullException_WhenServicesIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddTuxedoMySql(TestMySqlConnectionString));
        }

        [Fact]
        public void AddTuxedoMySql_ThrowsArgumentException_WhenConnectionStringIsEmpty()
        {
            var services = new ServiceCollection();
            
            Assert.Throws<ArgumentException>(() =>
                services.AddTuxedoMySql(""));
        }

        private class ConnectionStringProvider
        {
            public string ConnectionString { get; set; } = "";
        }
    }
}