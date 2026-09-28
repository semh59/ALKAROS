namespace ALKAROS.CustomerData;

using ALKAROS.CustomerData.Profiles;
using ALKAROS.ModuleComposition;
using ALKAROS.Secrets;

/// <summary>
/// V14-CST-001: the customer PII boundary. A leaf bounded context (see
/// docs/architecture/module-dependency-rules.md's new row) - Customer
/// Account (V14-ACC) and Invoicing (V14-INV) are expected to reference a
/// customer's identity through this module later, not the other way
/// around, so this module itself declares no module dependency.
/// </summary>
public sealed class CustomerDataModule : IModule
{
    public string Id => "CustomerData";
    public string DisplayName => "Customer Data (PII Profile Boundary)";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        // V14-CST-001: name/phone/email/address persist as a single AES-256-GCM
        // envelope (customer_data.profiles, migration 159) - only ciphertext
        // ever reaches the row. ISecretProvider is registered here (not
        // shared) for the same reason PostgresQnbCredentialStore's own
        // module does: ISensitiveDataAccessPolicy has no shared DI
        // registration by design (a second/third module registering it
        // globally would silently collide with the others).
        context.RegisterTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        context.RegisterTransient<ICustomerProfileStore, PostgresCustomerProfileStore>();
    }
}
