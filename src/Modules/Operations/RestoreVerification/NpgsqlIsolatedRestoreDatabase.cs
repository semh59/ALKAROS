using System.Text.RegularExpressions;
using Npgsql;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// Provisions a real, throwaway PostgreSQL database per restore drill —
/// <c>CREATE DATABASE</c> against a maintenance connection, then a fresh
/// data source scoped to it. Disposing drops it with <c>WITH (FORCE)</c>
/// so a stray open connection never leaks the scratch database.
/// </summary>
public sealed class NpgsqlIsolatedRestoreDatabaseFactory : IIsolatedRestoreDatabaseFactory
{
    private static readonly Regex GeneratedNamePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly string _maintenanceConnectionString;

    /// <param name="maintenanceConnectionString">
    /// Connects to a maintenance database (typically "postgres") on the
    /// same server the restore should be drilled against — never the live
    /// application database.
    /// </param>
    public NpgsqlIsolatedRestoreDatabaseFactory(string maintenanceConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(maintenanceConnectionString);
        _maintenanceConnectionString = maintenanceConnectionString;
    }

    public async Task<IIsolatedRestoreDatabase> ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var name = "alkaros_restore_drill_" + Guid.NewGuid().ToString("N")[..16];
        if (!GeneratedNamePattern.IsMatch(name))
            throw new InvalidOperationException("Generated scratch database name failed its own charset check.");

        await using (var maintenance = new NpgsqlDataSourceBuilder(_maintenanceConnectionString).Build())
        {
            await using var create = maintenance.CreateCommand($"CREATE DATABASE {QuoteIdentifier(name)};");
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var scopedBuilder = new NpgsqlConnectionStringBuilder(_maintenanceConnectionString) { Database = name };
        var dataSource = new NpgsqlDataSourceBuilder(scopedBuilder.ConnectionString).Build();
        return new NpgsqlIsolatedRestoreDatabase(name, dataSource, _maintenanceConnectionString);
    }

    private static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}

internal sealed class NpgsqlIsolatedRestoreDatabase : IIsolatedRestoreDatabase
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _maintenanceConnectionString;
    private bool _disposed;

    public string Name { get; }

    internal NpgsqlIsolatedRestoreDatabase(string name, NpgsqlDataSource dataSource, string maintenanceConnectionString)
    {
        Name = name;
        _dataSource = dataSource;
        _maintenanceConnectionString = maintenanceConnectionString;
    }

    public async Task ApplyScriptAsync(string sqlScript, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sqlScript);
        await using var command = _dataSource.CreateCommand(sqlScript);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var command = _dataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await _dataSource.DisposeAsync();

        await using var maintenance = new NpgsqlDataSourceBuilder(_maintenanceConnectionString).Build();
        await using var drop = maintenance.CreateCommand($"DROP DATABASE IF EXISTS \"{Name.Replace("\"", "\"\"")}\" WITH (FORCE);");
        await drop.ExecuteNonQueryAsync();
    }
}
