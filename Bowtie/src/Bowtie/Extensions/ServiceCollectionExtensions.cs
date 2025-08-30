using Bowtie.Analysis;
using Bowtie.Core;
using Bowtie.DDL;
using Bowtie.Introspection;
using Microsoft.Extensions.DependencyInjection;

namespace Bowtie.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBowtie(this IServiceCollection services)
        {
            services.AddTransient<ModelAnalyzer>();
            services.AddTransient<DataLossAnalyzer>();
            services.AddTransient<DatabaseSynchronizer>();
            services.AddTransient<ScriptGenerator>();
            services.AddTransient<ModelValidator>();
            
            services.AddTransient<IDdlGenerator, SqlServerDdlGenerator>();
            services.AddTransient<IDdlGenerator, PostgreSqlDdlGenerator>();
            services.AddTransient<IDdlGenerator, MySqlDdlGenerator>();
            services.AddTransient<IDdlGenerator, SqliteDdlGenerator>();
            
            services.AddTransient<IDatabaseIntrospector, SqlServerIntrospector>();
            services.AddTransient<IDatabaseIntrospector, PostgreSqlIntrospector>();
            
            return services;
        }

        public static IServiceCollection AddBowtieForProvider(this IServiceCollection services, DatabaseProvider provider)
        {
            services.AddTransient<ModelAnalyzer>();
            services.AddTransient<DataLossAnalyzer>();
            services.AddTransient<DatabaseSynchronizer>();
            services.AddTransient<ScriptGenerator>();
            services.AddTransient<ModelValidator>();
            
            services.AddTransient<IDdlGenerator>(serviceProvider =>
            {
                return provider switch
                {
                    DatabaseProvider.SqlServer => new SqlServerDdlGenerator(),
                    DatabaseProvider.PostgreSQL => new PostgreSqlDdlGenerator(),
                    DatabaseProvider.MySQL => new MySqlDdlGenerator(),
                    DatabaseProvider.SQLite => new SqliteDdlGenerator(),
                    _ => throw new NotSupportedException($"Provider {provider} is not supported")
                };
            });
            
            return services;
        }
    }
}