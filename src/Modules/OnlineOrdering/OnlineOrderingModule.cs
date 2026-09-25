using ALKAROS.ModuleComposition;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering;

/// <summary>
/// Online ordering channels (module-dependency-rules.md row 20). Today the only channel is
/// Yemeksepeti, built against the public Partner API v2.0.2 document without sandbox
/// access (V0-YSP-001 is Blocked; V12-GOV-004 waiver).
/// </summary>
public sealed class OnlineOrderingModule : IModule
{
    public string Id => "OnlineOrdering";

    public string DisplayName => "Online Ordering";

    public IReadOnlyCollection<string> DependsOn => ["Catalog"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IYemeksepetiProductMappingService, PostgresYemeksepetiProductMappingService>();
        // V12-ONL-001: the inbox builds its own resolver/cipher/protector chain around its
        // single-purpose access policy; only the secret source itself comes from DI.
        context.RegisterTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        context.RegisterTransient<YemeksepetiWebhookInbox, YemeksepetiWebhookInbox>();
    }
}
