using ALKAROS.Settings.TypedSettings.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Settings.TypedSettings.Tests;

[Collection(nameof(SettingsTestFixtureDefinition))]
public sealed class PostgresSettingsRepositoryTests : IClassFixture<SettingsTestDatabase>, IAsyncLifetime
{
    private readonly SettingsTestDatabase _db;
    private readonly SettingValidator _validator;
    private readonly PostgresSettingsRepository _repository;
    private readonly SettingsService _service;
    private Guid _userId;

    public PostgresSettingsRepositoryTests(SettingsTestDatabase db)
    {
        _db = db;
        _validator = new SettingValidator();
        _repository = new PostgresSettingsRepository(_db.DataSource, _validator);
        _service = new SettingsService(_repository);
    }

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();

        const string insertUserSql = """
            INSERT INTO identity.users (user_id, username, display_name, password_hash, active, created_at, updated_at)
            VALUES (@id, @username, @display, 'hash', true, now(), now())
            ON CONFLICT (user_id) DO NOTHING;
            """;
        await using var cmd = new NpgsqlCommand(insertUserSql, connection);
        cmd.Parameters.AddWithValue("id", _userId);
        cmd.Parameters.AddWithValue("username", $"user_{_userId:N}"[..20]);
        cmd.Parameters.AddWithValue("display", "Settings Admin");
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterAndReadTypedSettingsSuccessfully()
    {
        var request = new RegisterSettingRequest(
            Key: "billing.service_charge_rate",
            Value: "0.10",
            DataType: SettingDataType.PreciseNumber,
            Scope: SettingScope.Module,
            ModuleOwner: "Billing",
            Description: "Default service charge rate",
            RequiresRestart: false,
            RegisteredBy: _userId,
            Reason: "Initial setup");

        var created = await _repository.RegisterSettingAsync(request);

        created.Should().NotBeNull();
        created.Key.Should().Be("billing.service_charge_rate");
        created.Value.Should().Be("0.10");
        created.DataType.Should().Be(SettingDataType.PreciseNumber);
        created.Scope.Should().Be(SettingScope.Module);
        created.ModuleOwner.Should().Be("Billing");
        created.Active.Should().BeTrue();
        created.RowVersion.Should().Be(1);

        // Read by key
        var fetched = await _repository.GetByKeyAsync("billing.service_charge_rate");
        fetched.Should().NotBeNull();
        fetched!.SettingId.Should().Be(created.SettingId);
        fetched.Value.Should().Be("0.10");

        // Verify history contains initial entry
        var history = await _repository.GetHistoryAsync(created.SettingId);
        history.Should().HaveCount(1);
        history[0].OldValue.Should().BeNull();
        history[0].NewValue.Should().Be("0.10");
        history[0].ChangedBy.Should().Be(_userId);
        history[0].Reason.Should().Be("Initial setup");
    }

    [Fact]
    public async Task UpdateSettingValueAppendsToHistoryAndIncrementsVersion()
    {
        var regRequest = new RegisterSettingRequest(
            Key: "table.idle_timeout",
            Value: "00:15:00",
            DataType: SettingDataType.Duration,
            Scope: SettingScope.Module,
            ModuleOwner: "Tables",
            RegisteredBy: _userId);

        var setting = await _repository.RegisterSettingAsync(regRequest);

        // Update value
        var updateRequest = new UpdateSettingRequest(
            Key: "table.idle_timeout",
            NewValue: "00:30:00",
            ExpectedRowVersion: 1,
            UpdatedBy: _userId,
            Reason: "Extended timeout for evening service");

        var updated = await _repository.UpdateSettingAsync(updateRequest);

        updated.Value.Should().Be("00:30:00");
        updated.RowVersion.Should().Be(2);

        // Verify History has 2 entries (descending order)
        var history = await _repository.GetHistoryAsync(setting.SettingId);
        history.Should().HaveCount(2);

        history[0].OldValue.Should().Be("00:15:00");
        history[0].NewValue.Should().Be("00:30:00");
        history[0].Reason.Should().Be("Extended timeout for evening service");
        history[0].ChangedBy.Should().Be(_userId);

        history[1].OldValue.Should().BeNull();
        history[1].NewValue.Should().Be("00:15:00");
    }

    [Fact]
    public async Task UpdateWithStaleVersionThrowsSettingConcurrencyException()
    {
        var regRequest = new RegisterSettingRequest(
            Key: "kitchen.auto_route_limit",
            Value: "5",
            DataType: SettingDataType.WholeNumber,
            Scope: SettingScope.Module,
            ModuleOwner: "Kitchen");

        await _repository.RegisterSettingAsync(regRequest);

        var updateRequest = new UpdateSettingRequest(
            Key: "kitchen.auto_route_limit",
            NewValue: "10",
            ExpectedRowVersion: 99); // Stale version

        var act = () => _repository.UpdateSettingAsync(updateRequest);

        var ex = await act.Should().ThrowAsync<SettingConcurrencyException>();
        ex.Which.SettingKey.Should().Be("kitchen.auto_route_limit");
        ex.Which.ExpectedVersion.Should().Be(99);
    }

    [Fact]
    public async Task DeactivateSettingSoftDeletesAndPreservesHistory()
    {
        var regRequest = new RegisterSettingRequest(
            Key: "pos.legacy_theme",
            Value: "ClassicDark",
            DataType: SettingDataType.Text,
            Scope: SettingScope.Global,
            ModuleOwner: "POS",
            RegisteredBy: _userId);

        var setting = await _repository.RegisterSettingAsync(regRequest);

        var deactRequest = new DeactivateSettingRequest(
            Key: "pos.legacy_theme",
            ExpectedRowVersion: 1,
            DeactivatedBy: _userId,
            Reason: "Deprecated theme option");

        var deactivated = await _repository.DeactivateSettingAsync(deactRequest);

        deactivated.Active.Should().BeFalse();
        deactivated.RowVersion.Should().Be(2);

        // Verify not in active list
        var activeSettings = await _repository.GetAllActiveAsync();
        activeSettings.Should().NotContain(s => s.Key == "pos.legacy_theme");

        // Verify still retrievable directly
        var direct = await _repository.GetByKeyAsync("pos.legacy_theme");
        direct.Should().NotBeNull();
        direct!.Active.Should().BeFalse();

        // Verify history includes deactivation
        var history = await _repository.GetHistoryAsync(setting.SettingId);
        history.Should().HaveCount(2);
        history[0].NewValue.Should().Be("[Deactivated]");
        history[0].Reason.Should().Be("Deprecated theme option");
    }

    [Fact]
    public async Task RegisterSecretKeyThrowsSecretSettingsStorageBanException()
    {
        var request = new RegisterSettingRequest(
            Key: "integrations.qnb.api_token",
            Value: "secret_12345",
            DataType: SettingDataType.Text,
            Scope: SettingScope.Global,
            ModuleOwner: "Integrations");

        var act = () => _repository.RegisterSettingAsync(request);

        var ex = await act.Should().ThrowAsync<SecretSettingsStorageBanException>();
        ex.Which.SettingKey.Should().Be("integrations.qnb.api_token");
    }

    [Fact]
    public async Task RegisterInvalidDataTypeThrowsSettingTypeValidationException()
    {
        var request = new RegisterSettingRequest(
            Key: "orders.max_split_count",
            Value: "not_a_number",
            DataType: SettingDataType.WholeNumber,
            Scope: SettingScope.Module,
            ModuleOwner: "Orders");

        var act = () => _repository.RegisterSettingAsync(request);

        var ex = await act.Should().ThrowAsync<SettingTypeValidationException>();
        ex.Which.SettingKey.Should().Be("orders.max_split_count");
        ex.Which.ExpectedType.Should().Be(SettingDataType.WholeNumber);
    }

    [Fact]
    public async Task RegisterDuplicateKeyThrowsDuplicateSettingKeyException()
    {
        var request1 = new RegisterSettingRequest("app.company_name", "Alkaros", SettingDataType.Text, SettingScope.Global, "Core");
        await _repository.RegisterSettingAsync(request1);

        var request2 = new RegisterSettingRequest("app.company_name", "Another Name", SettingDataType.Text, SettingScope.Global, "Core");
        var act = () => _repository.RegisterSettingAsync(request2);

        await act.Should().ThrowAsync<DuplicateSettingKeyException>();
    }

    [Fact]
    public async Task StronglyTypedSettingsServiceMethodsWorkCorrectly()
    {
        // 1. Integer (WholeNumber)
        await _service.RegisterSettingAsync(new RegisterSettingRequest("test.int_val", "42", SettingDataType.WholeNumber, SettingScope.Global, "Test"));
        var intVal = await _service.GetValueAsync<int>("test.int_val");
        intVal.Should().Be(42);

        // 2. Decimal (PreciseNumber)
        await _service.RegisterSettingAsync(new RegisterSettingRequest("test.dec_val", "123.45", SettingDataType.PreciseNumber, SettingScope.Global, "Test"));
        var decVal = await _service.GetValueAsync<decimal>("test.dec_val");
        decVal.Should().Be(123.45m);

        // 3. Boolean (Toggle)
        await _service.RegisterSettingAsync(new RegisterSettingRequest("test.bool_val", "true", SettingDataType.Toggle, SettingScope.Global, "Test"));
        var boolVal = await _service.GetValueAsync<bool>("test.bool_val");
        boolVal.Should().BeTrue();

        // 4. Duration (TimeSpan)
        await _service.RegisterSettingAsync(new RegisterSettingRequest("test.span_val", "01:30:00", SettingDataType.Duration, SettingScope.Global, "Test"));
        var spanVal = await _service.GetValueAsync<TimeSpan>("test.span_val");
        spanVal.Should().Be(new TimeSpan(1, 30, 0));

        // 5. GetValueOrDefault
        var defaultMissing = await _service.GetValueOrDefaultAsync<int>("non_existing_key", 999);
        defaultMissing.Should().Be(999);

        // 6. SetValueAsync typed helper
        var updated = await _service.SetValueAsync<int>("test.int_val", 100, expectedRowVersion: 1);
        updated.Value.Should().Be("100");
        var newIntVal = await _service.GetValueAsync<int>("test.int_val");
        newIntVal.Should().Be(100);
    }
}

/// <summary>
/// Migration tests verifying up/down cycle for 026-typed-settings.
/// </summary>
public sealed class PostgresSettingsMigrationTests : IClassFixture<SettingsTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSettingsMigrationTests(SettingsTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "026-typed-settings.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "026-typed-settings.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('settings.settings')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('settings.setting_history')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables exist again
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('settings.settings')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("settings.settings");
            }
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('settings.setting_history')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("settings.setting_history");
            }
        }
    }
}

[CollectionDefinition(nameof(SettingsTestFixtureDefinition), DisableParallelization = true)]
public sealed class SettingsTestFixtureDefinition : ICollectionFixture<SettingsTestDatabase>
{
}
