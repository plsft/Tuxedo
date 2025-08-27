using System;
using System.Data;

namespace Tuxedo.DependencyInjection
{
    public class TuxedoOptions
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

    public class TuxedoSqlServerOptions : TuxedoOptions
    {
        public bool MultipleActiveResultSets { get; set; } = true;
        
        public bool TrustServerCertificate { get; set; }
        
        public int? ConnectTimeout { get; set; }
    }

    public class TuxedoPostgresOptions : TuxedoOptions
    {
        public bool Pooling { get; set; } = true;
        
        public int? MinPoolSize { get; set; }
        
        public int? MaxPoolSize { get; set; }
        
        public bool PrepareStatements { get; set; } = true;
    }

    public class TuxedoMySqlOptions : TuxedoOptions
    {
        public bool AllowUserVariables { get; set; } = true;
        
        public bool UseCompression { get; set; }
        
        public uint? ConnectionLifeTime { get; set; }
        
        public bool ConvertZeroDateTime { get; set; } = true;
    }
}