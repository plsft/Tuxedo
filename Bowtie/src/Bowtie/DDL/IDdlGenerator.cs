using Bowtie.Models;

namespace Bowtie.DDL
{
    public interface IDdlGenerator
    {
        DatabaseProvider Provider { get; }
        string GenerateCreateTable(TableModel table);
        string GenerateDropTable(TableModel table);
        string GenerateAlterTable(TableModel currentTable, TableModel targetTable);
        string GenerateCreateIndex(IndexModel index, string tableName);
        string GenerateDropIndex(IndexModel index, string tableName);
        string GenerateAddColumn(ColumnModel column, string tableName);
        string GenerateDropColumn(string columnName, string tableName);
        string GenerateAlterColumn(ColumnModel currentColumn, ColumnModel targetColumn, string tableName);
        string GenerateAddConstraint(ConstraintModel constraint, string tableName);
        string GenerateDropConstraint(string constraintName, string tableName);
        List<string> GenerateMigrationScript(List<TableModel> currentTables, List<TableModel> targetTables);
        string MapNetTypeToDbType(Type netType, ColumnModel column);
        bool ValidateIndexType(IndexType indexType);
    }
}