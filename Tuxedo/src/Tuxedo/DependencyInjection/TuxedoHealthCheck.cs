using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tuxedo.DependencyInjection
{
    internal sealed class TuxedoHealthCheck : IHealthCheck
    {
        private readonly IDbConnection _connection;

        public TuxedoHealthCheck(IDbConnection connection) => _connection = connection;

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext? context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT 1";
                _ = cmd.ExecuteScalar();
                return Task.FromResult(HealthCheckResult.Healthy("Database reachable"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Database unreachable", ex));
            }
        }
    }
}