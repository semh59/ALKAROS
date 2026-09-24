namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// The full connection string the host was started with, password included.
/// <c>NpgsqlDataSource.ConnectionString</c> deliberately omits the password, so
/// anything that must open ANOTHER connection to the same server (the restore
/// drill provisions and drops a scratch database) cannot derive it from the
/// data source and would fail authentication.
/// </summary>
public sealed record HostDatabaseConnection(string ConnectionString);
