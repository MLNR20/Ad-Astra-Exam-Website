using AAExamManagementSystem.Data;
using AAExamManagementSystem.Repository;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Whether the database is ready to serve requests. Migrating and seeding a remote database can take minutes on a
/// cold start, so the web server starts listening first and requests are held back by
/// <see cref="StartupGateMiddleware"/> until this says ready.
/// </summary>
public class DatabaseInitializationState
{
    private volatile bool _isReady;
    private volatile bool _hasFailed;

    public bool IsReady => _isReady;

    /// <summary>True once every attempt to prepare the database has failed.</summary>
    public bool HasFailed => _hasFailed;

    public void MarkReady()
    {
        _hasFailed = false;
        _isReady = true;
    }

    public void MarkFailed() => _hasFailed = true;

    public void MarkRetrying() => _hasFailed = false;
}

/// <summary>The database steps run at startup, kept behind an interface so the sequencing can be tested.</summary>
public interface IDatabaseBootstrapper
{
    Task MigrateAsync(CancellationToken cancellationToken);

    /// <summary>True when an earlier run already seeded the database, so the site can open before seeding repeats.</summary>
    Task<bool> IsSeededAsync(CancellationToken cancellationToken);

    Task SeedAsync(CancellationToken cancellationToken);
}

public class DatabaseBootstrapper : IDatabaseBootstrapper
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseBootstrapper(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public async Task<bool> IsSeededAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AnyAsync(u => u.UserName == IdentitySeeder.AdminUserName, cancellationToken)
            && await db.Sections.IgnoreQueryFilters().AnyAsync(cancellationToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    }
}

/// <summary>
/// Migrates and seeds the database in the background so Kestrel is already listening (and /healthz already answering)
/// while it runs. A host that waits for the port, such as Render, would otherwise time out on a slow start.
/// </summary>
public class DatabaseInitializer : BackgroundService
{
    public const int MaxAttempts = 5;

    private readonly IDatabaseBootstrapper _bootstrapper;
    private readonly DatabaseInitializationState _state;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDatabaseBootstrapper bootstrapper,
        DatabaseInitializationState state,
        ILogger<DatabaseInitializer> logger)
    {
        _bootstrapper = bootstrapper;
        _state = state;
        _logger = logger;
    }

    /// <summary>The pause before a failed attempt is retried; it grows with each attempt.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give control back at once; the host does not finish starting until the first await in here.
        await Task.Yield();

        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await InitializeAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preparing the database failed (attempt {Attempt} of {Max}).", attempt, MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    _state.MarkFailed();
                    return;
                }

                _state.MarkRetrying();
                try
                {
                    await Task.Delay(RetryDelay * attempt, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _bootstrapper.MigrateAsync(cancellationToken);

        // A database that was seeded on an earlier start is usable now; seeding still runs, but behind an open site.
        if (await _bootstrapper.IsSeededAsync(cancellationToken))
        {
            _state.MarkReady();
            _logger.LogInformation("Database is ready; refreshing seed data in the background.");
            await _bootstrapper.SeedAsync(cancellationToken);
            return;
        }

        await _bootstrapper.SeedAsync(cancellationToken);
        _state.MarkReady();
        _logger.LogInformation("Database migrated and seeded.");
    }
}
