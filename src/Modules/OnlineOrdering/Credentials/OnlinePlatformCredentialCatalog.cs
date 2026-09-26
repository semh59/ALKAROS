namespace ALKAROS.OnlineOrdering.Credentials;

/// <summary>One setting a platform's API needs; a secret one is stored only encrypted and never read back.</summary>
public sealed record OnlinePlatformCredentialField(string Name, bool IsSecret, bool IsUrl = false);

/// <summary>The settings one online platform's API needs.</summary>
public sealed record OnlinePlatformCredentialDefinition(string Provider, IReadOnlyList<OnlinePlatformCredentialField> Fields)
{
    public OnlinePlatformCredentialField? Field(string name) =>
        Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// V12-OUI-003: the platforms whose API settings can be entered from the interface. Each setting answers to
/// the secret reference <c>{provider}-{field}</c>, the same name its environment variable has had
/// (<c>ALKAROS_SECRET_YEMEKSEPETI_CLIENT_SECRET</c> is <c>yemeksepeti</c> / <c>client-secret</c>), so a stored
/// value and an environment variable are two sources of the same setting.
/// <para>
/// Trendyol Go's fields follow its public integration document (EXT:TGO-MEAL-API: supplier id plus API key and
/// secret for basic authentication); like the adapter that will read them, they are an UNVERIFIED DRAFT until a
/// real seller account exists (V12-TGO-001). Migros Yemek has no public document, so it has no settings yet.
/// </para>
/// </summary>
public static class OnlinePlatformCredentialCatalog
{
    public const string Yemeksepeti = "yemeksepeti";
    public const string TrendyolGo = "trendyol-go";

    public static IReadOnlyList<OnlinePlatformCredentialDefinition> Platforms { get; } =
    [
        new(Yemeksepeti,
        [
            new("api-base-url", IsSecret: false, IsUrl: true),
            new("chain-id", IsSecret: false),
            new("vendor-id", IsSecret: false),
            new("client-id", IsSecret: false),
            new("client-secret", IsSecret: true),
            new("webhook-secret", IsSecret: true),
        ]),
        new(TrendyolGo,
        [
            new("api-base-url", IsSecret: false, IsUrl: true),
            new("supplier-id", IsSecret: false),
            new("api-key", IsSecret: true),
            new("api-secret", IsSecret: true),
        ]),
    ];

    public static OnlinePlatformCredentialDefinition? Find(string? provider) =>
        Platforms.FirstOrDefault(platform => string.Equals(platform.Provider, provider, StringComparison.Ordinal));

    /// <summary>The platform and field a secret reference name stands for, or null when it is not a platform setting.</summary>
    public static (string Provider, string Field)? FromSecretReference(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var platform in Platforms)
        {
            var prefix = platform.Provider + "-";
            if (name.StartsWith(prefix, StringComparison.Ordinal) && platform.Field(name[prefix.Length..]) is { } field)
                return (platform.Provider, field.Name);
        }

        return null;
    }
}
