using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;

/// <summary>
/// Only the webhook inbox itself may resolve the webhook secret (V12-ONL-010: payloads are sealed and opened by
/// the shared ProviderInbox under its own policy). It builds its own resolver/cipher/protector chain around this
/// policy instead of sharing one through DI (the same single-purpose pattern as
/// RelayCredentialAccessPolicy).
/// </summary>
public sealed class YemeksepetiWebhookAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "online-ordering.yemeksepeti-webhook-inbox";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
