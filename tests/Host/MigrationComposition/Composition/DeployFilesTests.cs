using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

public sealed class DeployFilesTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string[] RejectPlainTextRule = ["hostnossl", "all", "all", "all", "reject"];

    [Theory]
    [InlineData("alkaros")]
    [InlineData("postgres")]
    public void RestoreRefusesToRecreateTheLiveOrMaintenanceDatabase(string target)
    {
        var (exitCode, error) = RunShell($"sh deploy/docker/restore.sh missing.dump {target}");

        Assert.Equal(5, exitCode);
        Assert.Contains("refusing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreHonoursAnotherLiveDatabaseName()
    {
        var (exitCode, _) = RunShell("ALKAROS_LIVE_DB=shop sh deploy/docker/restore.sh missing.dump shop");

        Assert.Equal(5, exitCode);
    }

    [Fact]
    public void RestoreStillGetsPastTheGuardForASideDatabase()
    {
        var (exitCode, error) = RunShell("sh deploy/docker/restore.sh missing.dump alkaros_restore");

        Assert.Equal(2, exitCode);
        Assert.Contains("artifact not found", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchivingASegmentIsAtomicAndNeverOverwrites()
    {
        var archive = Path.Combine(Path.GetTempPath(), $"alkaros-wal-{Guid.NewGuid():N}");
        var source = Path.Combine(Path.GetTempPath(), $"alkaros-seg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(archive);
        File.WriteAllText(source, "segment");
        try
        {
            Assert.Contains(".tmp && mv ", ArchiveCommand(), StringComparison.Ordinal);
            var command = ArchiveCommand().Replace("/wal-archive", ToShellPath(archive), StringComparison.Ordinal);
            var script = $"p='{ToShellPath(source)}'; f=000000010000000000000001; {command.Replace("%p", "$p").Replace("%f", "$f")}";

            var first = RunShell(script);
            var second = RunShell(script);

            Assert.Equal(0, first.ExitCode);
            Assert.NotEqual(0, second.ExitCode);
            Assert.Equal("segment", File.ReadAllText(Path.Combine(archive, "000000010000000000000001")));
            Assert.Empty(Directory.GetFiles(archive, "*.tmp"));
        }
        finally
        {
            Directory.Delete(archive, recursive: true);
            File.Delete(source);
        }
    }

    [Fact]
    public void TheBuildContextExcludesTheEnvironmentFileAndGitHistory()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, ".dockerignore")).Select(line => line.Trim()).ToHashSet();

        Assert.Contains(".env", lines);
        Assert.Contains(".git", lines);
    }

    [Fact]
    public void TheApiReadsBackupsAndWritesTheOffsiteCopyOnMountedVolumes()
    {
        var compose = File.ReadAllText(Path.Combine(Root, "compose.yaml"));

        Assert.Contains("ALKAROS_BACKUP_DIR: /backups", compose, StringComparison.Ordinal);
        Assert.Contains("ALKAROS_OFFSITE_BACKUP_DIR: /offsite-backup", compose, StringComparison.Ordinal);
        Assert.Contains("alkaros-backups:/backups:ro", compose, StringComparison.Ordinal);
        Assert.Contains("stop_grace_period: 50s", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDevelopmentOverlayPublishesNothingBeyondLoopback()
    {
        var ports = File.ReadAllLines(Path.Combine(Root, "compose.dev.yaml"))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- \"", StringComparison.Ordinal) && line.Contains(':', StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(ports);
        Assert.All(ports, port => Assert.StartsWith("- \"127.0.0.1:", port, StringComparison.Ordinal));
    }

    [Fact]
    public void TheProductionShellIsRevalidatedButHashedAssetsAreNot()
    {
        var caddyfile = File.ReadAllText(Path.Combine(Root, "deploy", "docker", "Caddyfile"));

        Assert.Contains("@revalidate not path /assets/*", caddyfile, StringComparison.Ordinal);
        Assert.Contains("header @revalidate Cache-Control \"no-cache\"", caddyfile, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePostgresServerOnlyAcceptsTlsFromTheNetwork()
    {
        var hba = File.ReadAllLines(Path.Combine(Root, "deploy", "docker", "pg_hba.conf"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
        var conf = File.ReadAllText(Path.Combine(Root, "deploy", "docker", "postgresql.tuned.conf"));

        Assert.DoesNotContain(hba, fields => fields[0] == "host" && fields[3] != "127.0.0.1/32" && fields[3] != "::1/128");
        Assert.Contains(hba, fields => fields[0] == "hostssl" && fields[1] == "all" && fields[3] == "samenet");
        Assert.Contains(hba, fields => fields[0] == "hostssl" && fields[1] == "replication" && fields[3] == "samenet");
        Assert.Equal(RejectPlainTextRule, hba[^1]);
        Assert.Contains("ssl = on", conf, StringComparison.Ordinal);
        Assert.Contains("ssl_min_protocol_version = 'TLSv1.2'", conf, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCertificateKeyIsCopiedToAPrivateFileOfThePostgresUser()
    {
        var block = ServiceBlock(File.ReadAllText(Path.Combine(Root, "compose.yaml")), "postgres");

        Assert.Contains("install -o postgres -g postgres -m 600 /run/secrets/db_tls_key /run/alkaros-tls/server.key", block, StringComparison.Ordinal);
        Assert.Contains("secrets: [db_password, db_tls_cert, db_tls_key]", block, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("api", "serve --db-url postgresql://alkaros_app@postgres")]
    [InlineData("connector", "ConnectorHost.dll --db-url postgresql://alkaros_app@postgres")]
    public void TheApplicationAndTheConnectorRunAsTheLowPrivilegeRoleWithoutCapabilities(string service, string command)
    {
        var block = ServiceBlock(File.ReadAllText(Path.Combine(Root, "compose.yaml")), service);

        Assert.Contains(command, block, StringComparison.Ordinal);
        Assert.Contains("/run/secrets/app_db_password", block, StringComparison.Ordinal);
        Assert.DoesNotContain("/run/secrets/db_password", block, StringComparison.Ordinal);
        Assert.Contains("cap_drop: [ALL]", block, StringComparison.Ordinal);
        Assert.Contains("no-new-privileges:true", block, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheMigrationAndOperationServicesUseTheOwnerRole()
    {
        var compose = File.ReadAllText(Path.Combine(Root, "compose.yaml"));
        var ops = File.ReadAllText(Path.Combine(Root, "compose.ops.yaml"));

        foreach (var service in new[] { "migrate", "provision" })
            Assert.Contains("postgresql://alkaros@postgres", ServiceBlock(compose, service), StringComparison.Ordinal);
        foreach (var service in new[] { "housekeeping", "audit-disposal" })
            Assert.Contains("postgresql://alkaros@postgres", ServiceBlock(ops, service), StringComparison.Ordinal);
        Assert.Contains("ALKAROS_RESTORE_DB_USER: alkaros_restore", ServiceBlock(compose, "api"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("compose.yaml", "migrate")]
    [InlineData("compose.yaml", "provision")]
    [InlineData("compose.yaml", "api")]
    [InlineData("compose.yaml", "connector")]
    [InlineData("compose.ops.yaml", "backup")]
    [InlineData("compose.ops.yaml", "basebackup")]
    [InlineData("compose.ops.yaml", "housekeeping")]
    [InlineData("compose.ops.yaml", "audit-disposal")]
    public void EveryDatabaseClientServiceVerifiesTheServerCertificate(string file, string service)
    {
        var text = File.ReadAllText(Path.Combine(Root, file)).Replace("\r\n", "\n");
        var block = ServiceBlock(text, service);

        Assert.Contains("<<: *db-tls-environment", block, StringComparison.Ordinal);
        Assert.Contains("db_tls_ca", block, StringComparison.Ordinal);
        var anchor = text[text.IndexOf("x-db-tls-environment:", StringComparison.Ordinal)..text.IndexOf("\nservices:", StringComparison.Ordinal)];
        Assert.Contains("ALKAROS_DB_SSL_MODE: verify-full", anchor, StringComparison.Ordinal);
        Assert.Contains("PGSSLMODE: verify-full", anchor, StringComparison.Ordinal);
        Assert.Contains("PGSSLROOTCERT: /run/secrets/db_tls_ca", anchor, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRuntimeRolesAreGrantedAfterEveryMigrationRun()
    {
        var migrate = ServiceBlock(File.ReadAllText(Path.Combine(Root, "compose.yaml")), "migrate");

        Assert.Contains("./deploy/docker/roles.sql:/roles.sql:ro", migrate, StringComparison.Ordinal);
        Assert.True(
            migrate.IndexOf("dotnet ALKAROS.Host.dll", StringComparison.Ordinal) < migrate.IndexOf("-f /roles.sql", StringComparison.Ordinal),
            "the roles script has to run after the migrations");
    }

    [Fact]
    public void TheRolesScriptKeepsTheApplicationRoleUnprivilegedAndOutOfTheMigrationHistory()
    {
        var script = File.ReadAllText(Path.Combine(Root, "deploy", "docker", "roles.sql")).Replace("\r\n", "\n");

        Assert.Contains("LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',\n              :'app_role'", script, StringComparison.Ordinal);
        Assert.Contains("LOGIN NOSUPERUSER CREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',\n              :'restore_role'", script, StringComparison.Ordinal);
        Assert.Contains("REVOKE ALL ON %s FROM %I", script, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTlsScriptWritesACertificateForThePostgresHostAndRefusesToOverwriteIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"alkaros-tls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var script = ToShellPath(Path.Combine(Root, "deploy", "docker", "generate-db-tls.sh"));
            var first = RunShell($"sh '{script}' '{ToShellPath(directory)}'");
            var second = RunShell($"sh '{script}' '{ToShellPath(directory)}'");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(5, second.ExitCode);
            Assert.Contains("refusing to overwrite", second.Error, StringComparison.Ordinal);
            using var ca = new X509Certificate2(Path.Combine(directory, "db_tls_ca.crt"));
            using var server = new X509Certificate2(Path.Combine(directory, "db_tls.crt"));
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            Assert.True(chain.Build(server), string.Join(", ", chain.ChainStatus.Select(status => status.StatusInformation)));
            Assert.Equal("postgres", server.GetNameInfo(X509NameType.DnsName, forIssuer: false));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory))
                File.Delete(file);
            Directory.Delete(directory);
        }
    }

    // A service's own lines in a compose file: from "  name:" up to the next two-space key or top-level key.
    private static string ServiceBlock(string compose, string service)
    {
        var lines = compose.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line => line == $"  {service}:");
        Assert.True(start >= 0, $"service {service} not found");
        var end = start + 1;
        while (end < lines.Length && !StartsNextBlock(lines[end]))
            end++;
        return string.Join('\n', lines[start..end]);
    }

    private static bool StartsNextBlock(string line)
        => line.Length > 0 && line[0] != ' ' && line[0] != '#'
            || line.StartsWith("  ", StringComparison.Ordinal) && line.Length > 2 && line[2] != ' ' && line[2] != '#';

    private static string ArchiveCommand()
    {
        var line = File.ReadAllLines(Path.Combine(Root, "deploy", "docker", "postgresql.tuned.conf"))
            .Single(l => l.StartsWith("archive_command", StringComparison.Ordinal));
        var start = line.IndexOf('\'', StringComparison.Ordinal) + 1;
        return line[start..line.LastIndexOf('\'')];
    }

    private static string ToShellPath(string path) => path.Replace('\\', '/');

    private static (int ExitCode, string Error) RunShell(string script)
    {
        var shell = FindShell();
        var info = new ProcessStartInfo(shell)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, error);
    }

    private static string FindShell()
    {
        var candidates = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "sh.exe" : "sh"))
            .Append(@"C:\Program Files\Git\usr\bin\sh.exe")
            .Append("/bin/sh");
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("No POSIX shell found; the deploy script tests need sh.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
