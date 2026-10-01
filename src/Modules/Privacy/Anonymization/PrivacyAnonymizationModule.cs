using ALKAROS.ModuleComposition;
using ALKAROS.Privacy.RetentionExecution;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Privacy.Anonymization;

/// <summary>
/// Registers the KVKK anonymization workflow. It consumes the work items of <c>Privacy.RetentionExecution</c>; the field
/// actions (<see cref="IAnonymizationPlans"/>) come from the composition root, so this module writes only its own schema.
/// </summary>
public sealed class PrivacyAnonymizationModule : IModule
{
    public string Id => "Privacy.Anonymization";
    public string DisplayName => "Privacy - Anonymization";

    public IReadOnlyCollection<string> DependsOn => ["Privacy.RetentionExecution"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IAnonymizationWorkflow>(provider => new PostgresAnonymizationWorkflow(
            provider.GetRequiredService<NpgsqlDataSource>(),
            provider.GetRequiredService<IRetentionExecutionService>(),
            provider.GetRequiredService<IAnonymizationPlans>()));
}
