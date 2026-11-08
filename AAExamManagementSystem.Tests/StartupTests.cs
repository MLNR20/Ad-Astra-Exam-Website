using System.Net;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAExamManagementSystem.Tests;

public class StartupTests
{
    private sealed class FakeBootstrapper : IDatabaseBootstrapper
    {
        public bool Seeded { get; set; }
        public int MigrateFailures { get; set; }
        public int MigrateCalls { get; private set; }
        public int SeedCalls { get; private set; }
        public TaskCompletionSource<bool> SeedGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> MigrateGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool BlockMigrate { get; set; }
        public bool BlockSeed { get; set; }

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            MigrateCalls++;
            if (BlockMigrate)
            {
                await MigrateGate.Task;
            }

            if (MigrateCalls <= MigrateFailures)
            {
                throw new InvalidOperationException("database unreachable");
            }
        }

        public Task<bool> IsSeededAsync(CancellationToken cancellationToken) => Task.FromResult(Seeded);

        public async Task SeedAsync(CancellationToken cancellationToken)
        {
            SeedCalls++;
            if (BlockSeed)
            {
                await SeedGate.Task;
            }
        }
    }

    private static DatabaseInitializer CreateInitializer(FakeBootstrapper bootstrapper, DatabaseInitializationState state) =>
        new(bootstrapper, state, NullLogger<DatabaseInitializer>.Instance) { RetryDelay = TimeSpan.FromMilliseconds(1) };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "Timed out waiting for the condition.");
    }

    [Fact]
    public async Task StartAsync_returns_while_the_database_work_is_still_running()
    {
        var bootstrapper = new FakeBootstrapper { BlockMigrate = true };
        var state = new DatabaseInitializationState();
        var initializer = CreateInitializer(bootstrapper, state);

        var started = initializer.StartAsync(CancellationToken.None);

        Assert.True(await Task.WhenAny(started, Task.Delay(TimeSpan.FromSeconds(5))) == started,
            "The host must not wait for migration before it starts listening.");
        Assert.False(state.IsReady);

        bootstrapper.MigrateGate.SetResult(true);
        await initializer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_fresh_database_is_ready_only_after_seeding()
    {
        var bootstrapper = new FakeBootstrapper { Seeded = false, BlockSeed = true };
        var state = new DatabaseInitializationState();
        var initializer = CreateInitializer(bootstrapper, state);

        await initializer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => bootstrapper.SeedCalls == 1);

        Assert.False(state.IsReady);

        bootstrapper.SeedGate.SetResult(true);
        await WaitUntilAsync(() => state.IsReady);
        await initializer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task An_already_seeded_database_is_ready_before_seeding_finishes()
    {
        var bootstrapper = new FakeBootstrapper { Seeded = true, BlockSeed = true };
        var state = new DatabaseInitializationState();
        var initializer = CreateInitializer(bootstrapper, state);

        await initializer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => state.IsReady);

        Assert.Equal(1, bootstrapper.SeedCalls);

        bootstrapper.SeedGate.SetResult(true);
        await initializer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_failed_attempt_is_retried_and_the_site_still_comes_up()
    {
        var bootstrapper = new FakeBootstrapper { Seeded = true, MigrateFailures = 2 };
        var state = new DatabaseInitializationState();
        var initializer = CreateInitializer(bootstrapper, state);

        await initializer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => state.IsReady);

        Assert.Equal(3, bootstrapper.MigrateCalls);
        Assert.False(state.HasFailed);
        await initializer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Running_out_of_attempts_marks_failure_without_crashing_the_host()
    {
        var bootstrapper = new FakeBootstrapper { MigrateFailures = int.MaxValue };
        var state = new DatabaseInitializationState();
        var initializer = CreateInitializer(bootstrapper, state);

        await initializer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => state.HasFailed);

        Assert.False(state.IsReady);
        Assert.Equal(DatabaseInitializer.MaxAttempts, bootstrapper.MigrateCalls);
        await initializer.StopAsync(CancellationToken.None);
    }

    private static async Task<(int Status, string Body, IHeaderDictionary Headers, bool ReachedNext)> SendAsync(
        DatabaseInitializationState state, string path)
    {
        var reachedNext = false;
        var middleware = new StartupGateMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        }, state);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body, context.Response.Headers, reachedNext);
    }

    [Fact]
    public async Task Requests_get_a_503_page_while_the_database_is_starting()
    {
        var result = await SendAsync(new DatabaseInitializationState(), "/Dashboard");

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.False(result.ReachedNext);
        Assert.Equal("5", result.Headers.RetryAfter.ToString());
        Assert.Contains("starting up", result.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_health_check_answers_even_before_the_database_is_ready()
    {
        var result = await SendAsync(new DatabaseInitializationState(), "/healthz");

        Assert.True(result.ReachedNext);
    }

    [Fact]
    public async Task Requests_pass_through_once_the_database_is_ready()
    {
        var state = new DatabaseInitializationState();
        state.MarkReady();

        var result = await SendAsync(state, "/Dashboard");

        Assert.True(result.ReachedNext);
    }

    [Fact]
    public async Task A_failed_start_shows_the_unavailable_page_instead_of_the_starting_page()
    {
        var state = new DatabaseInitializationState();
        state.MarkFailed();

        var result = await SendAsync(state, "/Login");

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Contains("unavailable", result.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://ems.onrender.com", "https://ems.onrender.com/healthz")]
    [InlineData("https://ems.onrender.com/", "https://ems.onrender.com/healthz")]
    [InlineData("  https://ems.onrender.com/some/path  ", "https://ems.onrender.com/healthz")]
    [InlineData("http://localhost:5240", "http://localhost:5240/healthz")]
    public void The_ping_address_is_the_health_path_on_the_site(string baseUrl, string expected)
    {
        Assert.Equal(expected, KeepAliveService.BuildPingUri(baseUrl)?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void No_usable_site_address_turns_the_ping_off(string? baseUrl)
    {
        Assert.Null(KeepAliveService.BuildPingUri(baseUrl));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri?> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri);
            return Task.FromResult(respond(request));
        }
    }

    private static KeepAliveService CreateKeepAlive(StubHandler handler) =>
        new(new HttpClient(handler), new KeepAliveSettings(), NullLogger<KeepAliveService>.Instance);

    [Fact]
    public async Task A_successful_ping_requests_the_health_address_once()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var address = new Uri("https://ems.onrender.com/healthz");

        var ok = await CreateKeepAlive(handler).PingAsync(address, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(new[] { address }, handler.Requests);
    }

    [Fact]
    public async Task A_failing_status_or_a_network_error_is_reported_without_throwing()
    {
        var failing = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var broken = new StubHandler(_ => throw new HttpRequestException("no route"));
        var address = new Uri("https://ems.onrender.com/healthz");

        Assert.False(await CreateKeepAlive(failing).PingAsync(address, CancellationToken.None));
        Assert.False(await CreateKeepAlive(broken).PingAsync(address, CancellationToken.None));
    }

    [Fact]
    public async Task The_service_stays_idle_when_no_site_address_is_configured()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateKeepAlive(handler);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
    }
}
