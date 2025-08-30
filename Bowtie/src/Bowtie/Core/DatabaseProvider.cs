namespace Bowtie.Core
{
    public enum DatabaseProvider
    {
        SqlServer,
        PostgreSQL,
        MySQL,
        SQLite
    }

    public static class DatabaseProviderExtensions
    {
        public static string GetConnectionString(this DatabaseProvider provider, string connectionString)
        {
            return connectionString;
        }

        public static string GetQuotedIdentifier(this DatabaseProvider provider, string identifier)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => $"[{identifier}]",
                DatabaseProvider.PostgreSQL => $"\"{identifier}\"",
                DatabaseProvider.MySQL => $"`{identifier}`",
                DatabaseProvider.SQLite => $"[{identifier}]",
                _ => identifier
            };
        }

        public static string GetParameterPrefix(this DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => "@",
                DatabaseProvider.PostgreSQL => "@",
                DatabaseProvider.MySQL => "@",
                DatabaseProvider.SQLite => "@",
                _ => "@"
            };
        }

        public static bool SupportsIndexType(this DatabaseProvider provider, IndexType indexType)
        {
            return indexType switch
            {
                IndexType.BTree => true,
                IndexType.Hash => provider == DatabaseProvider.PostgreSQL || provider == DatabaseProvider.MySQL,
                IndexType.GIN => provider == DatabaseProvider.PostgreSQL,
                IndexType.GiST => provider == DatabaseProvider.PostgreSQL,
                IndexType.BRIN => provider == DatabaseProvider.PostgreSQL,
                IndexType.SPGiST => provider == DatabaseProvider.PostgreSQL,
                IndexType.Clustered => provider == DatabaseProvider.SqlServer,
                IndexType.NonClustered => provider == DatabaseProvider.SqlServer,
                IndexType.ColumnStore => provider == DatabaseProvider.SqlServer,
                IndexType.Spatial => provider != DatabaseProvider.SQLite,
                IndexType.FullText => provider == DatabaseProvider.SqlServer || provider == DatabaseProvider.MySQL,
                _ => false
            };
        }

        public static bool SupportsSchemas(this DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => true,
                DatabaseProvider.PostgreSQL => true,
                DatabaseProvider.MySQL => false,
                DatabaseProvider.SQLite => false,
                _ => false
            };
        }

        public static string GetDefaultSchema(this DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => "dbo",
                DatabaseProvider.PostgreSQL => "public",
                DatabaseProvider.MySQL => "",
                DatabaseProvider.SQLite => "",
                _ => ""
            };
        }
    }
}