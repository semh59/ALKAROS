using Npgsql;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// The full connection string the host was started with, password included.
/// <c>NpgsqlDataSource.ConnectionString</c> deliberately omits the password, so
/// anything that must open ANOTHER connection to the same server (the restore
/// drill provisions and drops a scratch database) cannot derive it from the
/// data source and would fail authentication.
/// </summary>
public sealed record HostDatabaseConnection(string ConnectionString)
{
    public const string RestoreConnectionVariable = "ALKAROS_RESTORE_MAINTENANCE_CONNECTION";
    public const string RestoreUserVariable = "ALKAROS_RESTORE_DB_USER";
    public const string RestorePasswordFileVariable = "ALKAROS_RESTORE_DB_PASSWORD_FILE";

    public override string ToString() => nameof(HostDatabaseConnection);

    /// <summary>
    /// The connection the restore drill uses to create and drop its scratch database. The application role cannot
    /// create databases, so a deployment names a separate role that can (and can reach nothing else) through
    /// <see cref="RestoreUserVariable"/> and <see cref="RestorePasswordFileVariable"/>; a complete connection string in
    /// <see cref="RestoreConnectionVariable"/> still wins, and with neither the application's own connection is used.
    /// </summary>
    public string RestoreMaintenanceConnectionString()
    {
        if (Environment.GetEnvironmentVariable(RestoreConnectionVariable) is { Length: > 0 } explicitConnection)
            return explicitConnection;

        var user = Environment.GetEnvironmentVariable(RestoreUserVariable);
        if (string.IsNullOrWhiteSpace(user))
            return ConnectionString;

        var passwordFile = Environment.GetEnvironmentVariable(RestorePasswordFileVariable);
        if (string.IsNullOrWhiteSpace(passwordFile))
            throw new InvalidOperationException($"{RestoreUserVariable} needs {RestorePasswordFileVariable}.");

        return new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = user.Trim(),
            Password = File.ReadAllText(passwordFile.Trim()).Trim('\r', '\n'),
        }.ConnectionString;
    }
}
