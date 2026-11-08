using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Repository;

public enum DatabaseProviderKind
{
    Postgres,
    SqlServer,
}

/// <summary>
/// Picks the database the app runs on. PostgreSQL (Supabase) is the default; SQL Server is selected with
/// <c>Database:Provider = SqlServer</c> (environment variable <c>Database__Provider</c>). Each provider has its own
/// migrations assembly, because one set of migrations cannot serve both.
/// </summary>
public static class DatabaseProvider
{
    public const string ConfigKey = "Database:Provider";

    public const string PostgresMigrationsAssembly = "AAExamManagementSystem.Migrations.Postgres";
    public const string SqlServerMigrationsAssembly = "AAExamManagementSystem.Migrations.SqlServer";

    /// <summary>Reads a provider name. An empty value means PostgreSQL; an unknown one is an error, not a silent default.</summary>
    public static DatabaseProviderKind Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DatabaseProviderKind.Postgres;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "npgsql" or "pgsql" => DatabaseProviderKind.Postgres,
            "sqlserver" or "sql server" or "mssql" => DatabaseProviderKind.SqlServer,
            _ => throw new ArgumentException(
                $"Unknown {ConfigKey} value '{value}'. Use 'Postgres' or 'SqlServer'.", nameof(value)),
        };
    }

    public static string MigrationsAssembly(DatabaseProviderKind kind) => kind switch
    {
        DatabaseProviderKind.SqlServer => SqlServerMigrationsAssembly,
        _ => PostgresMigrationsAssembly,
    };

    /// <summary>Points the context at the chosen database and at that provider's migrations.</summary>
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options, DatabaseProviderKind kind, string? connectionString)
    {
        var migrationsAssembly = MigrationsAssembly(kind);

        return kind switch
        {
            DatabaseProviderKind.SqlServer => options.UseSqlServer(
                connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)),
            _ => options.UseNpgsql(
                connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly)),
        };
    }
}
