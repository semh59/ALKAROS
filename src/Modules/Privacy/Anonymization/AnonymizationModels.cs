using ALKAROS.Privacy.RetentionExecution;

namespace ALKAROS.Privacy.Anonymization;

/// <summary>What one run did: how many records finished, were held back, or stopped on an error, and the fields it wrote per store.</summary>
public sealed record AnonymizationRunResult(
    int Completed, int Blocked, int Failed, IReadOnlyDictionary<string, int> FieldsChangedByStore);

/// <summary>
/// One store a data class touches. <paramref name="ApplySql"/> clears or masks the fields of the record <c>@subject</c> and
/// changes nothing when repeated; <paramref name="ResidueSql"/> counts what a finished step should have cleared (0 = clean).
/// </summary>
public sealed record AnonymizationStep(string Key, string ApplySql, string ResidueSql);

/// <summary>
/// The stores of one data class. <paramref name="BlockSql"/>, when set, returns true while the record must wait.
/// <paramref name="Parameters"/> are bound to every statement that mentions them (<c>@name</c>).
/// </summary>
public sealed record AnonymizationPlan(
    string? BlockSql, IReadOnlyList<AnonymizationStep> Steps, IReadOnlyDictionary<string, object> Parameters);

/// <summary>
/// The field actions the workflow runs. The composition root supplies them because they write other modules' stores,
/// which a module does not do itself.
/// </summary>
public interface IAnonymizationPlans
{
    AnonymizationPlan PlanFor(RetentionClass dataClass);
}
