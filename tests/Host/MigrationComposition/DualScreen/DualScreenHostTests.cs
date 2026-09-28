using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Identity.Authentication;
using ALKAROS.Settings.ReservationStation;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

[Collection("Host database password environment")]
public sealed class DualScreenHostTests : IDisposable
{
    private readonly string? _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-host-test-{Guid.NewGuid():N}");

    public DualScreenHostTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Fact]
    public void DualScreenOptionsParsingSucceeds()
    {
        var options = DualScreenOptions.Parse([
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://127.0.0.1:5090"
        ]);

        Assert.NotNull(options);
        Assert.Equal("http://127.0.0.1:5090/", options.Url);
        Assert.Equal(_webRoot, options.WebRoot);
    }

    [Fact]
    public async Task UntrustedForwardedHeadersCannotChangeTheEffectiveScheme()
    {
        await using var app = await StartAsync([IPAddress.Parse("192.0.2.10")]);
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/not-found");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.20");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HTTPS_REQUIRED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrustedClientPartitionsAreIndependentAndRetryAfterComesFromTheLease()
    {
        await using var app = await StartAsync([IPAddress.Loopback]);
        using var client = CreateClient(app);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await SendPairingRequestAsync(client, "198.51.100.21");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        using var limited = await SendPairingRequestAsync(client, "198.51.100.21");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(values.Single(), out var seconds));
        Assert.InRange(seconds, 1, 60);

        using var independent = await SendPairingRequestAsync(client, "198.51.100.22");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, independent.StatusCode);
    }

    [Fact]
    public async Task ResponseStartedFailureIsLoggedWithTraceIdBeforeKestrelAbortsTheConnection()
    {
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        await using var app = builder.Build();
        DualScreenApplication.UseDualScreenErrorHandling(app);
        app.MapGet("/fault-after-start", async context =>
        {
            context.Response.ContentLength = 1024;
            context.Response.Headers["X-Test-Trace-Id"] = context.TraceIdentifier;
            await context.Response.StartAsync();
            await context.Response.Body.WriteAsync(new byte[] { 0x2A });
            await context.Response.Body.FlushAsync();
            throw new InvalidOperationException("Test fault after response start.");
        });
        await app.StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync(
            "/fault-after-start",
            HttpCompletionOption.ResponseHeadersRead);
        var traceId = response.Headers.GetValues("X-Test-Trace-Id").Single();
        var transportFailure = await Record.ExceptionAsync(
            async () => await response.Content.ReadAsByteArrayAsync());

        Assert.True(
            transportFailure is IOException or HttpRequestException,
            $"Expected an aborted HTTP response, received {transportFailure?.GetType().FullName ?? "no error"}.");
        var entry = Assert.Single(logs.Entries, candidate => candidate.EventId.Id == 5000);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("/fault-after-start", entry.Message, StringComparison.Ordinal);
        Assert.Contains(traceId, entry.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    private async Task<WebApplication> StartAsync(IReadOnlyList<IPAddress> trustedProxies)
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            "Host=localhost;Database=unused;Username=unused;Password=unused",
            _webRoot,
            "http://127.0.0.1:0",
            trustedProxies));
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task HostTerminatesHttpsWithASelfSignedCertificateAndNeedsNoForwardedProto()
    {
        var tlsCacheRoot = Path.Combine(Path.GetTempPath(), $"alkaros-tls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tlsCacheRoot);
        var previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tlsCacheRoot);
        try
        {
            await using var app = DualScreenApplication.Build(new DualScreenOptions(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                _webRoot,
                "https://127.0.0.1:0",
                TrustedProxies: null,
                TrustedNetworks: null,
                AllowInsecureLoopbackDevelopment: false,
                TlsCertificatePath: null,
                TlsCertificateKeyPath: null,
                SelfSignedTlsHost: "pos.lan"));
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Assert.StartsWith("https://", address, StringComparison.Ordinal);

            System.Security.Cryptography.X509Certificates.X509Certificate2? presented = null;
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                {
                    presented = certificate is null
                        ? null
                        : new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
                    return true;
                },
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };

            // No X-Forwarded-Proto header: the request is genuinely HTTPS, so the
            // HTTPS_REQUIRED gate must not fire and the static shell is served.
            using var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            Assert.NotNull(presented);
            var rawSan = presented!.Extensions
                .Single(extension => extension.Oid?.Value == "2.5.29.17")
                .RawData;
            var subjectAlternativeNames =
                new System.Security.Cryptography.X509Certificates.X509SubjectAlternativeNameExtension(rawSan)
                    .EnumerateDnsNames()
                    .ToArray();
            Assert.Contains("pos.lan", subjectAlternativeNames);
            Assert.Contains("localhost", subjectAlternativeNames);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(tlsCacheRoot, recursive: true);
        }
    }

    [Fact]
    public void SelfSignedCertificateForLocalhostDoesNotDuplicateSubjectAlternativeNames()
    {
        var tlsCacheRoot = Path.Combine(Path.GetTempPath(), $"alkaros-tls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tlsCacheRoot);
        try
        {
            var certificate = DualScreenTls.Resolve(
                new DualScreenOptions(
                    "Host=localhost;Database=unused;Username=unused;Password=unused",
                    _webRoot,
                    "https://0.0.0.0:5443",
                    SelfSignedTlsHost: "localhost"),
                tlsCacheRoot)!;

            using (certificate)
            {
                var rawSan = certificate.Extensions
                    .Single(extension => extension.Oid?.Value == "2.5.29.17")
                    .RawData;
                var dnsNames = new System.Security.Cryptography.X509Certificates
                    .X509SubjectAlternativeNameExtension(rawSan)
                    .EnumerateDnsNames()
                    .ToArray();

                Assert.Equal("localhost", Assert.Single(dnsNames));
            }
        }
        finally
        {
            Directory.Delete(tlsCacheRoot, recursive: true);
        }
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<HttpResponseMessage> SendPairingRequestAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer-displays/pairing-requests")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLog> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<CapturedLog> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Enqueue(new CapturedLog(logLevel, eventId, formatter(state, exception), exception));
            }
        }
    }

    private sealed record CapturedLog(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}

[Collection("Host database password environment")]
public sealed class DualScreenAuthorizationHttpTests : IAsyncLifetime
{
    private const string Password = "Test-password-42";
    private const string KitchenStationId = "kitchen-test";
    private readonly TestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-http-{Guid.NewGuid():N}");
    private readonly string? _originalKitchenStation = Environment.GetEnvironmentVariable(
        DualScreenApplication.KitchenStationEnvironmentVariable);
    private NpgsqlDataSource? _dataSource;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "index.html"), "<!doctype html>");
        Environment.SetEnvironmentVariable(
            DualScreenApplication.KitchenStationEnvironmentVariable,
            KitchenStationId);
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
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        Environment.SetEnvironmentVariable(
            DualScreenApplication.KitchenStationEnvironmentVariable,
            _originalKitchenStation);
        await _database.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task RuntimeConfigurationRequiresTheBoundTerminalAndReturnsTheKitchenStationAndReservationStationFlag()
    {
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var anonymous = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/runtime-configuration",
            "198.51.100.42",
            "missing=cookie");
        using var anonymousResponse = await client.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var cashierCookie = await LoginAsync(client, terminalId, "198.51.100.42");
        using var configured = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/runtime-configuration",
            "198.51.100.42",
            cashierCookie);
        using var configuredResponse = await client.SendAsync(configured);
        Assert.Equal(HttpStatusCode.OK, configuredResponse.StatusCode);
        using var document = JsonDocument.Parse(await configuredResponse.Content.ReadAsStringAsync());
        var properties = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        // V1-SET-004: garsonFeatures joined kitchenStationId/reservationStationEnabled.
        Assert.Equal(3, properties.Count);
        Assert.Equal(KitchenStationId, properties["kitchenStationId"].GetString());
        // V1-SET-003: off by default — a fresh test database never turned it on.
        Assert.False(properties["reservationStationEnabled"].GetBoolean());
        // V1-SET-004: every Garson feature is on by default — a fresh test
        // database never turned any of them off.
        var garsonFeatures = properties["garsonFeatures"].EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal(9, garsonFeatures.Count);
        Assert.All(garsonFeatures.Values, value => Assert.True(value.GetBoolean()));

        var settings = app.Services.GetRequiredService<ISettingsService>();
        var record = await settings.GetRecordAsync(ReservationStationSetting.Key);
        await settings.SetValueAsync(ReservationStationSetting.Key, true, record!.RowVersion);
        using var afterToggle = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/runtime-configuration",
            "198.51.100.42",
            cashierCookie);
        using var afterToggleResponse = await client.SendAsync(afterToggle);
        using var afterToggleDoc = JsonDocument.Parse(await afterToggleResponse.Content.ReadAsStringAsync());
        Assert.True(afterToggleDoc.RootElement.GetProperty("reservationStationEnabled").GetBoolean());

        Environment.SetEnvironmentVariable(
            DualScreenApplication.KitchenStationEnvironmentVariable,
            null);
        try
        {
            using var missingConfiguration = CreateForwardedRequest(
                HttpMethod.Get,
                $"/api/v1/terminals/{terminalId:D}/runtime-configuration",
                "198.51.100.42",
                cashierCookie);
            using var missingConfigurationResponse = await client.SendAsync(missingConfiguration);
            Assert.Equal(HttpStatusCode.InternalServerError, missingConfigurationResponse.StatusCode);
            var body = await missingConfigurationResponse.Content.ReadAsStringAsync();
            Assert.Contains("INTERNAL_ERROR", body, StringComparison.Ordinal);
            Assert.DoesNotContain(
                DualScreenApplication.KitchenStationEnvironmentVariable,
                body,
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DualScreenApplication.KitchenStationEnvironmentVariable,
                KitchenStationId);
        }

        using var wrongTerminal = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{Guid.NewGuid():D}/runtime-configuration",
            "198.51.100.42",
            cashierCookie);
        using var wrongTerminalResponse = await client.SendAsync(wrongTerminal);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTerminalResponse.StatusCode);

        Environment.SetEnvironmentVariable(
            DualScreenApplication.CustomerDisplayOriginUrlEnvironmentVariable,
            "https://display.pos.local:8443");
        try
        {
            using var withDisplay = CreateForwardedRequest(
                HttpMethod.Get,
                $"/api/v1/terminals/{terminalId:D}/runtime-configuration",
                "198.51.100.42",
                cashierCookie);
            using var withDisplayResponse = await client.SendAsync(withDisplay);
            Assert.Equal(HttpStatusCode.OK, withDisplayResponse.StatusCode);
            using var withDisplayDoc = JsonDocument.Parse(await withDisplayResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                "https://display.pos.local:8443",
                withDisplayDoc.RootElement.GetProperty("customerDisplayUrl").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DualScreenApplication.CustomerDisplayOriginUrlEnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task LoginCookieAuthorizationDenialDisplayIsolationAndRevocationAreEnforcedOverHttp()
    {
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent(new { terminalId, username = "cashier", password = Password }),
        };
        AddTrustedForwarding(login, "198.51.100.30");
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        // V1-RMD-175: found by the 2026-09-10 Garson audit — /auth/login's
        // own response never carried the signed-in user's role at all, so
        // the waiter client's #userRole element had nothing to read.
        // Pinned at the wire-format level here: the property must exist
        // (this seeded user holds no role, so its value is legitimately
        // null - what matters is that the client always gets a roleName
        // key to read, not always a non-null one).
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginBody.TryGetProperty("roleName", out _));
        var setCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(DualScreenApplication.CashierCookieName + "=", StringComparison.Ordinal));
        Assert.Contains("; secure", setCookie, StringComparison.OrdinalIgnoreCase);
        var cashierCookie = setCookie.Split(';', 2)[0];

        using var denied = CreateForwardedRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/orders",
            "198.51.100.30",
            cashierCookie);
        using var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM identity.denial_events WHERE user_id = @user_id;",
            ("user_id", userId)));

        await ExecuteAsync(
            "UPDATE identity.device_sessions SET revoked_at = now() WHERE user_id = @user_id;",
            ("user_id", userId));
        using var revoked = CreateForwardedRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/orders",
            "198.51.100.30",
            cashierCookie);
        using var revokedResponse = await client.SendAsync(revoked);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);

        var (displayToken, displayHash) = DualScreenToken.Create("test-display:");
        await ExecuteAsync(
            """
            INSERT INTO customer_display.display_sessions
                (session_id, display_id, terminal_id, token_hash, created_at, expires_at, revoked_at, last_seen_at)
            VALUES (@session_id, @display_id, @terminal_id, @token_hash, now(), now() + interval '1 hour', NULL, now());
            """,
            ("session_id", Guid.NewGuid()),
            ("display_id", Guid.NewGuid()),
            ("terminal_id", terminalId),
            ("token_hash", displayHash));
        using var displayMutation = CreateForwardedRequest(
            HttpMethod.Post,
            $"/api/v1/terminals/{terminalId:D}/orders",
            "198.51.100.31",
            $"{DualScreenApplication.DisplayCookieName}={displayToken}");
        using var displayResponse = await client.SendAsync(displayMutation);
        Assert.Equal(HttpStatusCode.Forbidden, displayResponse.StatusCode);
    }

    [Fact]
    public async Task LoginAndSessionBothReturnTheSignedInUsersRoleName()
    {
        // V1-RMD-175: found by the 2026-09-10 Garson audit — the waiter
        // client's #userRole element had nothing to read because neither
        // /auth/login nor /auth/session ever carried a role name. Unlike
        // the isolation test above (whose seeded user legitimately has no
        // role), this user is given a real, named role so the assertions
        // below prove an actual name round-trips, not just that the key
        // exists.
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await SeedUserRoleAsync(userId, "Vardiya Amiri");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent(new { terminalId, username = "cashier", password = Password }),
        };
        AddTrustedForwarding(login, "198.51.100.32");
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vardiya Amiri", loginBody.GetProperty("roleName").GetString());
        var cashierCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(DualScreenApplication.CashierCookieName + "=", StringComparison.Ordinal))
            .Split(';', 2)[0];

        using var session = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/auth/session?terminalId={terminalId:D}",
            "198.51.100.32",
            cashierCookie);
        using var sessionResponse = await client.SendAsync(session);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var sessionBody = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vardiya Amiri", sessionBody.GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task ASupervisorLoginReachesTheGrantDecisionSurfaceButNotManagerOnlyAreas()
    {
        // V1-RMD-403 (V1-RMD-399 H-03): a floor supervisor holds reports.view but never catalog.manage; login used
        // to hand them only the cashier cookie, so the grant-decision surface (model §4 step 3) answered 401.
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        await SeedUserRoleWithPermissionsAsync(userId, "Şef Garson", "reports.view");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent(new { terminalId, username = "cashier", password = Password }),
        };
        AddTrustedForwarding(login, "198.51.100.41");
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var cookies = loginResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';', 2)[0]).ToList();
        var managementCookie = Assert.Single(cookies, value => value.StartsWith("alkaros.manager=", StringComparison.Ordinal));
        var cashierCookie = Assert.Single(cookies, value => value.StartsWith(DualScreenApplication.CashierCookieName + "=", StringComparison.Ordinal));

        using var pending = CreateForwardedRequest(
            HttpMethod.Get, "/api/v1/management/authorization/pending-grants", "198.51.100.41", managementCookie);
        using var pendingResponse = await client.SendAsync(pending);
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);

        using var catalog = CreateForwardedRequest(
            HttpMethod.Get, "/api/v1/management/catalog/products", "198.51.100.41", managementCookie);
        using var catalogResponse = await client.SendAsync(catalog);
        Assert.Equal(HttpStatusCode.Unauthorized, catalogResponse.StatusCode);

        using var logout = CreateForwardedRequest(
            HttpMethod.Post, $"/api/v1/auth/logout?terminalId={terminalId:D}", "198.51.100.41", cashierCookie);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var afterLogout = CreateForwardedRequest(
            HttpMethod.Get, "/api/v1/management/authorization/pending-grants", "198.51.100.41", managementCookie);
        using var afterLogoutResponse = await client.SendAsync(afterLogout);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task CatalogHttpContractKeepsLegacyArrayAndProvidesFilteredStableBoundedContinuation()
    {
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        var catalog = await SeedCatalogAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cashierCookie = await LoginAsync(client, terminalId, "198.51.100.40");

        using var defaultRequest = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog",
            "198.51.100.40",
            cashierCookie);
        using var defaultResponse = await client.SendAsync(defaultRequest);
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal("1000", defaultResponse.Headers.GetValues("X-Catalog-Limit").Single());
        Assert.False(defaultResponse.Headers.Contains("X-Next-Cursor"));
        var legacyArray = await defaultResponse.Content.ReadFromJsonAsync<CatalogProductDto[]>();
        Assert.Equal(4, legacyArray!.Length);

        using var filteredRequest = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog?category=CAT-A&limit=2",
            "198.51.100.40",
            cashierCookie);
        using var filteredResponse = await client.SendAsync(filteredRequest);
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var firstPage = await filteredResponse.Content.ReadFromJsonAsync<CatalogProductDto[]>();
        Assert.Equal(catalog.CategoryA.Take(2), firstPage!.Select(product => product.ProductId));
        var cursor = filteredResponse.Headers.GetValues("X-Next-Cursor").Single();

        await ExecuteAsync(
            """
            INSERT INTO catalog.products (
                product_id, sku, name, category_id, tax_profile_id, product_type,
                stock_mode, active, display_order, current_price)
            VALUES (@product_id, 'CAT-A-000', 'Inserted before cursor', @category_id, NULL, 1,
                    1, true, -1, 10.00);
            """,
            ("product_id", Guid.NewGuid()),
            ("category_id", catalog.CategoryAId));

        using var continuationRequest = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog?category=CAT-A&limit=2&cursor={Uri.EscapeDataString(cursor)}",
            "198.51.100.40",
            cashierCookie);
        using var continuationResponse = await client.SendAsync(continuationRequest);
        Assert.Equal(HttpStatusCode.OK, continuationResponse.StatusCode);
        var secondPage = await continuationResponse.Content.ReadFromJsonAsync<CatalogProductDto[]>();
        Assert.Equal([catalog.CategoryA[2]], secondPage!.Select(product => product.ProductId));
        Assert.False(continuationResponse.Headers.Contains("X-Next-Cursor"));

        using var invalidLimitRequest = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog?limit=1001",
            "198.51.100.40",
            cashierCookie);
        using var invalidLimitResponse = await client.SendAsync(invalidLimitRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimitResponse.StatusCode);
        var error = await invalidLimitResponse.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.Equal("VALIDATION_FAILED", error!.Error.Code);

        using var mismatchedCursorRequest = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog?category=CAT-B&limit=2&cursor={Uri.EscapeDataString(cursor)}",
            "198.51.100.40",
            cashierCookie);
        using var mismatchedCursorResponse = await client.SendAsync(mismatchedCursorRequest);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedCursorResponse.StatusCode);
    }

    [Fact]
    public async Task CatalogReportsAProductsModifierGroupsAndHidesInactiveOnes()
    {
        // V1-RMD-148: the catalog said nothing about options, so a client had
        // no way to offer "extra rice" even though the order path (V1-RMD-147)
        // would have accepted it. What is listed here must be exactly what the
        // order path accepts, so inactive modifiers and groups stay hidden.
        var userId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        await SeedUserAsync(userId);
        var catalog = await SeedCatalogAsync();
        var productWithOptions = catalog.CategoryA[0];
        var productWithout = catalog.CategoryA[1];

        var liveGroup = Guid.NewGuid();
        var retiredGroup = Guid.NewGuid();
        var liveModifier = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@live_group, 'RMD148-EKSTRA', 'Ekstralar', 2, 0, 3, true),
                   (@retired_group, 'RMD148-ESKI', 'Kaldırılmış grup', 1, 1, 1, false);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@live_modifier, @live_group, 'RMD148-PILAV', 'Ekstra pilav', 120.00, true),
                   (@retired_modifier, @live_group, 'RMD148-KALDIRILDI', 'Kaldırılmış eklenti', 40.00, false),
                   (@hidden_modifier, @retired_group, 'RMD148-GIZLI', 'Gizli eklenti', 10.00, true);
            INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
            VALUES (@pmg_live, @product_id, @live_group),
                   (@pmg_retired, @product_id, @retired_group);
            """,
            ("live_group", liveGroup),
            ("retired_group", retiredGroup),
            ("live_modifier", liveModifier),
            ("retired_modifier", Guid.NewGuid()),
            ("hidden_modifier", Guid.NewGuid()),
            ("pmg_live", Guid.NewGuid()),
            ("pmg_retired", Guid.NewGuid()),
            ("product_id", productWithOptions));

        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cashierCookie = await LoginAsync(client, terminalId, "198.51.100.41");

        using var request = CreateForwardedRequest(
            HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/catalog",
            "198.51.100.41",
            cashierCookie);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<CatalogProductDto[]>();
        var withOptions = products!.Single(p => p.ProductId == productWithOptions);

        var group = Assert.Single(withOptions.ModifierGroups!);
        Assert.Equal(liveGroup, group.ModifierGroupId);
        Assert.Equal("Ekstralar", group.Name);
        Assert.Equal("Multiple", group.SelectionType);
        Assert.Equal(0, group.MinSelections);
        Assert.Equal(3, group.MaxSelections);

        var modifier = Assert.Single(group.Modifiers);
        Assert.Equal(liveModifier, modifier.ModifierId);
        Assert.Equal("Ekstra pilav", modifier.Name);
        Assert.Equal(120.00m, modifier.PriceDelta);

        // A product nobody assigned a group to reports nothing at all.
        Assert.Null(products!.Single(p => p.ProductId == productWithout).ModifierGroups);
    }

    private static async Task<string> LoginAsync(HttpClient client, Guid terminalId, string forwardedFor)
    {
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent(new { terminalId, username = "cashier", password = Password }),
        };
        AddTrustedForwarding(login, forwardedFor);
        using var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(DualScreenApplication.CashierCookieName + "=", StringComparison.Ordinal))
            .Split(';', 2)[0];
    }

    private async Task<CatalogSeed> SeedCatalogAsync()
    {
        var categoryAId = Guid.NewGuid();
        var categoryBId = Guid.NewGuid();
        var categoryA = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var categoryB = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.categories (category_id, code, name, sort_order, active)
            VALUES (@category_a_id, 'CAT-A', 'Category A', 1, true),
                   (@category_b_id, 'CAT-B', 'Category B', 2, true);

            INSERT INTO catalog.products (
                product_id, sku, name, category_id, tax_profile_id, product_type,
                stock_mode, active, display_order, current_price)
            VALUES (@product_a1, 'CAT-A-001', 'Alpha', @category_a_id, NULL, 1, 1, true, 1, 10.00),
                   (@product_a2, 'CAT-A-002', 'Beta', @category_a_id, NULL, 1, 1, true, 2, 20.00),
                   (@product_a3, 'CAT-A-003', 'Gamma', @category_a_id, NULL, 1, 1, true, 3, 30.00),
                   (@product_b1, 'CAT-B-001', 'Delta', @category_b_id, NULL, 1, 1, true, 1, 40.00);
            """,
            ("category_a_id", categoryAId),
            ("category_b_id", categoryBId),
            ("product_a1", categoryA[0]),
            ("product_a2", categoryA[1]),
            ("product_a3", categoryA[2]),
            ("product_b1", categoryB));
        return new CatalogSeed(categoryAId, categoryA);
    }

    private async Task SeedUserAsync(Guid userId)
    {
        var passwordHash = new PasswordHasher(10_000).Hash(Password);
        await ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, 'cashier', @password_hash, 'Cashier', true);
            """,
            ("user_id", userId),
            ("password_hash", passwordHash));
    }

    // V1-RMD-175: assigns a real, named role to an already-seeded user so
    // tests can assert on the roleName that /auth/login and /auth/session
    // now return — SeedUserAsync alone leaves the user role-less, which is
    // fine for the tests that only need a valid session, but useless for
    // proving the client actually receives a non-null role display name.
    private async Task SeedUserRoleAsync(Guid userId, string roleName)
    {
        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, @role_name);
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@user_role_id, @user_id, @role_id);
            """,
            ("role_id", roleId),
            ("role_code", "rmd175-role-" + roleId.ToString("N")[..8]),
            ("role_name", roleName),
            ("user_role_id", Guid.NewGuid()),
            ("user_id", userId));
    }

    // V1-RMD-403: a named role that also holds real permission codes, so login's capability-driven
    // management-session choice can be exercised.
    private async Task SeedUserRoleWithPermissionsAsync(Guid userId, string roleName, params string[] permissionCodes)
    {
        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, @role_name);
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@user_role_id, @user_id, @role_id);
            """,
            ("role_id", roleId),
            ("role_code", "rmd403-role-" + roleId.ToString("N")[..8]),
            ("role_name", roleName),
            ("user_role_id", Guid.NewGuid()),
            ("user_id", userId));
        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            CreateConnectionString(),
            _webRoot,
            "http://127.0.0.1:0",
            [IPAddress.Loopback]));
        await app.StartAsync();
        return app;
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

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private static StringContent JsonContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static HttpRequestMessage CreateForwardedRequest(
        HttpMethod method,
        string path,
        string forwardedFor,
        string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        AddTrustedForwarding(request, forwardedFor);
        return request;
    }

    private static void AddTrustedForwarding(HttpRequestMessage request, string forwardedFor)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
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

    private sealed record CatalogSeed(Guid CategoryAId, IReadOnlyList<Guid> CategoryA);
}

/// <summary>
/// --api-only mode: the reverse proxy serves the static bundles and owns the
/// customer-display origin split via a trusted header (deep-analysis finding
/// B-4). These tests exercise the origin-isolation middleware alone; no
/// database is required because the middleware runs before routing.
/// </summary>
[Collection("Host database password environment")]
public sealed class DualScreenApiOnlyOriginTests : IDisposable
{
    private const string OriginHeader = "X-Alkaros-Origin";
    private const string DisplayRoute = "/api/v1/customer-displays/00000000-0000-0000-0000-000000000000/snapshot";
    private readonly string? _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");

    public DualScreenApiOnlyOriginTests()
        => Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");

    public void Dispose()
        => Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);

    [Fact]
    public void ApiOnlyBuildsWithoutAWebRoot()
    {
        var options = DualScreenOptions.Parse([
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--urls", "http://0.0.0.0:5080",
            "--api-only",
            "--trusted-network", "172.16.0.0/12",
            "--customer-display-origin-header", OriginHeader,
        ]);

        Assert.True(options.ApiOnly);
        Assert.Equal(string.Empty, options.WebRoot);
        Assert.Equal(OriginHeader, options.CustomerDisplayOriginHeader);
    }

    [Fact]
    public async Task MainOriginRefusesDisplayOnlyRoutesWithoutTheHeader()
    {
        await using var app = await StartApiOnlyAsync([IPAddress.Loopback]);
        using var client = CreateClient(app);

        using var request = Forwarded(HttpMethod.Get, DisplayRoute, "198.51.100.5");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "yalnızca ayrı origin",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisplayOriginRefusesNonDisplayApiEvenWithTheHeader()
    {
        await using var app = await StartApiOnlyAsync([IPAddress.Loopback]);
        using var client = CreateClient(app);

        using var request = Forwarded(HttpMethod.Get, "/api/v1/auth/session", "198.51.100.6");
        request.Headers.TryAddWithoutValidation(OriginHeader, "display");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "müşteri ekranı origin",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisplayOriginAdmitsDisplayApiWithTheHeader()
    {
        await using var app = await StartApiOnlyAsync([IPAddress.Loopback]);
        using var client = CreateClient(app);

        using var request = Forwarded(HttpMethod.Get, "/api/v1/customer-displays/not-a-guid", "198.51.100.7");
        request.Headers.TryAddWithoutValidation(OriginHeader, "display");
        using var response = await client.SendAsync(request);

        // The isolation middleware let it through; no route matched the bad id,
        // so the API catch-all - not the origin guard - produced the 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("İstenen API adresi bulunamadı", body, StringComparison.Ordinal);
        Assert.DoesNotContain("yalnızca ayrı origin", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUntrustedPeerCannotPresentAsTheDisplayOrigin()
    {
        // Loopback is not in the trusted set, so the proxy header - like
        // X-Forwarded-Proto - carries no authority: the request is treated as
        // plain HTTP and stopped at the scheme gate before it can claim an origin.
        await using var app = await StartApiOnlyAsync([IPAddress.Parse("192.0.2.10")]);
        using var client = CreateClient(app);

        using var request = Forwarded(HttpMethod.Get, DisplayRoute, "198.51.100.8");
        request.Headers.TryAddWithoutValidation(OriginHeader, "display");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "HTTPS_REQUIRED",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private static async Task<WebApplication> StartApiOnlyAsync(IReadOnlyList<IPAddress> trustedProxies)
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            "Host=localhost;Database=unused;Username=unused;Password=unused",
            WebRoot: string.Empty,
            "http://127.0.0.1:0",
            trustedProxies,
            ApiOnly: true,
            CustomerDisplayOriginHeader: OriginHeader));
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Forwarded(HttpMethod method, string path, string forwardedFor)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
