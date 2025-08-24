using MySqlConnector;
using Npgsql;
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using Xunit;
using Xunit.Sdk;

namespace Tuxedo.Tests
{
    // The test suites here implement TestSuiteBase so that each provider runs
    // the entire set of tests without declarations per method
    // If we want to support a new provider, they need only be added here - not in multiple places

    [XunitTestCaseDiscoverer("Tuxedo.Tests.SkippableFactDiscoverer", "Tuxedo.Tests")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SkippableFactAttribute : FactAttribute
    {
    }

    public class SqlServerTestSuite : TestSuite
    {
        private const string DbName = "tempdb";
        public static string ConnectionString =>
            GetConnectionString("SqlServerConnectionString", $"Data Source=.;Initial Catalog={DbName};Integrated Security=True");

        private static readonly bool _skip;

        public override IDbConnection GetConnection()
        {
            if (_skip) Skip.Inconclusive("Skipping SQL Server Tests - no server.");
            return new SqlConnection(ConnectionString);
        }

        static SqlServerTestSuite()
        {
            try
            {
            using (var connection = new SqlConnection(ConnectionString))
            {
                // ReSharper disable once AccessToDisposedClosure
                    void dropTable(string name) => connection.Execute($"IF OBJECT_ID('{name}', 'U') IS NOT NULL DROP TABLE [{name}]; ", (object)null);
                connection.Open();
                dropTable("Stuff");
                connection.Execute("CREATE TABLE Stuff (TheId int IDENTITY(1,1) not null, Name nvarchar(100) not null, Created DateTime null);", (object)null);
                dropTable("People");
                connection.Execute("CREATE TABLE People (Id int IDENTITY(1,1) not null, Name nvarchar(100) not null);", (object)null);
                dropTable("Users");
                connection.Execute("CREATE TABLE Users (Id int IDENTITY(1,1) not null, Name nvarchar(100) not null, Age int not null);", (object)null);
                dropTable("Automobiles");
                connection.Execute("CREATE TABLE Automobiles (Id int IDENTITY(1,1) not null, Name nvarchar(100) not null);", (object)null);
                dropTable("Results");
                connection.Execute("CREATE TABLE Results (Id int IDENTITY(1,1) not null, Name nvarchar(100) not null, [Order] int not null);", (object)null);
                dropTable("ObjectX");
                connection.Execute("CREATE TABLE ObjectX (ObjectXId nvarchar(100) not null, Name nvarchar(100) not null);", (object)null);
                dropTable("ObjectY");
                connection.Execute("CREATE TABLE ObjectY (ObjectYId int not null, Name nvarchar(100) not null);", (object)null);
                dropTable("ObjectZ");
                connection.Execute("CREATE TABLE ObjectZ (Id int not null, Name nvarchar(100) not null);", (object)null);
                dropTable("GenericType");
                connection.Execute("CREATE TABLE GenericType (Id nvarchar(100) not null, Name nvarchar(100) not null);", (object)null);
                dropTable("NullableDates");
                connection.Execute("CREATE TABLE NullableDates (Id int IDENTITY(1,1) not null, DateValue DateTime null);", (object)null);
            }
            }
            catch (Exception)
            {
                _skip = true;
            }
        }
    }

    public class MySqlServerTestSuite : TestSuite
    {
        public static string ConnectionString { get; } =
            GetConnectionString("MySqlConnectionString", "Server=localhost;Database=tests;Uid=test;Pwd=pass;UseAffectedRows=false;");

        public override IDbConnection GetConnection()
        {
            if (_skip) Skip.Inconclusive("Skipping MySQL Tests - no server.");
            return new MySqlConnection(ConnectionString);
        }

        private static readonly bool _skip;

        static MySqlServerTestSuite()
        {
            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    // ReSharper disable once AccessToDisposedClosure
                    void dropTable(string name) => connection.Execute($"DROP TABLE IF EXISTS `{name}`;", (object)null);
                    connection.Open();
                    dropTable("Stuff");
                    connection.Execute("CREATE TABLE Stuff (TheId int not null AUTO_INCREMENT PRIMARY KEY, Name nvarchar(100) not null, Created DateTime null);", (object)null);
                    dropTable("People");
                    connection.Execute("CREATE TABLE People (Id int not null AUTO_INCREMENT PRIMARY KEY, Name nvarchar(100) not null);", (object)null);
                    dropTable("Users");
                    connection.Execute("CREATE TABLE Users (Id int not null AUTO_INCREMENT PRIMARY KEY, Name nvarchar(100) not null, Age int not null);", (object)null);
                    dropTable("Automobiles");
                    connection.Execute("CREATE TABLE Automobiles (Id int not null AUTO_INCREMENT PRIMARY KEY, Name nvarchar(100) not null);", (object)null);
                    dropTable("Results");
                    connection.Execute("CREATE TABLE Results (Id int not null AUTO_INCREMENT PRIMARY KEY, Name nvarchar(100) not null, `Order` int not null);", (object)null);
                    dropTable("ObjectX");
                    connection.Execute("CREATE TABLE ObjectX (ObjectXId nvarchar(100) not null, Name nvarchar(100) not null);", (object)null);
                    dropTable("ObjectY");
                    connection.Execute("CREATE TABLE ObjectY (ObjectYId int not null, Name nvarchar(100) not null);", (object)null);
                    dropTable("ObjectZ");
                    connection.Execute("CREATE TABLE ObjectZ (Id int not null, Name nvarchar(100) not null);", (object)null);
                    dropTable("GenericType");
                    connection.Execute("CREATE TABLE GenericType (Id nvarchar(100) not null, Name nvarchar(100) not null);", (object)null);
                    dropTable("NullableDates");
                    connection.Execute("CREATE TABLE NullableDates (Id int not null AUTO_INCREMENT PRIMARY KEY, DateValue DateTime);", (object)null);
                }
            }
            catch (MySqlException e)
            {
                if (e.Message.Contains("Unable to connect"))
                    _skip = true;
                else
                    throw;
            }
        }
    }




    public class PostgresTestSuite : TestSuite
    {
        public static string ConnectionString { get; } =
            GetConnectionString("PostgresConnectionString", "Host=localhost;Port=5432;Database=tests;Username=test;Password=pass");

        public override IDbConnection GetConnection()
        {
            if (_skip) Skip.Inconclusive("Skipping PostgreSQL Tests - no server.");
            return new NpgsqlConnection(ConnectionString);
        }

        private static readonly bool _skip;

        static PostgresTestSuite()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    void dropTable(string name) => connection.Execute($"DROP TABLE IF EXISTS \"{name}\";", (object)null);
                    connection.Open();
                    dropTable("Stuff");
                    connection.Execute("CREATE TABLE \"Stuff\" (\"TheId\" SERIAL PRIMARY KEY, \"Name\" varchar(100) NOT NULL, \"Created\" timestamp NULL);", (object)null);
                    dropTable("People");
                    connection.Execute("CREATE TABLE \"People\" (\"Id\" SERIAL PRIMARY KEY, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("Users");
                    connection.Execute("CREATE TABLE \"Users\" (\"Id\" SERIAL PRIMARY KEY, \"Name\" varchar(100) NOT NULL, \"Age\" int NOT NULL);", (object)null);
                    dropTable("Automobiles");
                    connection.Execute("CREATE TABLE \"Automobiles\" (\"Id\" SERIAL PRIMARY KEY, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("Results");
                    connection.Execute("CREATE TABLE \"Results\" (\"Id\" SERIAL PRIMARY KEY, \"Name\" varchar(100) NOT NULL, \"Order\" int NOT NULL);", (object)null);
                    dropTable("ObjectX");
                    connection.Execute("CREATE TABLE \"ObjectX\" (\"ObjectXId\" varchar(100) NOT NULL, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("ObjectY");
                    connection.Execute("CREATE TABLE \"ObjectY\" (\"ObjectYId\" int NOT NULL, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("ObjectZ");
                    connection.Execute("CREATE TABLE \"ObjectZ\" (\"Id\" int NOT NULL, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("GenericType");
                    connection.Execute("CREATE TABLE \"GenericType\" (\"Id\" varchar(100) NOT NULL, \"Name\" varchar(100) NOT NULL);", (object)null);
                    dropTable("NullableDates");
                    connection.Execute("CREATE TABLE \"NullableDates\" (\"Id\" SERIAL PRIMARY KEY, \"DateValue\" timestamp NULL);", (object)null);
                }
            }
            catch (PostgresException)
            {
                _skip = true;
            }
            catch (NpgsqlException)
            {
                _skip = true;
            }
            catch (Exception)
            {
                _skip = true;
            }
        }
    }}



