using System.Security.Cryptography;

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

    public bool AllApplied => Steps.All(step => step.Success);

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
        var history = await ReadHistoryAsync(steps, options, cancellationToken).ConfigureAwait(false);
        if (history is null)
            return new ForwardMigrationResult(steps);

        var historyError = ValidateHistoryPrefix(manifest, history);
        if (historyError is not null)
        {
            steps.Add(FailedHistoryStep(historyError));
            return new ForwardMigrationResult(steps);
        }

        foreach (var entry in manifest.Migrations)
        {
            var file = forwardFiles[entry.Id];
            string checksum;
            try
            {
                checksum = ComputeChecksum(file.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                steps.Add(new MigrationStepResult(entry.Id, file.Path, false, string.Empty, ex.Message));
                break;
            }

            if (history.TryGetValue(entry.Id, out var applied))
            {
                if (!string.Equals(applied.Checksum, checksum, StringComparison.Ordinal))
                {
                    steps.Add(new MigrationStepResult(
                        entry.Id,
                        file.Path,
                        false,
                        string.Empty,
                        $"Migration [{entry.Id}] checksum differs from its applied history entry."));
                    break;
                }

                continue;
            }

            var result = await PsqlScriptRunner.RunScriptWithCommandAsync(
                file.Path,
                MigrationHistoryStore.BuildInsertCommand(entry.Id, checksum),
                options,
                cancellationToken).ConfigureAwait(false);
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

    public static async Task<MigrationStepResult> RollbackAppliedAsync(
        MigrationManifest manifest,
        IReadOnlyDictionary<string, MigrationFile> forwardFiles,
        string migrationId,
        MigrationFile rollbackFile,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var historySteps = new List<MigrationStepResult>(capacity: 1);
        var history = await ReadHistoryAsync(historySteps, options, cancellationToken).ConfigureAwait(false);
        if (history is null)
            return historySteps.Single();

        var historyError = ValidateHistoryPrefix(manifest, history);
        if (historyError is not null)
            return FailedHistoryStep(historyError);

        if (!history.TryGetValue(migrationId, out var applied))
        {
            return new MigrationStepResult(
                migrationId,
                rollbackFile.Path,
                false,
                string.Empty,
                $"Rollback refused: migration [{migrationId}] is not applied.");
        }

        var migrationIndex = manifest.Migrations
            .Select((entry, index) => (entry, index))
            .Single(pair => pair.entry.Id == migrationId)
            .index;
        if (manifest.Migrations.Skip(migrationIndex + 1).Any(entry => history.ContainsKey(entry.Id)))
        {
            return new MigrationStepResult(
                migrationId,
                rollbackFile.Path,
                false,
                string.Empty,
                $"Rollback refused: migration [{migrationId}] has a later applied position.");
        }

        var forwardFile = forwardFiles[migrationId];
        string checksum;
        try
        {
            checksum = ComputeChecksum(forwardFile.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MigrationStepResult(migrationId, forwardFile.Path, false, string.Empty, ex.Message);
        }

        if (!string.Equals(applied.Checksum, checksum, StringComparison.Ordinal))
        {
            return new MigrationStepResult(
                migrationId,
                forwardFile.Path,
                false,
                string.Empty,
                $"Rollback refused: migration [{migrationId}] checksum differs from its applied history entry.");
        }

        var result = await PsqlScriptRunner.RunScriptWithCommandAsync(
            rollbackFile.Path,
            MigrationHistoryStore.BuildDeleteCommand(migrationId),
            options,
            cancellationToken).ConfigureAwait(false);
        return new MigrationStepResult(
            migrationId, rollbackFile.Path, result.Success, result.StandardOutput, result.StandardError);
    }

    private static async Task<IReadOnlyDictionary<string, AppliedMigration>?> ReadHistoryAsync(
        List<MigrationStepResult> steps,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var ensure = await MigrationHistoryStore.EnsureAsync(options, cancellationToken).ConfigureAwait(false);
        if (!ensure.Success)
        {
            steps.Add(FailedHistoryStep(ensure.StandardError));
            return null;
        }

        var history = await MigrationHistoryStore.ReadAsync(options, cancellationToken).ConfigureAwait(false);
        if (!history.Success)
        {
            steps.Add(FailedHistoryStep(history.StandardError));
            return null;
        }

        return history.Entries;
    }

    private static string? ValidateHistoryPrefix(
        MigrationManifest manifest,
        IReadOnlyDictionary<string, AppliedMigration> history)
    {
        var expectedIds = manifest.Migrations.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var unknownId = history.Keys.FirstOrDefault(id => !expectedIds.Contains(id));
        if (unknownId is not null)
            return $"Migration history contains undeclared position [{unknownId}].";

        var gapFound = false;
        foreach (var entry in manifest.Migrations)
        {
            if (history.ContainsKey(entry.Id))
            {
                if (gapFound)
                    return $"Migration history is not a contiguous prefix at position [{entry.Id}].";
            }
            else
            {
                gapFound = true;
            }
        }

        return null;
    }

    private static MigrationStepResult FailedHistoryStep(string error)
        => new("history", MigrationHistoryStore.TableName, false, string.Empty, error);

    private static string ComputeChecksum(string filePath)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));
}
