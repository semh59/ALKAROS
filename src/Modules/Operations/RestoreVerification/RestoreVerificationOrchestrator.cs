using System.Diagnostics;
using System.Text;
using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// Ties a restore drill together: pick the latest off-site artifact for a
/// data class, decrypt-and-verify it (V15-BKP-001's own boundary — a
/// corrupted or undecryptable artifact fails here, before any isolated
/// database is ever provisioned), apply it to a throwaway PostgreSQL
/// database, run integrity queries, run an application smoke check,
/// measure the whole thing against the approved RTO, and record the
/// attempt whether it succeeded or not.
/// </summary>
public sealed class RestoreVerificationOrchestrator
{
    private readonly IOffsiteBackupReceiptStore _receiptStore;
    private readonly OffsiteBackupRestoreVerificationService _downloadVerify;
    private readonly IIsolatedRestoreDatabaseFactory _databaseFactory;
    private readonly IRestoreAttemptStore _attemptStore;
    private readonly IReadOnlyList<IntegrityCheck> _integrityChecks;

    public RestoreVerificationOrchestrator(
        IOffsiteBackupReceiptStore receiptStore,
        OffsiteBackupRestoreVerificationService downloadVerify,
        IIsolatedRestoreDatabaseFactory databaseFactory,
        IRestoreAttemptStore attemptStore,
        IReadOnlyList<IntegrityCheck> integrityChecks)
    {
        _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
        _downloadVerify = downloadVerify ?? throw new ArgumentNullException(nameof(downloadVerify));
        _databaseFactory = databaseFactory ?? throw new ArgumentNullException(nameof(databaseFactory));
        _attemptStore = attemptStore ?? throw new ArgumentNullException(nameof(attemptStore));
        _integrityChecks = integrityChecks ?? throw new ArgumentNullException(nameof(integrityChecks));
    }

    private static bool IsCustomFormatDump(byte[] content)
        => content.Length >= 5 && content[0] == (byte)'P' && content[1] == (byte)'G'
            && content[2] == (byte)'D' && content[3] == (byte)'M' && content[4] == (byte)'P';

    public async Task<RestoreAttemptRecord> RunAsync(DataClass dataClass, CancellationToken cancellationToken = default)
    {
        var receipts = await _receiptStore.GetByDataClassAsync(dataClass, limit: 1, cancellationToken);
        if (receipts.Count == 0)
            throw new RestoreArtifactNotFoundException(dataClass);
        var receipt = receipts[0];

        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        byte[] plaintext;
        try
        {
            // Throws OffsiteBackupDecryptionFailedException or
            // OffsiteBackupSourceIntegrityException here — before this
            // orchestrator ever provisions a database — for a corrupted or
            // tampered artifact.
            plaintext = await _downloadVerify.DownloadAndVerifyAsync(receipt, cancellationToken);
        }
        catch (Exception ex) when (ex is OffsiteBackupDecryptionFailedException or OffsiteBackupSourceIntegrityException)
        {
            stopwatch.Stop();
            var failed = new RestoreAttemptRecord(
                receipt.ArtifactId, dataClass, startedAtUtc, stopwatch.Elapsed,
                succeeded: false, withinRtoTarget: false,
                integrityChecksPassed: 0, integrityChecksTotal: _integrityChecks.Count,
                failureReason: ex.Message);
            await _attemptStore.RecordAsync(failed, cancellationToken);
            throw;
        }

        await using var database = await _databaseFactory.ProvisionAsync(cancellationToken);

        // Lives outside the try block so a check failure that jumps to the
        // catch below still records how many checks GENUINELY passed before
        // the failure, instead of the misleading fixed 0.
        var passed = 0;
        try
        {
            // backup.sh produces a pg_dump custom-format archive (binary, starts with "PGDMP"); it
            // must go through pg_restore. Anything else is treated as a plain SQL script.
            if (IsCustomFormatDump(plaintext))
                await database.ApplyCustomFormatDumpAsync(plaintext, cancellationToken);
            else
                await database.ApplyScriptAsync(Encoding.UTF8.GetString(plaintext), cancellationToken);

            foreach (var check in _integrityChecks)
            {
                var value = await database.ExecuteScalarAsync(check.SqlQuery, cancellationToken);
                if (!check.Predicate(value))
                    throw new RestoreIntegrityCheckFailedException(check.Name, receipt.ArtifactId);
                passed++;
            }

            // Application smoke check: the restored database must at least
            // answer a trivial query — proves the server accepted the
            // restore and the connection is usable, without starting a
            // full Host.
            var smoke = await database.ExecuteScalarAsync("SELECT 1;", cancellationToken);
            if (smoke is not int and not long || Convert.ToInt64(smoke, System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new RestoreApplicationSmokeCheckFailedException(receipt.ArtifactId);

            stopwatch.Stop();
            var withinRto = stopwatch.Elapsed <= RtoTargets.For(dataClass);
            var succeeded = new RestoreAttemptRecord(
                receipt.ArtifactId, dataClass, startedAtUtc, stopwatch.Elapsed,
                succeeded: true, withinRtoTarget: withinRto,
                integrityChecksPassed: passed, integrityChecksTotal: _integrityChecks.Count,
                failureReason: null);
            await _attemptStore.RecordAsync(succeeded, cancellationToken);
            return succeeded;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var failed = new RestoreAttemptRecord(
                receipt.ArtifactId, dataClass, startedAtUtc, stopwatch.Elapsed,
                succeeded: false, withinRtoTarget: false,
                integrityChecksPassed: passed, integrityChecksTotal: _integrityChecks.Count,
                failureReason: ex.Message);
            await _attemptStore.RecordAsync(failed, cancellationToken);
            throw;
        }
    }
}
