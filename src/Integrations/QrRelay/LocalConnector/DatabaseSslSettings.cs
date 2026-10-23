using Npgsql;

namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// The TLS settings every process that opens a PostgreSQL connection shares (the host, its one-shot verbs, the
/// connector and the psql child processes): one place decides the SSL mode so no connection string is left to
/// Npgsql's silent fallback to plain text. Lives in this library because both the host and the connector reference it.
/// </summary>
public static class DatabaseSslSettings
{
    public const string ModeVariable = "ALKAROS_DB_SSL_MODE";
    public const string RootCertVariable = "ALKAROS_DB_SSL_ROOT_CERT";

    /// <summary>
    /// The configured mode; when none is configured a loopback server may fall back to plain text (a developer's
    /// own machine) and every other server requires TLS.
    /// </summary>
    public static SslMode ModeFor(string host)
    {
        var configured = Environment.GetEnvironmentVariable(ModeVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return IsLoopback(host) ? SslMode.Prefer : SslMode.Require;

        return configured.Trim().ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "prefer" => SslMode.Prefer,
            "require" => SslMode.Require,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            _ => throw new ArgumentException($"{ModeVariable} must be one of disable, prefer, require, verify-ca, verify-full."),
        };
    }

    public static void Apply(NpgsqlConnectionStringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SslMode = ModeFor(builder.Host ?? string.Empty);
        if (RootCertificate() is { } root)
            builder.RootCertificate = root;
    }

    /// <summary>The value psql / libpq expects in PGSSLMODE for the same decision.</summary>
    public static string PsqlMode(string host) => ModeFor(host) switch
    {
        SslMode.Disable => "disable",
        SslMode.Prefer => "prefer",
        SslMode.Require => "require",
        SslMode.VerifyCA => "verify-ca",
        _ => "verify-full",
    };

    public static string? RootCertificate()
    {
        var root = Environment.GetEnvironmentVariable(RootCertVariable);
        return string.IsNullOrWhiteSpace(root) ? null : root.Trim();
    }

    private static bool IsLoopback(string host)
        => host.Length == 0
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var address) && System.Net.IPAddress.IsLoopback(address));
}
