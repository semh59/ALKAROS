using System.Diagnostics;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

public sealed class DeployFilesTests
{
    private static readonly string Root = FindRepositoryRoot();

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
