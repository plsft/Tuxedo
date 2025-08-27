using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tuxedo.DependencyInjection;
using Tuxedo.Contrib;

namespace Tuxedo.BulkOperations
{
    public class BulkOperations : IBulkOperations
    {
        private readonly TuxedoDialect _dialect;

        public BulkOperations(TuxedoDialect dialect = TuxedoDialect.SqlServer)
        {
            _dialect = dialect;
        }

        public async Task<int> BulkInsertAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return 0;

            tableName ??= GetTableName<T>();
            var properties = GetInsertableProperties<T>();
            var totalInserted = 0;

            foreach (var batch in GetBatches(entityList, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = BuildBulkInsertSql(tableName, properties, batch.Count);
                var parameters = CreateBulkParameters(batch, properties);

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                AddParameters(command, parameters);
                totalInserted += await Task.Run(() => command.ExecuteNonQuery(), cancellationToken).ConfigureAwait(false);
            }

            return totalInserted;
        }

        public async Task<int> BulkUpdateAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return 0;

            tableName ??= GetTableName<T>();
            var keyProperty = GetKeyProperty<T>();
            var updateProperties = GetUpdateableProperties<T>();
            var totalUpdated = 0;

            foreach (var batch in GetBatches(entityList, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                foreach (var entity in batch)
                {
                    var sql = BuildUpdateSql(tableName, updateProperties, keyProperty);
                    var parameters = CreateUpdateParameters(entity, updateProperties, keyProperty);

                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Transaction = transaction;
                    if (commandTimeout.HasValue)
                        command.CommandTimeout = commandTimeout.Value;

                    AddParameters(command, parameters);
                    totalUpdated += await Task.Run(() => command.ExecuteNonQuery(), cancellationToken).ConfigureAwait(false);
                }
            }

            return totalUpdated;
        }

        public async Task<int> BulkDeleteAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return 0;

            tableName ??= GetTableName<T>();
            var keyProperty = GetKeyProperty<T>();
            var totalDeleted = 0;

            foreach (var batch in GetBatches(entityList, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = BuildBulkDeleteSql(tableName, keyProperty, batch.Count);
                var parameters = CreateDeleteParameters(batch, keyProperty);

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                AddParameters(command, parameters);
                totalDeleted += await Task.Run(() => command.ExecuteNonQuery(), cancellationToken).ConfigureAwait(false);
            }

            return totalDeleted;
        }

        public async Task<int> BulkMergeAsync<T>(
            IDbConnection connection,
            IEnumerable<T> entities,
            string? tableName = null,
            IDbTransaction? transaction = null,
            int batchSize = 1000,
            int? commandTimeout = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return 0;

            tableName ??= GetTableName<T>();
            var keyProperty = GetKeyProperty<T>();
            var properties = GetInsertableProperties<T>();
            var totalMerged = 0;

            foreach (var batch in GetBatches(entityList, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sql = BuildMergeSql(tableName, properties, keyProperty);
                var parameters = CreateBulkParameters(batch, properties);

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                AddParameters(command, parameters);
                totalMerged += await Task.Run(() => command.ExecuteNonQuery(), cancellationToken).ConfigureAwait(false);
            }

            return totalMerged;
        }

        private string BuildBulkInsertSql(string tableName, PropertyInfo[] properties, int recordCount)
        {
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var valuesBuilder = new StringBuilder();

            for (int i = 0; i < recordCount; i++)
            {
                if (i > 0)
                    valuesBuilder.Append(", ");

                valuesBuilder.Append("(");
                valuesBuilder.Append(string.Join(", ", properties.Select((p, j) => $"@p{i}_{j}")));
                valuesBuilder.Append(")");
            }

            return $"INSERT INTO {tableName} ({columns}) VALUES {valuesBuilder}";
        }

        private string BuildUpdateSql(string tableName, PropertyInfo[] updateProperties, PropertyInfo keyProperty)
        {
            var setClause = string.Join(", ", updateProperties.Select((p, i) => $"{p.Name} = @p{i}"));
            return $"UPDATE {tableName} SET {setClause} WHERE {keyProperty.Name} = @key";
        }

        private string BuildBulkDeleteSql(string tableName, PropertyInfo keyProperty, int recordCount)
        {
            var inClause = string.Join(", ", Enumerable.Range(0, recordCount).Select(i => $"@p{i}"));
            return $"DELETE FROM {tableName} WHERE {keyProperty.Name} IN ({inClause})";
        }

        private string BuildMergeSql(string tableName, PropertyInfo[] properties, PropertyInfo keyProperty)
        {
            if (_dialect != TuxedoDialect.SqlServer)
            {
                // For non-SQL Server, use INSERT ... ON CONFLICT (PostgreSQL) or INSERT ... ON DUPLICATE KEY (MySQL)
                return BuildUpsertSql(tableName, properties, keyProperty);
            }

            // SQL Server MERGE statement
            var sourceColumns = string.Join(", ", properties.Select(p => p.Name));
            var targetColumns = string.Join(", ", properties.Select(p => $"Target.{p.Name}"));
            var sourceValues = string.Join(", ", properties.Select((p, i) => $"@p{i} AS {p.Name}"));
            var updateSet = string.Join(", ", properties.Where(p => p != keyProperty).Select(p => $"Target.{p.Name} = Source.{p.Name}"));
            var insertColumns = string.Join(", ", properties.Select(p => p.Name));
            var insertValues = string.Join(", ", properties.Select(p => $"Source.{p.Name}"));

            return $@"
                MERGE {tableName} AS Target
                USING (SELECT {sourceValues}) AS Source
                ON Target.{keyProperty.Name} = Source.{keyProperty.Name}
                WHEN MATCHED THEN
                    UPDATE SET {updateSet}
                WHEN NOT MATCHED THEN
                    INSERT ({insertColumns}) VALUES ({insertValues});";
        }

        private string BuildUpsertSql(string tableName, PropertyInfo[] properties, PropertyInfo keyProperty)
        {
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var values = string.Join(", ", properties.Select((p, i) => $"@p{i}"));
            var updateSet = string.Join(", ", properties.Where(p => p != keyProperty).Select(p => $"{p.Name} = EXCLUDED.{p.Name}"));

            return _dialect switch
            {
                TuxedoDialect.Postgres => $@"
                    INSERT INTO {tableName} ({columns}) 
                    VALUES ({values})
                    ON CONFLICT ({keyProperty.Name}) 
                    DO UPDATE SET {updateSet}",
                
                TuxedoDialect.MySql => $@"
                    INSERT INTO {tableName} ({columns}) 
                    VALUES ({values})
                    ON DUPLICATE KEY UPDATE {updateSet}",
                
                _ => throw new NotSupportedException($"Upsert not supported for dialect: {_dialect}")
            };
        }

        private IEnumerable<List<T>> GetBatches<T>(List<T> source, int batchSize)
        {
            for (int i = 0; i < source.Count; i += batchSize)
            {
                yield return source.Skip(i).Take(batchSize).ToList();
            }
        }

        private Dictionary<string, object?> CreateBulkParameters<T>(List<T> entities, PropertyInfo[] properties)
        {
            var parameters = new Dictionary<string, object?>();
            
            for (int i = 0; i < entities.Count; i++)
            {
                for (int j = 0; j < properties.Length; j++)
                {
                    var paramName = $"p{i}_{j}";
                    parameters[paramName] = properties[j].GetValue(entities[i]);
                }
            }

            return parameters;
        }

        private Dictionary<string, object?> CreateUpdateParameters<T>(T entity, PropertyInfo[] updateProperties, PropertyInfo keyProperty)
        {
            var parameters = new Dictionary<string, object?>();
            
            for (int i = 0; i < updateProperties.Length; i++)
            {
                parameters[$"p{i}"] = updateProperties[i].GetValue(entity);
            }
            
            parameters["key"] = keyProperty.GetValue(entity);
            return parameters;
        }

        private Dictionary<string, object?> CreateDeleteParameters<T>(List<T> entities, PropertyInfo keyProperty)
        {
            var parameters = new Dictionary<string, object?>();
            
            for (int i = 0; i < entities.Count; i++)
            {
                parameters[$"p{i}"] = keyProperty.GetValue(entities[i]);
            }

            return parameters;
        }

        private void AddParameters(IDbCommand command, Dictionary<string, object?> parameters)
        {
            foreach (var kvp in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = kvp.Key;
                parameter.Value = kvp.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        private string GetTableName<T>()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name + "s";
        }

        private PropertyInfo GetKeyProperty<T>()
        {
            var type = typeof(T);
            return type.GetProperties().FirstOrDefault(p => 
                p.GetCustomAttribute<KeyAttribute>() != null || 
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No key property found for type {type.Name}");
        }

        private PropertyInfo[] GetInsertableProperties<T>()
        {
            return typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite && 
                       p.GetCustomAttribute<ComputedAttribute>() == null &&
                       p.GetCustomAttribute<WriteAttribute>()?.Write != false)
                .ToArray();
        }

        private PropertyInfo[] GetUpdateableProperties<T>()
        {
            return typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite && 
                       p.GetCustomAttribute<KeyAttribute>() == null &&
                       p.GetCustomAttribute<ComputedAttribute>() == null &&
                       p.GetCustomAttribute<WriteAttribute>()?.Write != false)
                .ToArray();
        }
    }
}