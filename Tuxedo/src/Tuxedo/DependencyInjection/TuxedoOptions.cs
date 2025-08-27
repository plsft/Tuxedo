using System;
using System.Data;

namespace Tuxedo.DependencyInjection
{
    public sealed class TuxedoOptions
    {
        /// <summary>Factory that creates the provider-specific connection (SqlConnection, NpgsqlConnection, MySqlConnection).</summary>
        public Func<IServiceProvider, IDbConnection> ConnectionFactory { get; set; } = null!;

        /// <summary>When true, opens the connection immediately within the scope (recommended for per-request reuse).</summary>
        public bool OpenOnResolve { get; set; } = true;

        /// <summary>Optional default command timeout (seconds) you can set on created commands.</summary>
        public int? DefaultCommandTimeoutSeconds { get; set; }

        /// <summary>Database dialect used by some helpers (e.g., paging).</summary>
        public TuxedoDialect Dialect { get; set; } = TuxedoDialect.Postgres;
    }

    public class TuxedoLegacyOptions
    {
        public string? ConnectionString { get; set; }
        
        public int? CommandTimeout { get; set; }
        
        public bool EnableSensitiveDataLogging { get; set; }
        
        public RetryPolicy? RetryPolicy { get; set; }
    }

    public class RetryPolicy
    {
        public int MaxRetryAttempts { get; set; } = 3;
        
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        
        public bool ExponentialBackoff { get; set; } = true;
    }

    public class TuxedoSqlServerOptions : TuxedoLegacyOptions
    {
        public bool MultipleActiveResultSets { get; set; } = true;
        
        public bool TrustServerCertificate { get; set; }
        
        public int? ConnectTimeout { get; set; }
    }

    public class TuxedoPostgresOptions : TuxedoLegacyOptions
    {
        public bool Pooling { get; set; } = true;
        
        public int? MinPoolSize { get; set; }
        
        public int? MaxPoolSize { get; set; }
        
        public bool PrepareStatements { get; set; } = true;
    }

    public class TuxedoMySqlOptions : TuxedoLegacyOptions
    {
        public bool AllowUserVariables { get; set; } = true;
        
        public bool UseCompression { get; set; }
        
        public uint? ConnectionLifeTime { get; set; }
        
        public bool ConvertZeroDateTime { get; set; } = true;
    }
}