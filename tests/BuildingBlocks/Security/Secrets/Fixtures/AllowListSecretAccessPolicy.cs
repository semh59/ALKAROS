using ALKAROS.Secrets;

namespace ALKAROS.Secrets.Tests.Fixtures;

/// <summary>
/// Test policy that grants an accessor read permission for an explicit set
/// of secret names. Any accessor or secret not listed is denied.
/// </summary>
public sealed class AllowListSecretAccessPolicy : ISecretAccessPolicy
{
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _allowed;

    public AllowListSecretAccessPolicy(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> allowed)
    {
        _allowed = allowed ?? throw new ArgumentNullException(nameof(allowed));
    }

    public bool IsAllowed(string accessor, SecretReference reference) =>
        _allowed.TryGetValue(accessor, out var names)
        && names.Contains(reference.Name, StringComparer.Ordinal);
}
