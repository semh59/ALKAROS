using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;

namespace ALKAROS.Host;

/// <summary>
/// Entry point of the ALKAROS host application. The host executable entry
/// point is exposed as a callable <c>Main</c> returning the process exit code;
/// the executable packaging is owned by the release tasks.
/// Exit codes: 0 = success, 1 = migration execution failed, 2 = startup or
/// validation failed.
/// </summary>
public static class Program
{
    private const string PasswordEnvironmentVariable = "ALKAROS_DB_PASSWORD";

    public static int Main(string[] args)
    {
        var options = ParseArguments(args);
        if (options is null)
        {
            PrintUsage(Console.Error);
            return (int)HostExitCode.StartupFailed;
        }

        try
        {
            var exitCode = HostComposition.Run(options, Console.Out);
            Console.Out.WriteLine($"exit: {(int)exitCode}");
            return (int)exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            return (int)HostExitCode.StartupFailed;
        }
    }

    private static HostCompositionOptions? ParseArguments(string[] args)
    {
        string? manifestPath = null;
        string? migrationsDirectory = null;
        string? databaseUrl = null;
        string? psqlExecutable = null;
        string? rollbackId = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--order-manifest" when i + 1 < args.Length:
                    if (manifestPath is not null) return null;
                    manifestPath = args[++i];
                    break;
                case "--migrations-dir" when i + 1 < args.Length:
                    if (migrationsDirectory is not null) return null;
                    migrationsDirectory = args[++i];
                    break;
                case "--db-url" when i + 1 < args.Length:
                    if (databaseUrl is not null) return null;
                    databaseUrl = args[++i];
                    break;
                case "--psql" when i + 1 < args.Length:
                    if (psqlExecutable is not null) return null;
                    psqlExecutable = args[++i];
                    break;
                case "--rollback" when i + 1 < args.Length:
                    if (rollbackId is not null) return null;
                    rollbackId = args[++i];
                    break;
                default:
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath)
            || string.IsNullOrWhiteSpace(migrationsDirectory)
            || string.IsNullOrWhiteSpace(databaseUrl))
            return null;

        if (rollbackId is not null && !IsPosition(rollbackId))
            return null;

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(password))
            return null;

        return new HostCompositionOptions(
            manifestPath,
            migrationsDirectory,
            new PsqlOptions(
                databaseUrl,
                psqlExecutable ?? "psql",
                password),
            rollbackId);
    }

    private static bool IsPosition(string value)
        => value.Length == 3 && value.All(char.IsAsciiDigit);

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: ALKAROS.Host --order-manifest <path> --migrations-dir <path> --db-url <url> [--psql <path>] [--rollback <position>]");
        writer.WriteLine("  --order-manifest  Path to database/MigrationComposition/order.json");
        writer.WriteLine("  --migrations-dir  Directory scanned for <NNN>-<name>.up.sql / .down.sql files");
        writer.WriteLine("  --db-url          PostgreSQL connection URL (e.g. postgresql://user@host:5432/db)");
        writer.WriteLine("  --psql            psql executable path (default: psql from PATH)");
        writer.WriteLine("  --rollback        Run the rollback script of the given position instead of forward");
        writer.WriteLine("  Database password is read from the ALKAROS_DB_PASSWORD environment variable.");
    }
}
