using Npgsql;

namespace ALKAROS.Host.DualScreen;

public sealed class DualScreenStartupException : Exception
{
    public DualScreenStartupException(string message)
        : base(message)
    {
    }
}

public sealed record DualScreenOptions(string ConnectionString, string WebRoot, string Url)
{
    private const string PasswordEnvironmentVariable = "ALKAROS_DB_PASSWORD";

    public static DualScreenOptions Parse(string[] args)
    {
        string? databaseUrl = null;
        string? webRoot = null;
        var url = "http://127.0.0.1:5080";

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--db-url" when index + 1 < args.Length && databaseUrl is null:
                    databaseUrl = args[++index];
                    break;
                case "--web-root" when index + 1 < args.Length && webRoot is null:
                    webRoot = args[++index];
                    break;
                case "--urls" when index + 1 < args.Length:
                    url = args[++index];
                    break;
                default:
                    throw new DualScreenStartupException("Invalid dual-screen serve arguments.");
            }
        }

        if (string.IsNullOrWhiteSpace(databaseUrl) || string.IsNullOrWhiteSpace(webRoot))
            throw new DualScreenStartupException("Both --db-url and --web-root are required.");

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(password))
            throw new DualScreenStartupException($"{PasswordEnvironmentVariable} is required.");

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres")
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new DualScreenStartupException("--db-url must be a PostgreSQL URL with host and database.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 1 || string.IsNullOrWhiteSpace(userInfo[0]))
            throw new DualScreenStartupException("--db-url must contain a username and must not contain a password.");

        var resolvedWebRoot = Path.GetFullPath(webRoot);
        if (!File.Exists(Path.Combine(resolvedWebRoot, "index.html")))
            throw new DualScreenStartupException("--web-root must contain the built index.html file.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var listenUri)
            || (listenUri.Scheme != Uri.UriSchemeHttp && listenUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DualScreenStartupException("--urls must contain one absolute HTTP or HTTPS URL.");
        }

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = password,
            ApplicationName = "ALKAROS.DualScreen",
            Pooling = true,
        }.ConnectionString;

        return new DualScreenOptions(connectionString, resolvedWebRoot, listenUri.ToString());
    }
}
