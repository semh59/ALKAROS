using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.QrRelay.LocalConnector;
using ALKAROS.QrRelay.PublicGateway;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.QrRelay.ConnectorHost;

/// <summary>
/// V12-QRT-005. Standalone entry point for the relay connector — everything
/// this process needs to supervise `cloudflared` and report its own status,
/// and nothing else (no ASP.NET Core, no other module, no reflection-based
/// module discovery). Splits `RelayConnectorSupervisor` (V12-QRT-001) out of
/// the main `api` container: see docs/architecture/qr-relay-topology.md
/// (V0-ARC-009) for why "Connector -&gt; POS: Internal localhost" must stay
/// true regardless (compose.yaml's `network_mode: "service:api"` on this
/// container is what preserves it, not anything in this file).
/// </summary>
public static class Program
{
    private const string PasswordEnvironmentVariable = "ALKAROS_DB_PASSWORD";

    public static async Task<int> Main(string[] args)
    {
        string databaseUrl;
        try
        {
            databaseUrl = ParseDbUrlArgument(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: ALKAROS.QrRelay.ConnectorHost --db-url <url>");
            return 2;
        }

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine($"{PasswordEnvironmentVariable} is required.");
            return 2;
        }

        var connectionString = BuildConnectionString(databaseUrl, password);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

        // Same registration chain as QrOrderingModule.Register — this
        // process's own DI container, not shared with the `api` container's.
        builder.Services.AddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        builder.Services.AddTransient<ISecretAccessPolicy, RelayCredentialAccessPolicy>();
        builder.Services.AddTransient<ISensitiveDataAccessPolicy, RelayCredentialAccessPolicy>();
        builder.Services.AddTransient<ISecretResolver, SecretResolver>();
        builder.Services.AddTransient<IEnvelopeCipher, AesGcmEnvelopeCipher>();
        builder.Services.AddTransient<SensitivePayloadProtector, SensitivePayloadProtector>();
        builder.Services.AddTransient<IRelayTunnelStore, PostgresRelayTunnelStore>();

        builder.Services.AddSingleton<ICloudflaredProcessFactory, CloudflaredProcessFactory>();
        builder.Services.AddSingleton<RelayConnectorSupervisor>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RelayConnectorSupervisor>());
        builder.Services.AddSingleton<IRelayConnectorStatusReporter>(
            sp => sp.GetRequiredService<RelayConnectorSupervisor>());
        builder.Services.AddHostedService<RelayConnectorStatusPublisher>();

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    private static string ParseDbUrlArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--db-url" && i + 1 < args.Length)
                return args[i + 1];
        }

        throw new ArgumentException("--db-url is required.");
    }

    private static string BuildConnectionString(string databaseUrl, string password)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres")
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new ArgumentException("--db-url must be a PostgreSQL URL with host and database.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 1 || string.IsNullOrWhiteSpace(userInfo[0]))
            throw new ArgumentException("--db-url must contain a username and must not contain a password.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = password,
        }.ConnectionString;
    }
}
