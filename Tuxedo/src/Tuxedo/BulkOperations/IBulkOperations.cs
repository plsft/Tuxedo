using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Tuxedo.BulkOperations
{
    public interface IBulkOperations
    {
        Task<int> BulkInsertAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class;

        Task<int> BulkUpdateAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class;

        Task<int> BulkDeleteAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class;

        Task<int> BulkMergeAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class;
    }
}