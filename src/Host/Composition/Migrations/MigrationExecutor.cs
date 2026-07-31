namespace ALKAROS.Host.Composition.Migrations;

/// <summary>
/// Outcome of executing one migration script. Failure output is retained so
/// partial success is never hidden from the operator.
/// </summary>
public sealed record MigrationStepResult(
    string Id,
    string FilePath,
    bool Success,
    string StandardOutput,
    string StandardError);

/// <summary>
/// Outcome of a forward migration run. Execution stops at the first failed
/// step; the step list always contains every executed step, applied or not.
/// </summary>
public sealed class ForwardMigrationResult
{
    public ForwardMigrationResult(IReadOnlyList<MigrationStepResult> steps)
    {
        Steps = steps;
    }

    public IReadOnlyList<MigrationStepResult> Steps { get; }

    public bool AllApplied => Steps.Count > 0 && Steps.All(step => step.Success);

    public IReadOnlyList<string> AppliedIds => Steps.Where(step => step.Success).Select(step => step.Id).ToList();

    public MigrationStepResult? FailedStep => Steps.FirstOrDefault(step => !step.Success);
}

/// <summary>
/// Executes migration scripts in the verified manifest order through psql.
/// The executor trusts that the caller already validated the discovered set
/// against the manifest: positions are executed exactly as declared.
/// </summary>
public static class MigrationExecutor
{
    public static async Task<ForwardMigrationResult> ApplyAsync(
        MigrationManifest manifest,
        IReadOnlyDictionary<string, MigrationFile> forwardFiles,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var steps = new List<MigrationStepResult>(manifest.Migrations.Count);

        foreach (var entry in manifest.Migrations)
        {
            var file = forwardFiles[entry.Id];
            var result = await PsqlScriptRunner.RunAsync(file.Path, options, cancellationToken).ConfigureAwait(false);
            steps.Add(new MigrationStepResult(
                entry.Id, file.Path, result.Success, result.StandardOutput, result.StandardError));
            if (!result.Success)
                break;
        }

        return new ForwardMigrationResult(steps);
    }

    public static async Task<MigrationStepResult> RollbackAsync(
        string migrationId,
        MigrationFile rollbackFile,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var result = await PsqlScriptRunner.RunAsync(rollbackFile.Path, options, cancellationToken).ConfigureAwait(false);
        return new MigrationStepResult(
            migrationId, rollbackFile.Path, result.Success, result.StandardOutput, result.StandardError);
    }
}
