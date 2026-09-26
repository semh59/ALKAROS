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

    private readonly string _rotationDirectory = Path.Combine(Path.GetTempPath(), $"alkaros-rmd269-{Guid.NewGuid():N}");
    private readonly string _backupSourceDirectory = Path.Combine(Path.GetTempPath(), $"alkaros-rmd270-src-{Guid.NewGuid():N}");
    private readonly string _backupTargetDirectory = Path.Combine(Path.GetTempPath(), $"alkaros-rmd270-dst-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_rotationDirectory);
        Directory.CreateDirectory(_backupSourceDirectory);
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_ROTATION_DIR", _rotationDirectory);
        Environment.SetEnvironmentVariable("ALKAROS_OFFSITE_BACKUP_DIR", _backupTargetDirectory);
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton(new HostDatabaseConnection(new Npgsql.NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString) { Password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD") }.ConnectionString));
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
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_ROTATION_DIR", null);
        Environment.SetEnvironmentVariable("ALKAROS_OFFSITE_BACKUP_DIR", null);
        Environment.SetEnvironmentVariable("ALKAROS_BACKUP_DIR", null);
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V1", null);
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V2", null);
        foreach (var directory in new[] { _rotationDirectory, _backupSourceDirectory, _backupTargetDirectory })
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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

    private const string BundlePath = "/api/v1/management/security/diagnostic-bundle";
    private static readonly string[] OneCorrelation = ["c"];
    private static readonly string[] BundleCorrelation = ["rmd267-corr"];

    [Fact]
    public async Task ADiagnosticBundleIsRedactedBoundedAndItsOwnGenerationIsAudited()
    {
        var store = _application!.Services.GetRequiredService<ALKAROS.Audit.EventStore.IAuditEventStore>();
        await store.AppendAsync(new ALKAROS.Audit.EventStore.AuditEvent(
            Guid.NewGuid(), "bill.discount.applied", "Bill", Guid.NewGuid(), "User", "rmd267-corr",
            afterStateJson: "{\"password\":\"hunter2-secret\",\"note\":\"visible\"}"));
        using var client = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var response = await client.PostAsJsonAsync(BundlePath, new
        {
            CorrelationIds = BundleCorrelation,
            WindowStart = DateTimeOffset.UtcNow.AddHours(-1),
            WindowEnd = DateTimeOffset.UtcNow.AddHours(1),
            Reason = "Destek incelemesi",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2-secret", raw);
        Assert.Contains("visible", raw);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(1, body.GetProperty("logEntries").GetArrayLength());
        Assert.Equal(
            SecurityAdministrationTestDatabase.ManagerUserId.ToString("D"),
            body.GetProperty("requestedByActorId").GetString());
    }

    [Fact]
    public async Task ABundleRequestWithoutCorrelationIdsAnOversizedWindowOrNoReasonIsRejected()
    {
        using var client = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        var now = DateTimeOffset.UtcNow;

        using var none = await client.PostAsJsonAsync(BundlePath, new { CorrelationIds = Array.Empty<string>(), WindowStart = now.AddHours(-1), WindowEnd = now, Reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal("NO_CORRELATION_IDS", (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());

        using var wide = await client.PostAsJsonAsync(BundlePath, new { CorrelationIds = OneCorrelation, WindowStart = now.AddDays(-31), WindowEnd = now, Reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, wide.StatusCode);
        Assert.Equal("TIME_WINDOW_TOO_LARGE", (await wide.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());

        using var noReason = await client.PostAsJsonAsync(BundlePath, new { CorrelationIds = OneCorrelation, WindowStart = now.AddHours(-1), WindowEnd = now, Reason = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    [Fact]
    public async Task ADiagnosticBundleNeedsAManagerSessionWithSecurityManage()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new { CorrelationIds = OneCorrelation, WindowStart = now.AddHours(-1), WindowEnd = now, Reason = "x" };

        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(BundlePath, request)).StatusCode);
        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsJsonAsync(BundlePath, request)).StatusCode);
    }

    private const string JobsPath = "/api/v1/management/security/maintenance/jobs";

    [Fact]
    public async Task MaintenanceJobsAreListedRunOnDemandAndRefuseUnknownNamesAndUnauthorizedCallers()
    {
        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(JobsPath)).StatusCode);
        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync($"{JobsPath}/retention-sweep/run", null)).StatusCode);

        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        var jobs = await manager.GetFromJsonAsync<JsonElement>(JobsPath);
        var retention = jobs.EnumerateArray().Single(job => job.GetProperty("name").GetString() == "retention-sweep");
        Assert.True(retention.GetProperty("enabled").GetBoolean());
        Assert.Equal("NeverRun", retention.GetProperty("lastStatus").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsync($"{JobsPath}/no-such-job/run", null)).StatusCode);
    }

    [Fact]
    public async Task TheRetentionSweepDisposesOnlyExpiredUnheldSubjectsAndIsIdempotent()
    {
        var (expired, held, fresh) = await _database.SeedRetentionSubjectsAsync();
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var first = await manager.PostAsync($"{JobsPath}/retention-sweep/run", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var status = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Succeeded", status.GetProperty("lastStatus").GetString());
        Assert.Contains("1 kayıt imha edildi", status.GetProperty("lastSummary").GetString());
        Assert.Equal(1, await _database.DisposedCountAsync(expired));
        Assert.Equal(0, await _database.DisposedCountAsync(held));
        Assert.Equal(0, await _database.DisposedCountAsync(fresh));
        Assert.Equal(1, await _database.SystemAuditCountAsync("RetentionSubjectDisposed", expired));

        using var second = await manager.PostAsync($"{JobsPath}/retention-sweep/run", null);
        var again = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("0 kayıt imha edildi", again.GetProperty("lastSummary").GetString());
        Assert.Equal(1, await _database.SystemAuditCountAsync("RetentionSubjectDisposed", expired));
    }

    private const string SecretsPath = "/api/v1/management/security/secrets";
    private const string RotationPath = SecretsPath + "/offsite-backup/rotation";
    private const string V1Value = "rmd269-key-material-one";
    private const string V2Value = "rmd269-key-material-two";

    [Fact]
    public async Task SecretRotationNeedsAManagerAndOnlyKnownSecretsAreManageable()
    {
        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(SecretsPath)).StatusCode);
        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.GetAsync(SecretsPath)).StatusCode);

        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"{SecretsPath}/not-a-known-secret/rotation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsync($"{SecretsPath}/../etc/rotation/initialize", null)).StatusCode);
    }

    [Fact]
    public async Task ARotationCannotActivateAVersionWhoseKeyMaterialIsNotInTheEnvironment()
    {
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var refused = await manager.PostAsync($"{RotationPath}/initialize", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("KEY_MATERIAL_MISSING", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("ALKAROS_SECRET_OFFSITE_BACKUP_V1", body.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync(RotationPath)).StatusCode);
    }

    [Fact]
    public async Task TheFullRotationLifecycleIsGuardedAuditedAndNeverEchoesKeyMaterial()
    {
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V1", V1Value);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        var aggregate = ALKAROS.Host.Experience.SecurityAdministration.SecretRotationAdministration.AggregateIdFor("offsite-backup");
        var everything = new System.Text.StringBuilder();

        async Task<JsonElement> Ok(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();
            everything.AppendLine(text);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(text).RootElement;
        }

        var initialized = await Ok(await manager.PostAsync($"{RotationPath}/initialize", null));
        Assert.Equal(1, initialized.GetProperty("activeVersion").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsync($"{RotationPath}/initialize", null)).StatusCode);

        // Version 2's material is not in the environment yet: refuse, state unchanged.
        using var early = await manager.PostAsJsonAsync($"{RotationPath}/rotate", new { OverlapHours = 24 });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V2", V2Value);
        var rotated = await Ok(await manager.PostAsJsonAsync($"{RotationPath}/rotate", new { OverlapHours = 24 }));
        Assert.Equal(2, rotated.GetProperty("activeVersion").GetInt32());
        Assert.Equal(1, rotated.GetProperty("overlapVersionCount").GetInt32());

        // The active version can never be revoked; an out-of-range overlap is refused.
        using var revokeActive = await manager.PostAsJsonAsync($"{RotationPath}/revoke", new { Version = 2 });
        Assert.Equal(HttpStatusCode.Conflict, revokeActive.StatusCode);
        using var badOverlap = await manager.PostAsJsonAsync($"{RotationPath}/rotate", new { OverlapHours = 100000 });
        Assert.Equal(HttpStatusCode.BadRequest, badOverlap.StatusCode);

        var rolledBack = await Ok(await manager.PostAsJsonAsync($"{RotationPath}/rollback", new { Version = 1 }));
        Assert.Equal(1, rolledBack.GetProperty("activeVersion").GetInt32());
        Assert.Equal(1, rolledBack.GetProperty("revokedVersionCount").GetInt32());

        var listed = await Ok(await manager.GetAsync(SecretsPath));
        Assert.Equal(1, listed.GetArrayLength());

        Assert.DoesNotContain(V1Value, everything.ToString());
        Assert.DoesNotContain(V2Value, everything.ToString());
        Assert.Equal(1, await _database.SystemAuditCountAsync("secret.rotation.initialize", aggregate));
        Assert.Equal(1, await _database.SystemAuditCountAsync("secret.rotation.rotate", aggregate));
        Assert.Equal(1, await _database.SystemAuditCountAsync("secret.rotation.rollback", aggregate));
        Assert.Equal(0, await _database.SystemAuditCountAsync("secret.rotation.revoke", aggregate));
    }

    private const string BackupJobRunPath = JobsPath + "/offsite-backup/run";
    private const string PlainMarker = "PGDMP-rmd270-plaintext-marker";

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private void WriteArtifact(string name, string content, string? overrideChecksum = null, bool sidecar = true)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(Path.Combine(_backupSourceDirectory, name), bytes);
        if (sidecar)
            File.WriteAllText(Path.Combine(_backupSourceDirectory, name + ".sha256"), $"{overrideChecksum ?? Sha256Hex(bytes)}  {name}\n");
    }

    [Fact]
    public async Task TheBackupJobReportsWhyItCannotRunInsteadOfSilentlySucceeding()
    {
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        var noSource = await manager.PostAsync(BackupJobRunPath, null);
        var first = await noSource.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Skipped", first.GetProperty("lastStatus").GetString());
        Assert.Contains("ALKAROS_BACKUP_DIR", first.GetProperty("lastSummary").GetString());

        Environment.SetEnvironmentVariable("ALKAROS_BACKUP_DIR", _backupSourceDirectory);
        var noKey = await manager.PostAsync(BackupJobRunPath, null);
        var second = await noKey.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Skipped", second.GetProperty("lastStatus").GetString());
        Assert.Contains("offsite-backup", second.GetProperty("lastSummary").GetString());
    }

    [Fact]
    public async Task TheBackupJobEncryptsAndShipsVerifiedArtifactsOnceRecordsReceiptsAndTheRpoReportIsHonest()
    {
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V1", "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        Environment.SetEnvironmentVariable("ALKAROS_BACKUP_DIR", _backupSourceDirectory);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"{RotationPath}/initialize", null)).StatusCode);

        WriteArtifact("alkaros_a_20260924T010000Z.dump", PlainMarker + " good");
        WriteArtifact("alkaros_b_20260924T020000Z.dump", PlainMarker + " corrupt", overrideChecksum: new string('0', 64));
        WriteArtifact("alkaros_c_20260924T030000Z.dump", PlainMarker + " no sidecar", sidecar: false);

        var run1 = await (await manager.PostAsync(BackupJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Failed", run1.GetProperty("lastStatus").GetString());
        Assert.Contains("1 yedek yüklendi", run1.GetProperty("lastSummary").GetString());
        Assert.Contains("alkaros_b_20260924T020000Z.dump", run1.GetProperty("lastSummary").GetString());

        // Only the verified artifact reached the target, and never as plaintext.
        var shipped = Directory.GetFiles(_backupTargetDirectory);
        Assert.Single(shipped);
        Assert.EndsWith("alkaros_a_20260924T010000Z.dump.enc", shipped[0]);
        Assert.DoesNotContain(PlainMarker, System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(shipped[0])));

        // The RPO report shows the truth: a dump only proves the 24 h class.
        var rpo = await manager.GetFromJsonAsync<JsonElement>("/api/v1/management/security/backup/rpo");
        bool Meets(string dataClass) => rpo.EnumerateArray()
            .Single(item => item.GetProperty("dataClass").GetString() == dataClass).GetProperty("meetsTarget").GetBoolean();
        Assert.True(Meets("Settings"));
        Assert.False(Meets("Fiscal"));
        Assert.False(Meets("OrdersInventory"));

        // Fixing the checksum ships the second one; the first is never uploaded twice.
        WriteArtifact("alkaros_b_20260924T020000Z.dump", PlainMarker + " corrupt");
        var run2 = await (await manager.PostAsync(BackupJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Succeeded", run2.GetProperty("lastStatus").GetString());
        Assert.Contains("1 yedek yüklendi, 1 yedek zaten uzakta", run2.GetProperty("lastSummary").GetString());
        var run3 = await (await manager.PostAsync(BackupJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("0 yedek yüklendi, 2 yedek zaten uzakta", run3.GetProperty("lastSummary").GetString());
        Assert.Equal(2, Directory.GetFiles(_backupTargetDirectory).Length);
    }

    private const string RestoreJobRunPath = JobsPath + "/restore-verification/run";

    /// <summary>Produces a REAL pg_dump custom-format archive of the test database, exactly what backup.sh ships.</summary>
    private void WriteRealDump(string name)
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString);
        var start = new System.Diagnostics.ProcessStartInfo("pg_dump")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "--format=custom", "--no-owner", "--no-privileges",
            $"--host={Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost"}",
            $"--port={Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT") ?? "5432"}",
            $"--username={Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres"}",
            $"--dbname={connection.Database}",
            $"--file={Path.Combine(_backupSourceDirectory, name)}",
        })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["PGPASSWORD"] = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD") ?? string.Empty;
        using var process = System.Diagnostics.Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"pg_dump failed: {error}");

        var bytes = File.ReadAllBytes(Path.Combine(_backupSourceDirectory, name));
        File.WriteAllText(Path.Combine(_backupSourceDirectory, name + ".sha256"), $"{Sha256Hex(bytes)}  {name}\n");
    }

    [Fact]
    public async Task TheRestoreDrillRestoresARealShippedDumpIntoAThrowawayDatabaseAndRecordsTheAttempt()
    {
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V1", "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        Environment.SetEnvironmentVariable("ALKAROS_BACKUP_DIR", _backupSourceDirectory);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        // Before anything is initialized or shipped the drill says why it cannot run.
        var blocked = await (await manager.PostAsync(RestoreJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Skipped", blocked.GetProperty("lastStatus").GetString());

        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"{RotationPath}/initialize", null)).StatusCode);
        var nothingShipped = await (await manager.PostAsync(RestoreJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Skipped", nothingShipped.GetProperty("lastStatus").GetString());
        Assert.Contains("yedek yok", nothingShipped.GetProperty("lastSummary").GetString());

        WriteRealDump("alkaros_real_20260924T040000Z.dump");
        var shipped = await (await manager.PostAsync(BackupJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Succeeded", shipped.GetProperty("lastStatus").GetString());

        var drill = await (await manager.PostAsync(RestoreJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(drill.GetProperty("lastStatus").GetString() == "Succeeded", drill.GetProperty("lastSummary").GetString());
        Assert.Contains("2/2 kontrol geçti", drill.GetProperty("lastSummary").GetString());

        var attempts = await manager.GetFromJsonAsync<JsonElement>("/api/v1/management/security/backup/restore-attempts");
        var attempt = attempts.EnumerateArray().Single();
        Assert.True(attempt.GetProperty("succeeded").GetBoolean());
        Assert.True(attempt.GetProperty("withinRtoTarget").GetBoolean());
        Assert.Equal("alkaros_real_20260924T040000Z.dump", attempt.GetProperty("artifactId").GetString());
        // The throwaway database was dropped again.
        Assert.Equal(0, await _database.ScratchDatabaseCountAsync());
    }

    [Fact]
    public async Task ATamperedOffsiteCopyFailsTheRestoreDrillAndTheFailureIsRecorded()
    {
        Environment.SetEnvironmentVariable("ALKAROS_SECRET_OFFSITE_BACKUP_V1", "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        Environment.SetEnvironmentVariable("ALKAROS_BACKUP_DIR", _backupSourceDirectory);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"{RotationPath}/initialize", null)).StatusCode);
        WriteArtifact("alkaros_t_20260924T050000Z.dump", "PGDMP-not-really-a-dump");
        Assert.Equal("Succeeded", (await (await manager.PostAsync(BackupJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lastStatus").GetString());

        var copy = Directory.GetFiles(_backupTargetDirectory).Single();
        var bytes = File.ReadAllBytes(copy);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(copy, bytes);

        var drill = await (await manager.PostAsync(RestoreJobRunPath, null)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Failed", drill.GetProperty("lastStatus").GetString());
        var attempts = await manager.GetFromJsonAsync<JsonElement>("/api/v1/management/security/backup/restore-attempts");
        var attempt = attempts.EnumerateArray().Single();
        Assert.False(attempt.GetProperty("succeeded").GetBoolean());
        Assert.Equal(0, await _database.ScratchDatabaseCountAsync());
    }

    private const string OrdersPath = "/api/v1/management/security/orders";

    [Fact]
    public async Task TheOrderBacklogToolsAreManagerOnly()
    {
        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{OrdersPath}/backlog")).StatusCode);
        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync($"{OrdersPath}/close-settled?dryRun=false", null)).StatusCode);
        using var supervisorDevice = CreateClient(SecurityAdministrationTestDatabase.SupervisorDeviceToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await supervisorDevice.GetAsync($"{OrdersPath}/backlog")).StatusCode);
    }

    [Fact]
    public async Task TheBacklogReportSeparatesProvablySettledOrdersFromThoseNeedingABusinessDecision()
    {
        await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Submitted, ALKAROS.Billing.BillFoundation.BillState.Paid);
        await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Submitted, null);
        await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Accepted, ALKAROS.Billing.BillFoundation.BillState.Open);
        await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Completed, ALKAROS.Billing.BillFoundation.BillState.Paid);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        var report = await manager.GetFromJsonAsync<JsonElement>($"{OrdersPath}/backlog");

        Assert.Equal(3, report.GetProperty("liveOrders").GetInt64());
        Assert.Equal(1, report.GetProperty("provablySettled").GetInt64());
        Assert.Equal(1, report.GetProperty("withoutBill").GetInt64());
        Assert.Equal(1, report.GetProperty("withOpenBill").GetInt64());
    }

    [Fact]
    public async Task ClosingSettledOrdersIsADryRunByDefaultAndTheRealRunTouchesOnlyProvablySettledOnes()
    {
        var (settled, _) = await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Submitted, ALKAROS.Billing.BillFoundation.BillState.Paid);
        var (noBill, _) = await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Submitted, null);
        var (openBill, _) = await _database.SeedOrderAsync(ALKAROS.Orders.OrderAggregate.OrderState.Accepted, ALKAROS.Billing.BillFoundation.BillState.Open);
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var dryResponse = await manager.PostAsync($"{OrdersPath}/close-settled", null);
        var dryText = await dryResponse.Content.ReadAsStringAsync();
        Assert.True(dryResponse.IsSuccessStatusCode, dryText);
        var dry = JsonDocument.Parse(dryText).RootElement;
        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, dry.GetProperty("eligible").GetInt32());
        Assert.Equal(0, dry.GetProperty("closed").GetInt32());
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(settled));

        using var realResponse = await manager.PostAsync($"{OrdersPath}/close-settled?dryRun=false", null);
        var realText = await realResponse.Content.ReadAsStringAsync();
        Assert.True(realResponse.IsSuccessStatusCode, realText);
        var real = JsonDocument.Parse(realText).RootElement;
        Assert.False(real.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, real.GetProperty("closed").GetInt32());
        Assert.Equal(0, real.GetProperty("failed").GetInt32());
        Assert.Equal("Completed", await _database.OrderStatusOfAsync(settled));
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(noBill));
        Assert.Equal("Accepted", await _database.OrderStatusOfAsync(openBill));
        Assert.Equal(1, await _database.SystemAuditCountAsync("orders.backfill.closed-settled", OrderBacklogAdministration.AuditAggregateId));

        var again = await (await manager.PostAsync($"{OrdersPath}/close-settled?dryRun=false", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, again.GetProperty("eligible").GetInt32());
    }

    private const string OutboxPath = "/api/v1/management/security/outbox";

    [Fact]
    public async Task TheOutboxDeadLetterToolsAreManagerOnly()
    {
        var deadLetterId = await _database.SeedDeadOutboxMessageAsync();

        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{OutboxPath}/dead-letters")).StatusCode);
        using var viewOnly = CreateClient(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.GetAsync($"{OutboxPath}/dead-letters")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await viewOnly.PostAsync($"{OutboxPath}/dead-letters/{deadLetterId:D}/requeue?dryRun=false", null)).StatusCode);
        using var supervisorDevice = CreateClient(SecurityAdministrationTestDatabase.SupervisorDeviceToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await supervisorDevice.GetAsync($"{OutboxPath}/dead-letters")).StatusCode);
    }

    [Fact]
    public async Task ARealDeadLetterIsListedWithItsAttemptCountAndLastError()
    {
        var deadLetterId = await _database.SeedDeadOutboxMessageAsync("orders.table-transfer.completed", "consumer threw: simulated permanent failure");
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        var list = await manager.GetFromJsonAsync<JsonElement>($"{OutboxPath}/dead-letters");

        Assert.Equal(1, list.GetProperty("total").GetInt64());
        var item = list.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(deadLetterId, item.GetProperty("id").GetGuid());
        Assert.Equal("orders.table-transfer.completed", item.GetProperty("eventType").GetString());
        Assert.Equal(3, item.GetProperty("attemptCount").GetInt32());
        Assert.Equal("consumer threw: simulated permanent failure", item.GetProperty("lastError").GetString());
    }

    [Fact]
    public async Task RequeuingADeadLetterIsADryRunByDefaultAndTheRealRunMakesItPendingAgain()
    {
        var deadLetterId = await _database.SeedDeadOutboxMessageAsync();
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var dryResponse = await manager.PostAsync($"{OutboxPath}/dead-letters/{deadLetterId:D}/requeue", null);
        var dryText = await dryResponse.Content.ReadAsStringAsync();
        Assert.True(dryResponse.IsSuccessStatusCode, dryText);
        var dry = JsonDocument.Parse(dryText).RootElement;
        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        Assert.False(dry.GetProperty("requeued").GetBoolean());
        Assert.Equal("dead", await _database.OutboxMessageStatusAsync(deadLetterId));

        using var realResponse = await manager.PostAsync($"{OutboxPath}/dead-letters/{deadLetterId:D}/requeue?dryRun=false", null);
        var realText = await realResponse.Content.ReadAsStringAsync();
        Assert.True(realResponse.IsSuccessStatusCode, realText);
        var real = JsonDocument.Parse(realText).RootElement;
        Assert.False(real.GetProperty("dryRun").GetBoolean());
        Assert.True(real.GetProperty("requeued").GetBoolean());
        Assert.Equal("pending", await _database.OutboxMessageStatusAsync(deadLetterId));
        Assert.Equal(1, await _database.SystemAuditCountAsync("outbox.dead-letter.requeued", OutboxAdministration.AuditAggregateId));

        // Already pending, not dead: a repeat is refused, not silently re-applied.
        using var repeat = await manager.PostAsync($"{OutboxPath}/dead-letters/{deadLetterId:D}/requeue?dryRun=false", null);
        Assert.Equal(HttpStatusCode.NotFound, repeat.StatusCode);
    }

    [Fact]
    public async Task RequeuingAnUnknownDeadLetterIsNotFound()
    {
        using var manager = CreateClient(SecurityAdministrationTestDatabase.ManagerToken);

        using var response = await manager.PostAsync($"{OutboxPath}/dead-letters/{Guid.NewGuid():D}/requeue?dryRun=false", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
