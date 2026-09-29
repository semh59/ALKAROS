using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Invoicing.Generation;

/// <summary>
/// V14-INV-002: the invoice's buyer snapshot is invoice data (V0-CMP-003: customer name and tax ID, Manager/Finance,
/// kept 10 years) and is stored encrypted like the customer profile it is copied from. Same private-chain pattern as
/// <c>QnbCredentialAccessPolicy</c>: deliberately not registered as the shared policy singletons.
/// </summary>
public sealed class InvoiceBuyerEncryptionPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "invoicing.generation";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
