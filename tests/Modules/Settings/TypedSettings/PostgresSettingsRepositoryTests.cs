using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.QrOrderExpiry;
using ALKAROS.Settings.TypedSettings.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;
using Toggles = ALKAROS.Settings.GarsonFeatureToggles.GarsonFeatureToggles;

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
    public async Task OutOfRangeValuesForTheKnownSettingsAreRefusedAndTheStoredValueStaysUntouched()
    {
        var known = new (string Key, string Default, SettingDataType Type, string[] Bad, string[] Good)[]
        {
            ("waiter.max_active_tables", "0", SettingDataType.WholeNumber, ["-1", "51"], ["50", "0"]),
            ("kitchen.dense_mode_threshold", "9", SettingDataType.WholeNumber, ["0", "100"], ["1", "99"]),
            ("qr_ordering.pending_confirmation_timeout", "00:05:00", SettingDataType.Duration,
                ["00:00:00", "-00:10:00", "00:00:59", "1.00:00:01", "5"], ["00:01:00", "1.00:00:00", "PT30M"]),
            ("business.name", "", SettingDataType.Text, [new string('a', 81), "ad\u0007"], [new string('a', 80)]),
        };

        foreach (var (key, defaultValue, type, bad, good) in known)
        {
            await _repository.RegisterSettingAsync(new RegisterSettingRequest(key, defaultValue, type, SettingScope.Global, "Test"));

            foreach (var value in bad)
            {
                var act = () => _repository.UpdateSettingAsync(new UpdateSettingRequest(key, value, 1, _userId));
                var thrown = await Record.ExceptionAsync(act);
                thrown.Should().BeAssignableTo<SettingsException>($"{key} must refuse '{value}'");
                (await _repository.GetByKeyAsync(key))!.Value.Should().Be(defaultValue);
            }

            var rowVersion = 1L;
            foreach (var value in good)
                rowVersion = (await _repository.UpdateSettingAsync(new UpdateSettingRequest(key, value, rowVersion, _userId))).RowVersion;
        }
    }

    [Fact]
    public async Task ABareNumberIsNotAcceptedAsADurationAndAZeroOrNegativeOneIsNeverAccepted()
    {
        foreach (var bad in new[] { "5", "00:00:00", "-00:05:00", "P0D" })
        {
            var act = () => _repository.RegisterSettingAsync(new RegisterSettingRequest(
                "test.duration." + Guid.NewGuid().ToString("N")[..8], bad, SettingDataType.Duration, SettingScope.Global, "Test"));
            await act.Should().ThrowAsync<SettingTypeValidationException>($"'{bad}' is not a usable duration");
        }
    }

    [Fact]
    public async Task AnOverlongValueOrReasonIsRefused()
    {
        var tooLong = await _repository.RegisterSettingAsync(new RegisterSettingRequest(
            "test.long.text", "ok", SettingDataType.Text, SettingScope.Global, "Test"));

        var longValue = () => _repository.UpdateSettingAsync(new UpdateSettingRequest(tooLong.Key, new string('x', 2001), 1, _userId));
        await longValue.Should().ThrowAsync<SettingValueRangeException>();

        var longReason = () => _repository.UpdateSettingAsync(new UpdateSettingRequest(tooLong.Key, "fine", 1, _userId, new string('r', 501)));
        await longReason.Should().ThrowAsync<ArgumentException>();

        (await _repository.GetByKeyAsync(tooLong.Key))!.Value.Should().Be("ok");
    }

    [Fact]
    public async Task ADeactivatedSettingRefusesAChangeAndASecondDeactivationWithoutWritingHistory()
    {
        var setting = await _repository.RegisterSettingAsync(new RegisterSettingRequest(
            "test.inactive.key", "a", SettingDataType.Text, SettingScope.Global, "Test"));
        var deactivated = await _repository.DeactivateSettingAsync(new DeactivateSettingRequest(setting.Key, 1, _userId));

        var change = () => _repository.UpdateSettingAsync(new UpdateSettingRequest(setting.Key, "b", deactivated.RowVersion, _userId));
        var again = () => _repository.DeactivateSettingAsync(new DeactivateSettingRequest(setting.Key, deactivated.RowVersion, _userId));

        await change.Should().ThrowAsync<SettingInactiveException>();
        await again.Should().ThrowAsync<SettingInactiveException>();
        (await _repository.GetHistoryAsync(setting.SettingId)).Should().HaveCount(2);
        (await _repository.GetByKeyAsync(setting.Key))!.Value.Should().Be("a");
    }

    [Fact]
    public async Task ManyParallelRegistrationsOfOneKeyLeaveExactlyOneWinnerAndNoRawDatabaseError()
    {
        var attempts = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            try
            {
                await _repository.RegisterSettingAsync(new RegisterSettingRequest(
                    "test.race.key", i.ToString(System.Globalization.CultureInfo.InvariantCulture), SettingDataType.WholeNumber, SettingScope.Global, "Test"));
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        })).ToArray();

        var results = await Task.WhenAll(attempts);

        results.Count(r => r is null).Should().Be(1);
        results.Where(r => r is not null).Should().AllBeOfType<DuplicateSettingKeyException>();
    }

    [Theory]
    [InlineData("x.api-key")]
    [InlineData("svc.api.key")]
    [InlineData("db.pass_wd")]
    [InlineData("db.pwd")]
    [InlineData("mail.passphrase")]
    [InlineData("Svc.Client-Secret")]
    public void SecretKeysAreBannedWhateverTheSeparator(string key)
    {
        var act = () => _validator.ValidateKey(key);

        act.Should().Throw<SecretSettingsStorageBanException>();
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

    private async Task<SettingRecord> RegisterHistorySettingAsync(string key)
        => await _repository.RegisterSettingAsync(new RegisterSettingRequest(
            Key: key, Value: "0", DataType: SettingDataType.WholeNumber, Scope: SettingScope.Module, ModuleOwner: "History", RegisteredBy: _userId));

    private async Task AddHistoryRowsAsync(Guid settingId, int count, string? fixedTime)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO settings.setting_history (setting_history_id, setting_id, old_value, new_value, changed_at)
            SELECT gen_random_uuid(), @id, NULL, 'v' || n, {(fixedTime is null ? "now() + n * interval '1 second'" : "@at::timestamptz")}
            FROM generate_series(1, @count) AS n;
            """,
            connection);
        command.Parameters.AddWithValue("id", settingId);
        command.Parameters.AddWithValue("count", count);
        if (fixedTime is not null)
            command.Parameters.AddWithValue("at", fixedTime);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task HistoryEntriesWithTheSameInstantComeBackInAStableNewestFirstOrder()
    {
        var setting = await RegisterHistorySettingAsync("history.tie_order");
        await AddHistoryRowsAsync(setting.SettingId, 20, "2031-01-01T00:00:00Z");

        var first = await _repository.GetHistoryAsync(setting.SettingId);
        var second = await _repository.GetHistoryAsync(setting.SettingId);

        var tied = first.Where(entry => entry.ChangedAt == DateTimeOffset.Parse("2031-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)).Select(entry => entry.SettingHistoryId).ToList();
        tied.Should().HaveCount(20);
        tied.Should().BeInDescendingOrder();
        first.Select(entry => entry.SettingHistoryId).Should().Equal(second.Select(entry => entry.SettingHistoryId));
    }

    [Fact]
    public async Task AVeryLongHistoryReturnsTheNewestEntriesInsteadOfFailing()
    {
        var setting = await RegisterHistorySettingAsync("history.very_long");
        await AddHistoryRowsAsync(setting.SettingId, 5001, null);

        var history = await _repository.GetHistoryAsync(setting.SettingId);

        history.Should().HaveCount(5000);
        history[0].NewValue.Should().Be("v5001");
        history.Should().NotContain(entry => entry.NewValue == "v1" || entry.NewValue == "0");
    }
}

/// <summary>One fresh database per test: the toggle keys are fixed, so a shared database would make tests order-dependent.</summary>
public sealed class GarsonFeatureTogglesTests : IAsyncLifetime
{
    private readonly SettingsTestDatabase _db = new();
    private CountingSettings _settings = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _settings = new CountingSettings(new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator())));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task FirstAskRegistersEveryFeatureAndAllAreOn()
    {
        var all = await Toggles.GetAllAsync(_settings);

        all.Keys.Should().BeEquivalentTo(Enum.GetValues<GarsonFeature>());
        all.Values.Should().OnlyContain(enabled => enabled);
        foreach (var feature in Enum.GetValues<GarsonFeature>())
            (await _settings.GetRecordAsync(Toggles.KeyFor(feature)))!.Value.Should().Be("true");
    }

    [Fact]
    public async Task AFeatureAnOperatorTurnedOffReadsOffWhileTheRestStayOn()
    {
        await Toggles.EnsureAllRegisteredAsync(_settings);
        var key = Toggles.KeyFor(GarsonFeature.VoluntaryTip);
        var record = (await _settings.GetRecordAsync(key))!;
        await _settings.SetValueAsync(key, false, record.RowVersion);

        var all = await Toggles.GetAllAsync(_settings);

        all[GarsonFeature.VoluntaryTip].Should().BeFalse();
        all.Where(pair => pair.Key != GarsonFeature.VoluntaryTip).Should().OnlyContain(pair => pair.Value);
        (await Toggles.IsEnabledAsync(_settings, GarsonFeature.VoluntaryTip)).Should().BeFalse();
    }

    [Fact]
    public async Task ADeactivatedFeatureReadsOnInBothTheSingleAndTheBatchRead()
    {
        await Toggles.EnsureAllRegisteredAsync(_settings);
        var key = Toggles.KeyFor(GarsonFeature.PartySize);
        var turnedOff = await _settings.SetValueAsync(key, false, (await _settings.GetRecordAsync(key))!.RowVersion);
        await _settings.DeactivateAsync(key, turnedOff.RowVersion);

        var all = await Toggles.GetAllAsync(_settings);

        all[GarsonFeature.PartySize].Should().BeTrue();
        (await Toggles.IsEnabledAsync(_settings, GarsonFeature.PartySize)).Should().BeTrue();
    }

    [Fact]
    public async Task OnceRegisteredTheBatchReadIsOneQueryNotOnePerFeature()
    {
        await Toggles.EnsureAllRegisteredAsync(_settings);
        _settings.Reads = 0;

        await Toggles.GetAllAsync(_settings);

        _settings.Reads.Should().Be(1);
    }

    [Fact]
    public async Task RegisteringAgainNeverResetsAChosenValue()
    {
        await Toggles.EnsureAllRegisteredAsync(_settings);
        var key = Toggles.KeyFor(GarsonFeature.ShiftSummary);
        await _settings.SetValueAsync(key, false, (await _settings.GetRecordAsync(key))!.RowVersion);

        await Toggles.EnsureAllRegisteredAsync(_settings);

        (await _settings.GetRecordAsync(key))!.Value.Should().Be("false");
    }

    private sealed class CountingSettings(ISettingsService inner) : ISettingsService
    {
        public int Reads { get; set; }

        public Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.GetValueAsync<T>(key, cancellationToken);
        }

        public Task<T> GetValueOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.GetValueOrDefaultAsync(key, defaultValue, cancellationToken);
        }

        public Task<SettingRecord?> GetRecordAsync(string key, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.GetRecordAsync(key, cancellationToken);
        }

        public Task<IReadOnlyList<SettingRecord>> GetByModuleOwnerAsync(string moduleOwner, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.GetByModuleOwnerAsync(moduleOwner, cancellationToken);
        }

        public Task<IReadOnlyList<SettingRecord>> GetAllActiveAsync(CancellationToken cancellationToken = default)
            => inner.GetAllActiveAsync(cancellationToken);

        public Task<SettingRecord> RegisterSettingAsync(RegisterSettingRequest request, CancellationToken cancellationToken = default)
            => inner.RegisterSettingAsync(request, cancellationToken);

        public Task<SettingRecord> SetValueAsync<T>(
            string key, T value, long expectedRowVersion, Guid? updatedBy = null, string? reason = null, CancellationToken cancellationToken = default)
            => inner.SetValueAsync(key, value, expectedRowVersion, updatedBy, reason, cancellationToken);

        public Task<SettingRecord> DeactivateAsync(
            string key, long expectedRowVersion, Guid? deactivatedBy = null, string? reason = null, CancellationToken cancellationToken = default)
            => inner.DeactivateAsync(key, expectedRowVersion, deactivatedBy, reason, cancellationToken);

        public Task<IReadOnlyList<SettingHistoryRecord>> GetHistoryAsync(Guid settingId, CancellationToken cancellationToken = default)
            => inner.GetHistoryAsync(settingId, cancellationToken);
    }
}

/// <summary>One fresh database per test: the key is a fixed constant, so sharing would make tests order-dependent.</summary>
public sealed class QrOrderExpirySettingTests : IAsyncLifetime
{
    private readonly SettingsTestDatabase _db = new();
    private SettingsService _service = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _service = new SettingsService(new PostgresSettingsRepository(_db.DataSource, new SettingValidator()));
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task FirstAskRegistersTheFiveMinuteDefault()
    {
        (await _service.GetRecordAsync(QrOrderExpirySetting.Key)).Should().BeNull();

        var timeout = await QrOrderExpirySetting.GetTimeoutAsync(_service);

        timeout.Should().Be(TimeSpan.FromMinutes(5));
        var record = await _service.GetRecordAsync(QrOrderExpirySetting.Key);
        record!.DataType.Should().Be(SettingDataType.Duration);
        record.Scope.Should().Be(SettingScope.Global);
        record.Value.Should().Be("00:05:00");
    }

    [Fact]
    public async Task AChosenTimeoutIsReadBackAndSurvivesRegisteringAgain()
    {
        await QrOrderExpirySetting.EnsureRegisteredAsync(_service);
        var record = (await _service.GetRecordAsync(QrOrderExpirySetting.Key))!;
        await _service.SetValueAsync(QrOrderExpirySetting.Key, TimeSpan.FromMinutes(12), record.RowVersion);

        await QrOrderExpirySetting.EnsureRegisteredAsync(_service);

        (await QrOrderExpirySetting.GetTimeoutAsync(_service)).Should().Be(TimeSpan.FromMinutes(12));
    }

    [Fact]
    public async Task ADeactivatedSettingFallsBackToTheDefault()
    {
        await QrOrderExpirySetting.EnsureRegisteredAsync(_service);
        var record = (await _service.GetRecordAsync(QrOrderExpirySetting.Key))!;
        var changed = await _service.SetValueAsync(QrOrderExpirySetting.Key, TimeSpan.FromMinutes(12), record.RowVersion);
        await _service.DeactivateAsync(QrOrderExpirySetting.Key, changed.RowVersion);

        (await QrOrderExpirySetting.GetTimeoutAsync(_service)).Should().Be(QrOrderExpirySetting.Default);
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
