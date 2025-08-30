using System.Data;
using Bowtie.Models;

namespace Bowtie.Introspection
{
    public interface IDatabaseIntrospector
    {
        DatabaseProvider Provider { get; }
        Task<List<TableModel>> GetTablesAsync(IDbConnection connection, string? schema = null);
        Task<List<ColumnModel>> GetColumnsAsync(IDbConnection connection, string tableName, string? schema = null);
        Task<List<IndexModel>> GetIndexesAsync(IDbConnection connection, string tableName, string? schema = null);
        Task<List<ConstraintModel>> GetConstraintsAsync(IDbConnection connection, string tableName, string? schema = null);
        Task<bool> TableExistsAsync(IDbConnection connection, string tableName, string? schema = null);
        Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName, string? schema = null);
    }
}