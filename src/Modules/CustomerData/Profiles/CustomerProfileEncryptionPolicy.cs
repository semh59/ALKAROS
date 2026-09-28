using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V14-CST-001. Mirrors `ALKAROS.Invoicing.Qnb.CredentialRegistration.
/// QnbCredentialAccessPolicy`'s exact shape and rationale: deliberately NOT
/// registered as the shared `ISecretAccessPolicy`/`ISensitiveDataAccessPolicy`
/// singletons (see that type's own doc comment for why a second/third module
/// registering either interface globally would silently collide with the
/// others). `PostgresCustomerProfileStore` builds its own private resolver/
/// cipher/protector chain around this policy instead.
/// </summary>
public sealed class CustomerProfileEncryptionPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "customerdata.profile-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
