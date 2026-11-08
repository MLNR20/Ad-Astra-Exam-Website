namespace AAExamManagementSystem.Services;

public class KeepAliveSettings
{
    /// <summary>
    /// The site's public address. Left empty on a developer machine, which turns the ping off. On Render it falls back
    /// to the RENDER_EXTERNAL_URL that the platform sets, so nothing needs configuring there.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>Minutes between pings. Render's free plan sleeps a service after 15 minutes without traffic.</summary>
    public int IntervalMinutes { get; set; } = 10;
}

/// <summary>
/// Calls the site's own anonymous /healthz through its public address on a timer. The request enters through the
/// host's proxy, so it counts as traffic and stops a free-tier service from going to sleep while it is running.
/// </summary>
public class KeepAliveService : BackgroundService
{
    public const string HealthPath = "/healthz";

    private readonly HttpClient _client;
    private readonly KeepAliveSettings _settings;
    private readonly ILogger<KeepAliveService> _logger;

    public KeepAliveService(HttpClient client, KeepAliveSettings settings, ILogger<KeepAliveService> logger)
    {
        _client = client;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>The full address to ping, or null when no site address is configured.</summary>
    public static Uri? BuildPingUri(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new Uri(uri, HealthPath);
    }

    /// <summary>Pings once. Returns true when the site answered with a success status; never throws.</summary>
    public async Task<bool> PingAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(address, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning("Keep-alive ping to {Address} returned {Status}.", address, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Keep-alive ping to {Address} failed.", address);
        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = BuildPingUri(_settings.Url);
        if (address is null)
        {
            _logger.LogInformation("Keep-alive ping is off: no site address is configured.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.IntervalMinutes));
        _logger.LogInformation("Keep-alive ping on: {Address} every {Interval}.", address, interval);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PingAsync(address, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down.
        }
    }
}
