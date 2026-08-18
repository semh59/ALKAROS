using System.Data;
using System.Data.Common;

namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// ADO.NET / PostgreSQL implementation of <see cref="ISettingsRepository"/> (V1-SET-001, PDF:III.27).
/// Provides atomic setting updates and append-only audit history persistence using standard <see cref="DbDataSource"/>.
/// </summary>
public sealed class PostgresSettingsRepository : ISettingsRepository
{
    private const string SettingsTable = "settings.settings";
    private const string HistoryTable = "settings.setting_history";

    private readonly DbDataSource _dataSource;
    private readonly ISettingValidator _validator;

    public PostgresSettingsRepository(DbDataSource dataSource, ISettingValidator validator)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<SettingRecord?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or whitespace.", nameof(key));

        const string sql = $"""
            SELECT setting_id, setting_key, setting_value, data_type, scope,
                   module_owner, description, requires_restart, active, updated_at, row_version
            FROM {SettingsTable}
            WHERE setting_key = @key;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "key", key);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<SettingRecord>> GetByModuleOwnerAsync(
        string moduleOwner,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleOwner))
            throw new ArgumentException("Module owner cannot be null or whitespace.", nameof(moduleOwner));

        const string sql = $"""
            SELECT setting_id, setting_key, setting_value, data_type, scope,
                   module_owner, description, requires_restart, active, updated_at, row_version
            FROM {SettingsTable}
            WHERE module_owner = @module_owner
            ORDER BY setting_key ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "module_owner", moduleOwner);

        var list = new List<SettingRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<SettingRecord>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT setting_id, setting_key, setting_value, data_type, scope,
                   module_owner, description, requires_restart, active, updated_at, row_version
            FROM {SettingsTable}
            WHERE active = true
            ORDER BY module_owner ASC, setting_key ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<SettingRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<SettingHistoryRecord>> GetHistoryAsync(
        Guid settingId,
        CancellationToken cancellationToken = default)
    {
        if (settingId == Guid.Empty)
            throw new ArgumentException("Setting ID cannot be empty.", nameof(settingId));

        const string sql = $"""
            SELECT setting_history_id, setting_id, old_value, new_value, reason, changed_by, changed_at
            FROM {HistoryTable}
            WHERE setting_id = @setting_id
            ORDER BY changed_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "setting_id", settingId);

        var list = new List<SettingHistoryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SettingHistoryRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return list;
    }

    public async Task<SettingRecord> RegisterSettingAsync(
        RegisterSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        _validator.ValidateKey(request.Key);
        _validator.ValidateValue(request.Key, request.Value, request.DataType);

        var settingId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Check duplicate
        const string checkSql = $"SELECT 1 FROM {SettingsTable} WHERE setting_key = @key;";
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = checkSql;
            AddParameter(checkCmd, "key", request.Key);
            var exists = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (exists is not null and not DBNull)
                throw new DuplicateSettingKeyException(request.Key);
        }

        // Insert Setting
        const string insertSettingSql = $"""
            INSERT INTO {SettingsTable} (
                setting_id, setting_key, setting_value, data_type, scope,
                module_owner, description, requires_restart, active, updated_at, row_version
            ) VALUES (
                @id, @key, @value, @data_type, @scope,
                @module_owner, @description, @requires_restart, true, @now, 1
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertSettingSql;
            AddParameter(cmd, "id", settingId);
            AddParameter(cmd, "key", request.Key);
            AddParameter(cmd, "value", request.Value);
            AddParameter(cmd, "data_type", SettingDataTypeMapper.ToDbString(request.DataType));
            AddParameter(cmd, "scope", request.Scope.ToString());
            AddParameter(cmd, "module_owner", request.ModuleOwner);
            AddParameter(cmd, "description", (object?)request.Description ?? DBNull.Value);
            AddParameter(cmd, "requires_restart", request.RequiresRestart);
            AddParameter(cmd, "now", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert Initial History
        const string insertHistorySql = $"""
            INSERT INTO {HistoryTable} (
                setting_history_id, setting_id, old_value, new_value, reason, changed_by, changed_at
            ) VALUES (
                @id, @setting_id, NULL, @new_value, @reason, @changed_by, @now
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertHistorySql;
            AddParameter(cmd, "id", historyId);
            AddParameter(cmd, "setting_id", settingId);
            AddParameter(cmd, "new_value", request.Value);
            AddParameter(cmd, "reason", (object?)request.Reason ?? "Initial registration");
            AddParameter(cmd, "changed_by", (object?)request.RegisteredBy ?? DBNull.Value);
            AddParameter(cmd, "now", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SettingRecord(
            settingId,
            request.Key,
            request.Value,
            request.DataType,
            request.Scope,
            request.ModuleOwner,
            request.Description,
            request.RequiresRestart,
            Active: true,
            now,
            RowVersion: 1);
    }

    public async Task<SettingRecord> UpdateSettingAsync(
        UpdateSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        var historyId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Lock existing setting
        const string selectSql = $"""
            SELECT setting_id, setting_key, setting_value, data_type, scope,
                   module_owner, description, requires_restart, active, row_version
            FROM {SettingsTable}
            WHERE setting_key = @key
            FOR UPDATE;
            """;

        Guid settingId;
        string oldValue;
        SettingDataType dataType;
        SettingScope scope;
        string moduleOwner;
        string? description;
        bool requiresRestart;
        bool active;
        long currentRowVersion;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = selectSql;
            AddParameter(cmd, "key", request.Key);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new SettingNotFoundException(request.Key);

            settingId = reader.GetGuid(0);
            oldValue = reader.GetString(2);
            dataType = SettingDataTypeMapper.FromDbString(reader.GetString(3));
            scope = Enum.Parse<SettingScope>(reader.GetString(4), ignoreCase: true);
            moduleOwner = reader.GetString(5);
            description = reader.IsDBNull(6) ? null : reader.GetString(6);
            requiresRestart = reader.GetBoolean(7);
            active = reader.GetBoolean(8);
            currentRowVersion = reader.GetInt64(9);
        }

        // Validate Concurrency
        if (currentRowVersion != request.ExpectedRowVersion)
        {
            throw new SettingConcurrencyException(request.Key, request.ExpectedRowVersion, currentRowVersion);
        }

        // Validate New Value Type
        _validator.ValidateValue(request.Key, request.NewValue, dataType);

        // Update Setting Value
        long newRowVersion;
        const string updateSql = $"""
            UPDATE {SettingsTable}
            SET setting_value = @new_value,
                updated_at = @now,
                row_version = row_version + 1
            WHERE setting_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "new_value", request.NewValue);
            AddParameter(cmd, "now", now);
            AddParameter(cmd, "id", settingId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
                throw new SettingConcurrencyException(request.Key, request.ExpectedRowVersion, currentRowVersion);

            newRowVersion = (long)result;
        }

        // Append to History
        const string insertHistorySql = $"""
            INSERT INTO {HistoryTable} (
                setting_history_id, setting_id, old_value, new_value, reason, changed_by, changed_at
            ) VALUES (
                @id, @setting_id, @old_value, @new_value, @reason, @changed_by, @now
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertHistorySql;
            AddParameter(cmd, "id", historyId);
            AddParameter(cmd, "setting_id", settingId);
            AddParameter(cmd, "old_value", oldValue);
            AddParameter(cmd, "new_value", request.NewValue);
            AddParameter(cmd, "reason", (object?)request.Reason ?? DBNull.Value);
            AddParameter(cmd, "changed_by", (object?)request.UpdatedBy ?? DBNull.Value);
            AddParameter(cmd, "now", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SettingRecord(
            settingId,
            request.Key,
            request.NewValue,
            dataType,
            scope,
            moduleOwner,
            description,
            requiresRestart,
            active,
            now,
            newRowVersion);
    }

    public async Task<SettingRecord> DeactivateSettingAsync(
        DeactivateSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        var historyId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Lock existing setting
        const string selectSql = $"""
            SELECT setting_id, setting_key, setting_value, data_type, scope,
                   module_owner, description, requires_restart, active, row_version
            FROM {SettingsTable}
            WHERE setting_key = @key
            FOR UPDATE;
            """;

        Guid settingId;
        string currentValue;
        SettingDataType dataType;
        SettingScope scope;
        string moduleOwner;
        string? description;
        bool requiresRestart;
        long currentRowVersion;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = selectSql;
            AddParameter(cmd, "key", request.Key);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new SettingNotFoundException(request.Key);

            settingId = reader.GetGuid(0);
            currentValue = reader.GetString(2);
            dataType = SettingDataTypeMapper.FromDbString(reader.GetString(3));
            scope = Enum.Parse<SettingScope>(reader.GetString(4), ignoreCase: true);
            moduleOwner = reader.GetString(5);
            description = reader.IsDBNull(6) ? null : reader.GetString(6);
            requiresRestart = reader.GetBoolean(7);
            currentRowVersion = reader.GetInt64(9);
        }

        if (currentRowVersion != request.ExpectedRowVersion)
        {
            throw new SettingConcurrencyException(request.Key, request.ExpectedRowVersion, currentRowVersion);
        }

        // Deactivate Setting
        long newRowVersion;
        const string updateSql = $"""
            UPDATE {SettingsTable}
            SET active = false,
                updated_at = @now,
                row_version = row_version + 1
            WHERE setting_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "now", now);
            AddParameter(cmd, "id", settingId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
                throw new SettingConcurrencyException(request.Key, request.ExpectedRowVersion, currentRowVersion);

            newRowVersion = (long)result;
        }

        // Append Deactivation to History
        const string insertHistorySql = $"""
            INSERT INTO {HistoryTable} (
                setting_history_id, setting_id, old_value, new_value, reason, changed_by, changed_at
            ) VALUES (
                @id, @setting_id, @old_value, '[Deactivated]', @reason, @changed_by, @now
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertHistorySql;
            AddParameter(cmd, "id", historyId);
            AddParameter(cmd, "setting_id", settingId);
            AddParameter(cmd, "old_value", currentValue);
            AddParameter(cmd, "reason", (object?)request.Reason ?? "Setting deactivated");
            AddParameter(cmd, "changed_by", (object?)request.DeactivatedBy ?? DBNull.Value);
            AddParameter(cmd, "now", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SettingRecord(
            settingId,
            request.Key,
            currentValue,
            dataType,
            scope,
            moduleOwner,
            description,
            requiresRestart,
            Active: false,
            now,
            newRowVersion);
    }

    private static SettingRecord ReadRecord(DbDataReader reader)
    {
        return new SettingRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            SettingDataTypeMapper.FromDbString(reader.GetString(3)),
            Enum.Parse<SettingScope>(reader.GetString(4), ignoreCase: true),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetInt64(10));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
