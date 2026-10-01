using ALKAROS.Privacy.RetentionExecution;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Privacy.Anonymization;

public sealed class PostgresAnonymizationWorkflow(
    NpgsqlDataSource dataSource, IRetentionExecutionService retention, IAnonymizationPlans plans)
    : IAnonymizationWorkflow
{
    public async Task<AnonymizationRunResult> RunAsync(
        RetentionClass dataClass,
        string actor,
        IReadOnlySet<Guid>? skip = null,
        int limit = 10_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var plan = plans.PlanFor(dataClass);
        var fieldsByStore = new Dictionary<string, int>();
        int completed = 0, blocked = 0, failed = 0;

        foreach (var item in await retention.PendingAsync(dataClass, limit, cancellationToken))
        {
            if (skip?.Contains(item.SubjectId) == true)
                continue;
            switch (await ProcessAsync(dataClass, plan, item.SubjectId, actor, fieldsByStore, cancellationToken))
            {
                case JobOutcome.Completed: completed++; break;
                case JobOutcome.Blocked: blocked++; break;
                default: failed++; break;
            }
        }

        return new AnonymizationRunResult(completed, blocked, failed, fieldsByStore);
    }

    private enum JobOutcome { Completed, Blocked, Failed }

    private async Task<JobOutcome> ProcessAsync(
        RetentionClass dataClass, AnonymizationPlan plan, Guid subject, string actor, Dictionary<string, int> fieldsByStore,
        CancellationToken cancellationToken)
    {
        var (jobId, alreadyDone) = await OpenJobAsync(dataClass, subject, cancellationToken);

        if (!alreadyDone)
        {
            if (plan.BlockSql is not null && await ScalarAsync<bool>(plan, plan.BlockSql, subject, cancellationToken))
                return await StopAsync(jobId, "Blocked", "Blocked", null, "open balance", actor, JobOutcome.Blocked, cancellationToken);

            var done = await DoneStepsAsync(jobId, cancellationToken);
            foreach (var step in plan.Steps.Where(step => !done.Contains(step.Key)))
            {
                try
                {
                    var changed = await ApplyStepAsync(jobId, plan, step, subject, actor, cancellationToken);
                    fieldsByStore[step.Key] = fieldsByStore.GetValueOrDefault(step.Key) + changed;
                }
                catch (NpgsqlException exception)
                {
                    var reason = exception is PostgresException postgres ? $"{postgres.SqlState}" : exception.GetType().Name;
                    return await StopAsync(jobId, "Failed", "StepFailed", step.Key, reason, actor, JobOutcome.Failed, cancellationToken);
                }
            }

            foreach (var step in plan.Steps)
                if (await ScalarAsync<long>(plan, step.ResidueSql, subject, cancellationToken) != 0)
                    return await StopAsync(jobId, "Failed", "VerificationFailed", step.Key, "residue", actor, JobOutcome.Failed, cancellationToken);
        }

        return await CompleteAsync(dataClass, subject, jobId, alreadyDone, actor, cancellationToken);
    }

    private async Task<(Guid JobId, bool Done)> OpenJobAsync(RetentionClass dataClass, Guid subject, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO privacy.anonymization_jobs (job_id, data_class, subject_id, status)
            VALUES (@id, @class, @subject, 'Running')
            ON CONFLICT (data_class, subject_id) DO UPDATE
                SET attempts = privacy.anonymization_jobs.attempts + CASE WHEN privacy.anonymization_jobs.status = 'Done' THEN 0 ELSE 1 END,
                    status = CASE WHEN privacy.anonymization_jobs.status = 'Done' THEN 'Done' ELSE 'Running' END,
                    updated_at = now()
            RETURNING job_id, status = 'Done';
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = Guid.NewGuid();
        command.Parameters.Add("class", NpgsqlDbType.Text).Value = dataClass.ToString();
        command.Parameters.Add("subject", NpgsqlDbType.Uuid).Value = subject;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetGuid(0), reader.GetBoolean(1));
    }

    private async Task<HashSet<string>> DoneStepsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT step_key FROM privacy.anonymization_checkpoints WHERE job_id = @job;");
        command.Parameters.Add("job", NpgsqlDbType.Uuid).Value = jobId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var done = new HashSet<string>();
        while (await reader.ReadAsync(cancellationToken))
            done.Add(reader.GetString(0));
        return done;
    }

    /// <summary>The write, its checkpoint and its event commit together, so a step is either fully done or not done at all.</summary>
    private async Task<int> ApplyStepAsync(Guid jobId, AnonymizationPlan plan, AnonymizationStep step, Guid subject, string actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int changed;
        await using (var write = Bind(new NpgsqlCommand(step.ApplySql, connection, transaction), plan, step.ApplySql, subject))
            changed = await write.ExecuteNonQueryAsync(cancellationToken);

        await using (var checkpoint = new NpgsqlCommand(
            "INSERT INTO privacy.anonymization_checkpoints (job_id, step_key, fields_changed) VALUES (@job, @step, @changed);",
            connection, transaction))
        {
            checkpoint.Parameters.Add("job", NpgsqlDbType.Uuid).Value = jobId;
            checkpoint.Parameters.Add("step", NpgsqlDbType.Text).Value = step.Key;
            checkpoint.Parameters.Add("changed", NpgsqlDbType.Integer).Value = changed;
            await checkpoint.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendEventAsync(connection, transaction, jobId, "StepDone", step.Key, $"rows={changed}", actor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    private async Task<JobOutcome> StopAsync(
        Guid jobId, string status, string eventType, string? stepKey, string detail, string actor, JobOutcome outcome,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = new NpgsqlCommand(
            "UPDATE privacy.anonymization_jobs SET status = @status, last_error = @detail, updated_at = now() WHERE job_id = @job;",
            connection, transaction))
        {
            update.Parameters.Add("status", NpgsqlDbType.Text).Value = status;
            update.Parameters.Add("detail", NpgsqlDbType.Text).Value = detail;
            update.Parameters.Add("job", NpgsqlDbType.Uuid).Value = jobId;
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendEventAsync(connection, transaction, jobId, eventType, stepKey, detail, actor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    /// <summary>The job is marked done and the retention work item completed in one transaction.</summary>
    private async Task<JobOutcome> CompleteAsync(
        RetentionClass dataClass, Guid subject, Guid jobId, bool alreadyDone, string actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await retention.CompleteAsync(dataClass, [subject], actor, transaction, cancellationToken);
        }
        catch (RetentionSubjectHeldException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await StopAsync(jobId, "Blocked", "Blocked", null, "legal hold", actor, JobOutcome.Blocked, cancellationToken);
        }

        if (!alreadyDone)
        {
            await using (var done = new NpgsqlCommand(
                "UPDATE privacy.anonymization_jobs SET status = 'Done', last_error = NULL, completed_at = now(), updated_at = now() WHERE job_id = @job;",
                connection, transaction))
            {
                done.Parameters.Add("job", NpgsqlDbType.Uuid).Value = jobId;
                await done.ExecuteNonQueryAsync(cancellationToken);
            }

            await AppendEventAsync(connection, transaction, jobId, "Completed", null, null, actor, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return JobOutcome.Completed;
    }

    private static async Task AppendEventAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid jobId, string eventType, string? stepKey, string? detail,
        string actor, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO privacy.anonymization_events (job_id, event_type, step_key, detail, actor) VALUES (@job, @type, @step, @detail, @actor);",
            connection, transaction);
        command.Parameters.Add("job", NpgsqlDbType.Uuid).Value = jobId;
        command.Parameters.Add("type", NpgsqlDbType.Text).Value = eventType;
        command.Parameters.Add("step", NpgsqlDbType.Text).Value = (object?)stepKey ?? DBNull.Value;
        command.Parameters.Add("detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        command.Parameters.Add("actor", NpgsqlDbType.Text).Value = actor;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T> ScalarAsync<T>(AnonymizationPlan plan, string sql, Guid subject, CancellationToken cancellationToken)
    {
        await using var command = Bind(dataSource.CreateCommand(sql), plan, sql, subject);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static NpgsqlCommand Bind(NpgsqlCommand command, AnonymizationPlan plan, string sql, Guid subject)
    {
        command.Parameters.Add("subject", NpgsqlDbType.Uuid).Value = subject;
        foreach (var (name, value) in plan.Parameters)
            if (sql.Contains("@" + name, StringComparison.Ordinal))
                command.Parameters.AddWithValue(name, value);
        return command;
    }
}
