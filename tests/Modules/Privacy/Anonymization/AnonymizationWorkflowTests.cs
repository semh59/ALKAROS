using ALKAROS.Privacy.Anonymization.Tests.Fixtures;
using ALKAROS.Privacy.RetentionExecution;
using Npgsql;
using Xunit;

namespace ALKAROS.Privacy.Anonymization.Tests;

/// <summary>
/// On a real PostgreSQL with every migration, with a plan that clears two stores (columns a and b of a scratch table): each
/// store is checkpointed, an interrupted job resumes at the store that stopped, a repeat changes nothing, a legal hold or a
/// block keeps a record back, and the final check refuses a record that still carries personal data.
/// </summary>
public sealed class AnonymizationWorkflowTests : IAsyncLifetime
{
    private readonly AnonymizationTestDatabase _database = new();
    private PostgresRetentionExecutionService _retention = null!;
    private PostgresAnonymizationWorkflow _workflow = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _retention = new PostgresRetentionExecutionService(_database.DataSource);
        _workflow = new PostgresAnonymizationWorkflow(_database.DataSource, _retention, new ScratchPlans());
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private Task<T> Scalar<T>(string sql) => _database.ScalarAsync<T>(sql);

    private async Task<Guid> QueuedRecordAsync(bool blocked = false)
    {
        var id = await _database.SeedRecordAsync(blocked);
        await _database.QueueAsync("Supplier", id);
        return id;
    }

    private Task<AnonymizationRunResult> RunAsync(IReadOnlySet<Guid>? skip = null)
        => _workflow.RunAsync(RetentionClass.Supplier, "test", skip);

    private Task<long> PendingItems(Guid id) =>
        Scalar<long>($"SELECT count(*) FROM privacy.retention_work_items WHERE subject_id = '{id}' AND status = 'Pending'");

    [Fact]
    public async Task EachStoreIsClearedWithOneCheckpointAndOneEventAndTheWorkItemIsCompleted()
    {
        var id = await QueuedRecordAsync();

        var result = await RunAsync();

        Assert.Equal((1, 0, 0), (result.Completed, result.Blocked, result.Failed));
        Assert.Equal(1, result.FieldsChangedByStore["scratch.a"]);
        Assert.Equal(1, result.FieldsChangedByStore["scratch.b"]);
        Assert.True(await Scalar<bool>($"SELECT a IS NULL AND b IS NULL FROM public.anon_scratch WHERE id = '{id}'"));
        Assert.Equal(2L, await Scalar<long>($"SELECT count(*) FROM privacy.anonymization_checkpoints c JOIN privacy.anonymization_jobs j USING (job_id) WHERE j.subject_id = '{id}'"));
        Assert.Equal(
            ["StepDone", "StepDone", "Completed"],
            await Strings($"SELECT event_type FROM privacy.anonymization_events e JOIN privacy.anonymization_jobs j USING (job_id) WHERE j.subject_id = '{id}' ORDER BY event_id"));
        Assert.Equal("Done", await Scalar<string>($"SELECT status FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal(1L, await Scalar<long>($"SELECT count(*) FROM privacy.retention_work_items WHERE subject_id = '{id}' AND status = 'Done'"));
    }

    [Fact]
    public async Task AJobThatStoppedHalfwayResumesAtTheUnfinishedStoreWithoutRepeatingTheFinishedOne()
    {
        var id = await QueuedRecordAsync();
        await _database.RunSqlAsync(
            """
            CREATE FUNCTION public.fail_b_update() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'disk full' USING ERRCODE = '53100'; END; $$;
            CREATE TRIGGER fail_b_update BEFORE UPDATE OF b ON public.anon_scratch FOR EACH ROW EXECUTE FUNCTION public.fail_b_update();
            """);

        var first = await RunAsync();

        Assert.Equal((0, 0, 1), (first.Completed, first.Blocked, first.Failed));
        Assert.True(await Scalar<bool>($"SELECT a IS NULL AND b IS NOT NULL FROM public.anon_scratch WHERE id = '{id}'"));
        Assert.Equal(1L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_checkpoints"));
        Assert.Equal("Failed", await Scalar<string>($"SELECT status FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal("53100", await Scalar<string>($"SELECT last_error FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal(1L, await PendingItems(id));

        await _database.RunSqlAsync("DROP TRIGGER fail_b_update ON public.anon_scratch;");
        var second = await RunAsync();

        Assert.Equal((1, 0, 0), (second.Completed, second.Blocked, second.Failed));
        Assert.False(second.FieldsChangedByStore.ContainsKey("scratch.a"));
        Assert.Equal(1, second.FieldsChangedByStore["scratch.b"]);
        Assert.Equal(2, await Scalar<int>($"SELECT attempts FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal(2L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_checkpoints"));
        Assert.Equal(0L, await PendingItems(id));
    }

    [Fact]
    public async Task RunningAgainChangesNothing()
    {
        await QueuedRecordAsync();
        await RunAsync();
        var events = await Scalar<long>("SELECT count(*) FROM privacy.anonymization_events");

        var again = await RunAsync();

        Assert.Equal((0, 0, 0), (again.Completed, again.Blocked, again.Failed));
        Assert.Equal(events, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_events"));
        Assert.Equal(1L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_jobs"));
    }

    [Fact]
    public async Task ALegalHoldKeepsTheRecordUntouchedAndItsWorkItemPending()
    {
        var id = await QueuedRecordAsync();
        await _retention.PlaceHoldAsync(RetentionClass.Supplier, id, "Vergi incelemesi", Guid.NewGuid());

        var result = await RunAsync();

        Assert.Equal((0, 0, 0), (result.Completed, result.Blocked, result.Failed));
        Assert.True(await Scalar<bool>($"SELECT a IS NOT NULL AND b IS NOT NULL FROM public.anon_scratch WHERE id = '{id}'"));
        Assert.Equal(1L, await PendingItems(id));
    }

    [Fact]
    public async Task AHoldPlacedWhileTheJobRunsStopsItBeforeTheWorkItemIsCompleted()
    {
        var id = await QueuedRecordAsync();
        // The hold appears after the records were listed: completing the retention work item is then refused.
        await _database.RunSqlAsync(
            $"""
            CREATE FUNCTION public.place_hold() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by)
                VALUES (gen_random_uuid(), 'Supplier', '{id}', 'late hold', gen_random_uuid()) ON CONFLICT DO NOTHING;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER place_hold AFTER UPDATE ON public.anon_scratch FOR EACH ROW EXECUTE FUNCTION public.place_hold();
            """);

        var result = await _workflow.RunAsync(RetentionClass.Supplier, "test");

        Assert.Equal((0, 1, 0), (result.Completed, result.Blocked, result.Failed));
        Assert.Equal("Blocked", await Scalar<string>($"SELECT status FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal("legal hold", await Scalar<string>($"SELECT last_error FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal(1L, await PendingItems(id));
    }

    [Fact]
    public async Task ARecordTheClassMustHoldBackIsNotWrittenUntilItIsReleased()
    {
        var id = await QueuedRecordAsync(blocked: true);

        var held = await RunAsync();

        Assert.Equal((0, 1, 0), (held.Completed, held.Blocked, held.Failed));
        Assert.True(await Scalar<bool>($"SELECT a IS NOT NULL AND b IS NOT NULL FROM public.anon_scratch WHERE id = '{id}'"));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_checkpoints"));
        Assert.Equal(1L, await PendingItems(id));

        await _database.RunSqlAsync($"UPDATE public.anon_scratch SET blocked = false WHERE id = '{id}';");
        var released = await RunAsync();

        Assert.Equal(1, released.Completed);
    }

    [Fact]
    public async Task ARecordThatStillCarriesPersonalDataAfterItsStepsIsNeverMarkedDone()
    {
        var id = await QueuedRecordAsync();
        // Something re-fills column a right after the clearing write, so the final check finds it.
        await _database.RunSqlAsync(
            """
            CREATE FUNCTION public.refill_a() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN NEW.a := 'Ahmet Bey'; RETURN NEW; END; $$;
            CREATE TRIGGER refill_a BEFORE UPDATE ON public.anon_scratch FOR EACH ROW EXECUTE FUNCTION public.refill_a();
            """);

        var result = await RunAsync();

        Assert.Equal((0, 0, 1), (result.Completed, result.Blocked, result.Failed));
        Assert.Equal("residue", await Scalar<string>($"SELECT last_error FROM privacy.anonymization_jobs WHERE subject_id = '{id}'"));
        Assert.Equal(1L, await PendingItems(id));
        Assert.Equal(1L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_events WHERE event_type = 'VerificationFailed' AND step_key = 'scratch.a'"));
    }

    [Fact]
    public async Task ARecordTheCallerSkipsIsLeftAlone()
    {
        var skipped = await QueuedRecordAsync();
        var other = await QueuedRecordAsync();

        var result = await RunAsync(new HashSet<Guid> { skipped });

        Assert.Equal(1, result.Completed);
        Assert.True(await Scalar<bool>($"SELECT a IS NOT NULL FROM public.anon_scratch WHERE id = '{skipped}'"));
        Assert.True(await Scalar<bool>($"SELECT a IS NULL FROM public.anon_scratch WHERE id = '{other}'"));
        Assert.Equal(1L, await PendingItems(skipped));
    }

    [Fact]
    public async Task TheTrailIsAppendOnlyAndHoldsNoPersonalData()
    {
        await QueuedRecordAsync();
        await RunAsync();

        await Assert.ThrowsAsync<PostgresException>(() => _database.RunSqlAsync("UPDATE privacy.anonymization_events SET detail = 'x';"));
        await Assert.ThrowsAsync<PostgresException>(() => _database.RunSqlAsync("DELETE FROM privacy.anonymization_checkpoints;"));
        await Assert.ThrowsAsync<PostgresException>(() => _database.RunSqlAsync("DELETE FROM privacy.anonymization_jobs;"));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM privacy.anonymization_events WHERE detail ILIKE '%Ahmet%' OR detail ILIKE '%0500%'"));
    }

    private async Task<string[]> Strings(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();
        var list = new List<string>();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list.ToArray();
    }

    private sealed class ScratchPlans : IAnonymizationPlans
    {
        public AnonymizationPlan PlanFor(RetentionClass dataClass) => new(
            "SELECT blocked FROM public.anon_scratch WHERE id = @subject;",
            [
                new("scratch.a", "UPDATE public.anon_scratch SET a = NULL WHERE id = @subject AND a IS NOT NULL;",
                    "SELECT count(*) FROM public.anon_scratch WHERE id = @subject AND a IS NOT NULL;"),
                new("scratch.b", "UPDATE public.anon_scratch SET b = NULL WHERE id = @subject AND b IS NOT NULL;",
                    "SELECT count(*) FROM public.anon_scratch WHERE id = @subject AND b IS NOT NULL;"),
            ],
            new Dictionary<string, object>());
    }
}
