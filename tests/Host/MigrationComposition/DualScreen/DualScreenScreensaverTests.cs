using System.Net;
using System.Net.Http.Headers;
using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Identity.DeviceSessions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

// V1-CDP-001: the business's own idle-screen screensaver image — storage
// round trip at the DualScreenStore layer, plus the HTTP-level validation
// and authentication behavior the endpoint handler itself is responsible
// for (PUT/DELETE reuse Catalog's manager gate, GET reuses the display
// principal's own read-only session).
[Collection("Host database password environment")]
public sealed class DualScreenScreensaverTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-cdp001-webroot-{Guid.NewGuid():N}");
    private NpgsqlDataSource? _dataSource;
    private DualScreenStore? _store;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);

        _dataSource = NpgsqlDataSource.Create(CreateConnectionString());
        _store = new DualScreenStore(_dataSource);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task GetScreensaverReturnsNullWhenNoneHasBeenSet()
    {
        Assert.Null(await _store!.GetScreensaverAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveThenGetRoundTripsTheSameContentAndType()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        await _store!.SaveScreensaverAsync(content, "image/png", CancellationToken.None);

        var image = await _store.GetScreensaverAsync(CancellationToken.None);

        Assert.NotNull(image);
        Assert.Equal(content, image!.Content);
        Assert.Equal("image/png", image.ContentType);
    }

    [Fact]
    public async Task SaveTwiceReplacesTheSingletonRowRatherThanInsertingASecondOne()
    {
        await _store!.SaveScreensaverAsync([1], "image/png", CancellationToken.None);
        await _store.SaveScreensaverAsync([2, 2], "image/webp", CancellationToken.None);

        var image = await _store.GetScreensaverAsync(CancellationToken.None);

        Assert.Equal(new byte[] { 2, 2 }, image!.Content);
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(
            1L,
            await ScalarAsync<long>("SELECT count(*) FROM customer_display.screensaver_images;"));
    }

    [Fact]
    public async Task DeleteAfterSaveLeavesNoImage()
    {
        await _store!.SaveScreensaverAsync([9], "image/jpeg", CancellationToken.None);
        await _store.DeleteScreensaverAsync(CancellationToken.None);

        Assert.Null(await _store.GetScreensaverAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutWithoutAManagerSessionIsRejected()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "screensaver.png");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWithoutADisplaySessionIsRejected()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customer-displays/{Guid.NewGuid():D}/screensaver");
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutRejectsADisallowedContentType()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-1");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-1");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "screensaver.pdf");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store!.GetScreensaverAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutRejectsAFileOverFiveMegabytes()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-2");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-2");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "too-big.png");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store!.GetScreensaverAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutRejectsADisallowedVideoContentType()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-quicktime");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-quicktime");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/quicktime");
        content.Add(fileContent, "file", "screensaver.mov");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store!.GetScreensaverAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutRejectsAVideoOverTwentyMegabytes()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-big-video");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-big-video");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[20 * 1024 * 1024 + 1]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(fileContent, "file", "too-big.mp4");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await _store!.GetScreensaverAsync(CancellationToken.None));
    }

    // V1-CDP-004: proves the two content-class limits are genuinely
    // separate, not one shared cap silently widened to 20 MB — a video
    // bigger than the image limit (5 MB) but within its own (20 MB) must
    // still be accepted.
    [Fact]
    public async Task PutAcceptsAVideoLargerThanTheImageLimitButWithinTheVideoLimit()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-mid-video");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-mid-video");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[6 * 1024 * 1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(fileContent, "file", "reel.mp4");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await _store!.GetScreensaverAsync(CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("video/mp4", stored!.ContentType);
        Assert.Equal(6 * 1024 * 1024, stored.Content.Length);
    }

    [Fact]
    public async Task PutWithAValidVideoThenGetWithARealDisplaySessionRoundTripsTheSameBytesAndContentType()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-video-roundtrip");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-video-roundtrip");

        var bytes = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 };
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            content.Add(fileContent, "file", "reel.mp4");
            using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
            AddTrustedForwarding(putRequest);
            using var putResponse = await client.SendAsync(putRequest);
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);
        }

        var displayId = Guid.NewGuid();
        await SeedDisplaySessionAsync(displayId, "display-token-video-1");
        using var getClient = CreateClient(app);
        getClient.DefaultRequestHeaders.Add("Cookie", "alkaros.customer-display=display-token-video-1");

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customer-displays/{displayId:D}/screensaver");
        AddTrustedForwarding(getRequest);
        using var getResponse = await getClient.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("video/mp4", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await getResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PutWithAValidImageThenGetWithARealDisplaySessionRoundTripsTheSameBytes()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        using var client = CreateClient(app);
        await SeedManagerSessionAsync("manager-token-3");
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.manager=manager-token-3");

        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47 };
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", "logo.png");
            using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver") { Content = content };
            AddTrustedForwarding(putRequest);
            using var putResponse = await client.SendAsync(putRequest);
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);
        }

        var displayId = Guid.NewGuid();
        await SeedDisplaySessionAsync(displayId, "display-token-1");
        using var getClient = CreateClient(app);
        getClient.DefaultRequestHeaders.Add("Cookie", "alkaros.customer-display=display-token-1");

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customer-displays/{displayId:D}/screensaver");
        AddTrustedForwarding(getRequest);
        using var getResponse = await getClient.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await getResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetAfterDeleteReturnsNotFound()
    {
        await using var app = DualScreenApplication.Build(BuildOptions());
        await app.StartAsync();
        await _store!.SaveScreensaverAsync([1, 2], "image/png", CancellationToken.None);
        await _store.DeleteScreensaverAsync(CancellationToken.None);

        var displayId = Guid.NewGuid();
        await SeedDisplaySessionAsync(displayId, "display-token-2");
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Add("Cookie", "alkaros.customer-display=display-token-2");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customer-displays/{displayId:D}/screensaver");
        AddTrustedForwarding(request);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private DualScreenOptions BuildOptions() => new(
        CreateConnectionString(),
        _webRoot,
        "http://127.0.0.1:0",
        TrustedProxies: [IPAddress.Loopback]);

    private static void AddTrustedForwarding(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }

    private static HttpClient CreateClient(Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private async Task SeedManagerSessionAsync(string rawToken)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'Screensaver Test Manager', true);

            INSERT INTO identity.permissions (permission_id, code, name)
            VALUES (gen_random_uuid(), 'catalog.manage', 'Manage catalog')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO identity.roles (role_id, code, name)
            VALUES (@role_id, @role_code, 'Screensaver Test Manager Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), @role_id, permission_id
            FROM identity.permissions WHERE code = 'catalog.manage';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (gen_random_uuid(), @user_id, @role_id);

            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("username", "screensaver-manager-" + userId.ToString("N")[..8]);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("role_code", "screensaver-manager-" + roleId.ToString("N")[..8]);
        command.Parameters.AddWithValue("device_id", "manager:" + rawToken);
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedDisplaySessionAsync(Guid displayId, string rawToken)
    {
        var terminalId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO customer_display.terminals (terminal_id, active_order_id, row_version, created_at, updated_at)
            VALUES (@terminal_id, NULL, 1, now(), now());

            INSERT INTO customer_display.display_sessions
                (session_id, display_id, terminal_id, token_hash, created_at, expires_at)
            VALUES (gen_random_uuid(), @display_id, @terminal_id, @token_hash, now(), now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        command.Parameters.AddWithValue("display_id", displayId);
        command.Parameters.AddWithValue("token_hash", DualScreenToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string CreateConnectionString()
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
