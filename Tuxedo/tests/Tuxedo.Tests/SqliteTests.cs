using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Tuxedo.DependencyInjection;
using Tuxedo.Contrib;
using Xunit;

namespace Tuxedo.Tests
{
    public class SqliteTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IDbConnection _connection;

        public SqliteTests()
        {
            var services = new ServiceCollection();
            
            // Use in-memory SQLite for testing
            services.AddTuxedoSqliteInMemory("TestDb");
            
            _provider = services.BuildServiceProvider();
            _connection = _provider.GetRequiredService<IDbConnection>();
            
            // Initialize test database
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            _connection.Execute(@"
                CREATE TABLE TestEntities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Value REAL NOT NULL,
                    CreatedDate TEXT NOT NULL
                )
            ");
        }

        [Table("TestEntities")]
        public class TestEntity
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Value { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        [Fact]
        public async Task Should_Insert_And_Select_Entity()
        {
            // Arrange
            var entity = new TestEntity
            {
                Name = "Test Entity",
                Value = 123.45m,
                CreatedDate = DateTime.UtcNow
            };

            // Act
            var id = await _connection.InsertAsync(entity);
            var retrieved = await _connection.SelectAsync<TestEntity>(id);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Test Entity", retrieved.Name);
            Assert.Equal(123.45m, retrieved.Value);
        }

        [Fact]
        public async Task Should_Update_Entity()
        {
            // Arrange
            var entity = new TestEntity
            {
                Name = "Original Name",
                Value = 100m,
                CreatedDate = DateTime.UtcNow
            };
            var id = await _connection.InsertAsync(entity);
            entity.Id = id;

            // Act
            entity.Name = "Updated Name";
            entity.Value = 200m;
            var success = await _connection.UpdateAsync(entity);

            // Assert
            Assert.True(success);
            var updated = await _connection.SelectAsync<TestEntity>(id);
            Assert.Equal("Updated Name", updated.Name);
            Assert.Equal(200m, updated.Value);
        }

        [Fact]
        public async Task Should_Delete_Entity()
        {
            // Arrange
            var entity = new TestEntity
            {
                Name = "To Delete",
                Value = 50m,
                CreatedDate = DateTime.UtcNow
            };
            var id = await _connection.InsertAsync(entity);
            entity.Id = id;

            // Act
            var success = await _connection.DeleteAsync(entity);

            // Assert
            Assert.True(success);
            var deleted = await _connection.SelectAsync<TestEntity>(id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task Should_Query_With_Dapper()
        {
            // Arrange
            await _connection.InsertAsync(new TestEntity { Name = "Entity 1", Value = 10m, CreatedDate = DateTime.UtcNow });
            await _connection.InsertAsync(new TestEntity { Name = "Entity 2", Value = 20m, CreatedDate = DateTime.UtcNow });
            await _connection.InsertAsync(new TestEntity { Name = "Entity 3", Value = 30m, CreatedDate = DateTime.UtcNow });

            // Act
            var results = await _connection.QueryAsync<TestEntity>(
                "SELECT * FROM TestEntities WHERE Value > @minValue",
                new { minValue = 15m });

            // Assert
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void Should_Use_SqliteConnection_Type()
        {
            // Act
            var sqliteConnection = _provider.GetRequiredService<SqliteConnection>();

            // Assert
            Assert.NotNull(sqliteConnection);
            Assert.IsType<SqliteConnection>(sqliteConnection);
            Assert.Same(_connection, sqliteConnection);
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _provider?.Dispose();
        }
    }
}