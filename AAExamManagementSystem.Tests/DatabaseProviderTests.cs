using AAExamManagementSystem.Repository;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Tests;

public class DatabaseProviderTests
{
    static DatabaseProviderTests()
    {
        // Program.cs sets this before anything else, and it changes how Npgsql maps DateTime (timestamp without time
        // zone instead of with), so the model only matches the Postgres migrations when it is on here too.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    [Theory]
    [InlineData(null, DatabaseProviderKind.Postgres)]
    [InlineData("", DatabaseProviderKind.Postgres)]
    [InlineData("  ", DatabaseProviderKind.Postgres)]
    [InlineData("Postgres", DatabaseProviderKind.Postgres)]
    [InlineData("PostgreSQL", DatabaseProviderKind.Postgres)]
    [InlineData("npgsql", DatabaseProviderKind.Postgres)]
    [InlineData("SqlServer", DatabaseProviderKind.SqlServer)]
    [InlineData("sql server", DatabaseProviderKind.SqlServer)]
    [InlineData("MSSQL", DatabaseProviderKind.SqlServer)]
    [InlineData("  sqlserver ", DatabaseProviderKind.SqlServer)]
    public void Parse_reads_the_provider_name(string? value, DatabaseProviderKind expected)
    {
        Assert.Equal(expected, DatabaseProvider.Parse(value));
    }

    [Theory]
    [InlineData("mysql")]
    [InlineData("sqlite")]
    [InlineData("postgre")]
    public void Parse_rejects_an_unknown_provider_instead_of_falling_back(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => DatabaseProvider.Parse(value));

        Assert.Contains("Database:Provider", error.Message);
    }

    private static ApplicationDbContext CreateContext(DatabaseProviderKind kind)
    {
        var connection = kind == DatabaseProviderKind.SqlServer
            ? "Server=localhost;Database=unused;Trusted_Connection=True"
            : "Host=localhost;Database=unused;Username=unused;Password=unused";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseProvider.Configure(options, kind, connection);
        return new ApplicationDbContext(options.Options);
    }

    [Theory]
    [InlineData(DatabaseProviderKind.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData(DatabaseProviderKind.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer")]
    public void Configure_uses_the_chosen_database(DatabaseProviderKind kind, string expectedProvider)
    {
        using var context = CreateContext(kind);

        Assert.Equal(expectedProvider, context.Database.ProviderName);
    }

    [Theory]
    [InlineData(DatabaseProviderKind.Postgres)]
    [InlineData(DatabaseProviderKind.SqlServer)]
    public void Each_provider_has_its_own_migrations(DatabaseProviderKind kind)
    {
        using var context = CreateContext(kind);

        var migrations = context.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, m => m.EndsWith("_InitialCreate"));
    }

    // Fails when an entity changes but a provider's migrations were not regenerated, which would only otherwise
    // show up as a broken database at startup on that provider.
    [Theory]
    [InlineData(DatabaseProviderKind.Postgres)]
    [InlineData(DatabaseProviderKind.SqlServer)]
    public void Each_providers_migrations_match_the_current_model(DatabaseProviderKind kind)
    {
        using var context = CreateContext(kind);

        Assert.False(context.Database.HasPendingModelChanges(),
            $"The {kind} migrations are out of date. Add a migration to AAExamManagementSystem.Migrations.{kind}.");
    }
}
