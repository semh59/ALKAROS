using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.SecurityAdministration.Tests;

/// <summary>
/// V1-RMD-266: AccountRecoveryService (V15-SEC-002) had no caller in the running
/// host. Proves a real HTTP client can sign a user out everywhere and clear a
/// lockout, that only a MANAGER session holding security.manage may do it, and
/// that each action lands in the durable audit trail with the acting manager.
/// </summary>
[Collection("Security administration PostgreSQL HTTP")]
public sealed class SecurityAdministrationHttpTests : IAsyncLifetime
{
    private readonly SecurityAdministrationTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, composition.Services);
        builder.Services.AddSecurityAdministrationExperience();

        _application = builder.Build();
        _application.MapSecurityAdministrationApi();
        await _application.StartAsync();
        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static string RevokePath(Guid userId) => $"/api/v1/management/security/users/{userId:D}/revoke-sessions";

    private static string UnlockPath(Guid userId) => $"/api/v1/management/security/users/{userId:D}/force-unlock";

    [Fact]
    public async Task OnlyAManagerSessionHoldingSecurityManageMayUseTheSurface()
    {
        var target = SecurityAdministrationTestDatabase.TargetUserId;

        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(RevokePath(target), null)).StatusCode);

        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync(RevokePath(target), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync(UnlockPath(target), null)).StatusCode);

        // The user behind this supervisor-device session holds security.manage, but the
        // surface is manager-only: a supervisor must not be able to lock managers out.
        using var supervisorDevice = CreateClient(SecurityAdministrationTestDatabase.SupervisorDeviceToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await supervisorDevice.PostAsync(RevokePath(target), null)).StatusCode);

        Assert.Equal(2, await _database.ActiveSessionCountAsync(target));
        Assert.Equal(1, await _database.IsLockedAsync(target));
    }

    [Fact]
    public async Task AManagerSignsAUserOutEverywhereAndTheActionIsAudited()
    {
        var target = SecurityAdministrationTestDatabase.TargetUserId;
        var manager = SecurityAdministrationTestDatabase.ManagerUserId;
        using var client = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var response = await client.PostAsync(RevokePath(target), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("revokedSessions").GetInt32());
        Assert.Equal(0, await _database.ActiveSessionCountAsync(target));
        Assert.Equal(1, await _database.AuditCountAsync("security.all-sessions-revoked", target, manager));
    }

    [Fact]
    public async Task AManagerClearsALockoutBeforeItExpiresAndAnUnknownUserIsNotFound()
    {
        var target = SecurityAdministrationTestDatabase.TargetUserId;
        var manager = SecurityAdministrationTestDatabase.ManagerUserId;
        using var client = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var unlocked = await client.PostAsync(UnlockPath(target), null);

        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
        Assert.Equal(0, await _database.IsLockedAsync(target));
        Assert.Equal(0, await _database.LockedAttemptsAsync(target));
        Assert.Equal(1, await _database.AuditCountAsync("security.account-force-unlocked", target, manager));

        using var missing = await client.PostAsync(UnlockPath(Guid.NewGuid()), null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private HttpClient CreateClient(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{SecurityAdministrationEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }
}

[CollectionDefinition("Security administration PostgreSQL HTTP", DisableParallelization = true)]
public sealed class SecurityAdministrationPostgresqlDefinition;
