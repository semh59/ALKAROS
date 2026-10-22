using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// Only <see cref="PushSubscriptionStore"/> may resolve the envelope master key or open the stored VAPID
/// private key. It is not registered as the shared access-policy singleton: each store that protects
/// secrets builds its own resolver/cipher/protector chain around its own policy, because one Host holds a
/// single shared policy instance that only recognises its own accessor.
/// </summary>
public sealed class PushVapidAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "notifications.push-vapid-key-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
