using System.Reflection;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Composition.Modules;

namespace ALKAROS.Host.Composition;

public enum HostExitCode
{
    Success = 0,
    MigrationFailed = 1,
    StartupFailed = 2,
}

public sealed record HostCompositionOptions(
    string OrderManifestPath,
    string MigrationsDirectory,
    PsqlOptions Psql,
    string? RollbackMigrationId = null);

/// <summary>
/// The single composition surface of the executable host: composes the
/// registered modules, validates the discovered migration set against the
/// verified global order, and executes forward or rollback scripts through
/// psql. Any validation finding or module failure prevents SQL execution
/// (fail-closed); a mid-run migration failure is reported with the applied
/// and failed positions and produces a non-zero exit code.
/// </summary>
public static class HostComposition
{
    public static HostExitCode Run(
        HostCompositionOptions options,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var moduleIds = ComposeModules(output);
        if (moduleIds is null)
            return HostExitCode.StartupFailed;

        var manifest = LoadManifest(options.OrderManifestPath, output);
        if (manifest is null)
            return HostExitCode.StartupFailed;

        var discovery = DiscoverMigrations(options.MigrationsDirectory, output);
        if (discovery is null)
            return HostExitCode.StartupFailed;

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);
        if (findings.Count > 0)
        {
            output.WriteLine($"Migration composition validation failed ({findings.Count} finding(s)); nothing was executed:");
            foreach (var finding in findings)
                output.WriteLine($"  {finding}");
            return HostExitCode.StartupFailed;
        }

        output.WriteLine(
            $"Migration composition validated: {manifest.Migrations.Count} position(s), " +
            $"{discovery.Files.Count} script(s).");

        if (options.RollbackMigrationId is not null)
            return RunRollback(manifest, discovery, options, output, cancellationToken);

        return RunForward(manifest, discovery, options, output, cancellationToken);
    }

    private static HostExitCode RunForward(
        MigrationManifest manifest,
        MigrationDiscoveryResult discovery,
        HostCompositionOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var forwardFiles = discovery.Files
            .Where(f => f.Kind == MigrationScriptKind.Up)
            .ToDictionary(f => f.Id, StringComparer.Ordinal);

        var result = MigrationExecutor.ApplyAsync(manifest, forwardFiles, options.Psql, cancellationToken)
            .GetAwaiter()
            .GetResult();

        foreach (var step in result.Steps)
        {
            output.WriteLine(step.Success
                ? $"  [{step.Id}] applied ({step.FilePath})"
                : $"  [{step.Id}] FAILED ({step.FilePath})");
            if (!step.Success && !string.IsNullOrWhiteSpace(step.StandardError))
                output.WriteLine($"      {step.StandardError.Trim()}");
        }

        if (!result.AllApplied)
        {
            var failed = result.FailedStep!;
            output.WriteLine(
                $"Migration failed at position [{failed.Id}]: {result.AppliedIds.Count} applied, " +
                "partial success is not hidden.");
            return HostExitCode.MigrationFailed;
        }

        output.WriteLine(
            $"All {manifest.Migrations.Count} migration(s) verified; {result.AppliedIds.Count} applied.");
        return HostExitCode.Success;
    }

    private static HostExitCode RunRollback(
        MigrationManifest manifest,
        MigrationDiscoveryResult discovery,
        HostCompositionOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var rollbackId = options.RollbackMigrationId!;
        if (manifest.Migrations.All(entry => entry.Id != rollbackId))
        {
            output.WriteLine($"Rollback refused: position [{rollbackId}] is not declared in the verified order.");
            return HostExitCode.StartupFailed;
        }

        var rollbackFile = discovery.Files.SingleOrDefault(
            f => f.Kind == MigrationScriptKind.Down && f.Id == rollbackId);
        if (rollbackFile is null)
        {
            output.WriteLine($"Rollback refused: no rollback script declares position [{rollbackId}].");
            return HostExitCode.StartupFailed;
        }

        var forwardFiles = discovery.Files
            .Where(f => f.Kind == MigrationScriptKind.Up)
            .ToDictionary(f => f.Id, StringComparer.Ordinal);
        var step = MigrationExecutor.RollbackAppliedAsync(
                manifest,
                forwardFiles,
                rollbackId,
                rollbackFile,
                options.Psql,
                cancellationToken)
            .GetAwaiter()
            .GetResult();

        output.WriteLine(step.Success
            ? $"  [{step.Id}] rolled back ({step.FilePath})"
            : $"  [{step.Id}] ROLLBACK FAILED ({step.FilePath})");
        if (!step.Success && !string.IsNullOrWhiteSpace(step.StandardError))
            output.WriteLine($"      {step.StandardError.Trim()}");

        return step.Success
            ? HostExitCode.Success
            : step.StandardError.StartsWith("Rollback refused:", StringComparison.Ordinal)
                ? HostExitCode.StartupFailed
                : HostExitCode.MigrationFailed;
    }

    private static IReadOnlyList<string>? ComposeModules(TextWriter output)
    {
        try
        {
            var hostAssembly = typeof(HostComposition).Assembly;
            var assemblies = new List<Assembly> { hostAssembly };
            foreach (var name in hostAssembly.GetReferencedAssemblies())
            {
                if (name.Name?.StartsWith("ALKAROS.", StringComparison.Ordinal) == true)
                    assemblies.Add(Assembly.Load(name));
            }

            var moduleIds = ModuleRegistry.Compose(ModuleRegistry.Discover(assemblies));
            output.WriteLine(moduleIds.Count == 0
                ? "Modules composed: none registered."
                : $"Modules composed ({moduleIds.Count}): {string.Join(", ", moduleIds)}.");
            return moduleIds;
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or ReflectionTypeLoadException
            or FileNotFoundException
            or BadImageFormatException)
        {
            output.WriteLine($"Module composition failed: {ex.Message}");
            return null;
        }
    }

    private static MigrationManifest? LoadManifest(string path, TextWriter output)
    {
        try
        {
            return MigrationManifest.Load(path);
        }
        catch (MigrationManifestException ex)
        {
            output.WriteLine($"Migration manifest error: {ex.Message}");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"Migration manifest error: {ex.Message}");
            return null;
        }
    }

    private static MigrationDiscoveryResult? DiscoverMigrations(string directory, TextWriter output)
    {
        try
        {
            return MigrationDiscoverer.Discover(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"Migration discovery error: {ex.Message}");
            return null;
        }
    }
}
