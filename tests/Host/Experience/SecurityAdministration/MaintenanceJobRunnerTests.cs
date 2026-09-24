using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.SecurityAdministration.Tests;

/// <summary>
/// V1-RMD-268: the runner must never let one job take the host down, never
/// silently succeed when a job cannot run, and never overlap a job with itself.
/// </summary>
public sealed class MaintenanceJobRunnerTests
{
    private static MaintenanceJobRunner CreateRunner(params IMaintenanceJob[] jobs)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        return new MaintenanceJobRunner(jobs, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MaintenanceJobRunner>.Instance);
    }

    [Fact]
    public async Task ADisabledJobReportsWhyItDidNotRunInsteadOfSucceeding()
    {
        var job = new FakeJob("disabled-job") { Disabled = "Yedek hedefi ayarlanmamış." };
        var runner = CreateRunner(job);

        var status = await runner.RunAsync("disabled-job", CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("Skipped", status.LastStatus);
        Assert.Equal("Yedek hedefi ayarlanmamış.", status.LastSummary);
        Assert.False(status.Enabled);
        Assert.Equal(0, job.Runs);
    }

    [Fact]
    public async Task AThrowingJobIsRecordedAsFailedWithoutLeakingTheMessageOrCrashingTheCaller()
    {
        var runner = CreateRunner(new FakeJob("boom") { Throw = new InvalidOperationException("secret connection string") });

        var status = await runner.RunAsync("boom", CancellationToken.None);

        Assert.Equal("Failed", status!.LastStatus);
        Assert.DoesNotContain("secret connection string", status.LastSummary);
    }

    [Fact]
    public async Task ASecondRunWhileTheFirstIsStillRunningIsSkippedNotOverlapped()
    {
        var gate = new TaskCompletionSource();
        var job = new FakeJob("slow") { Gate = gate.Task };
        var runner = CreateRunner(job);

        var first = runner.RunAsync("slow", CancellationToken.None);
        while (job.Runs == 0)
            await Task.Delay(10);
        var second = await runner.RunAsync("slow", CancellationToken.None);
        gate.SetResult();
        var firstStatus = await first;

        Assert.Equal("Skipped", second!.LastStatus);
        Assert.Equal(1, job.Runs);
        Assert.Equal("Succeeded", firstStatus!.LastStatus);
    }

    [Fact]
    public async Task AnUnknownJobNameReturnsNull()
    {
        Assert.Null(await CreateRunner().RunAsync("nope", CancellationToken.None));
    }

    private sealed class FakeJob(string name) : IMaintenanceJob
    {
        public int Runs { get; private set; }
        public string? Disabled { get; init; }
        public Exception? Throw { get; init; }
        public Task? Gate { get; init; }

        public string Name => name;
        public string Description => "test";
        public TimeSpan Interval => TimeSpan.FromHours(1);
        public string? DisabledReason => Disabled;

        public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
        {
            Runs++;
            if (Gate is not null)
                await Gate;
            if (Throw is not null)
                throw Throw;
            return new MaintenanceJobOutcome(MaintenanceJobStatusKind.Succeeded, "ok");
        }
    }
}
