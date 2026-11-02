using System.Text.RegularExpressions;

namespace AAExamManagementSystem.Services;

/// <summary>Chooses which database provider the app runs on.</summary>
public static class DatabaseProviders
{
    public const string Postgres = "Postgres";
    public const string SqlServer = "SqlServer";

    /// <summary>
    /// An explicit setting wins ("Postgres", "PostgreSQL", "Npgsql", "SqlServer", "MSSQL"). Otherwise a connection
    /// string that starts with a "Host=" key is PostgreSQL and anything else is SQL Server.
    /// </summary>
    public static string Resolve(string? configured, string? connectionString)
    {
        switch (configured?.Trim().ToLowerInvariant())
        {
            case "postgres" or "postgresql" or "npgsql":
                return Postgres;
            case "sqlserver" or "mssql":
                return SqlServer;
        }

        return connectionString is not null && Regex.IsMatch(connectionString, @"(^|;)\s*Host\s*=", RegexOptions.IgnoreCase)
            ? Postgres
            : SqlServer;
    }
}
