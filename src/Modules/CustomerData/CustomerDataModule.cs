namespace ALKAROS.CustomerData;

using ALKAROS.Audit.EventStore;
using ALKAROS.CustomerData.AnonymizationState;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.ModuleComposition;
using ALKAROS.Secrets;

/// <summary>
/// V14-CST-001/V14-CST-002: the customer PII boundary and its anonymization
/// request state machine. A near-leaf bounded context (see
/// docs/architecture/module-dependency-rules.md's row 30) - Customer
/// Account (V14-ACC) and Invoicing (V14-INV) are expected to reference a
/// customer's identity through this module later, not the other way
/// around. The only real dependency is Audit, for
/// CustomerAnonymizationService's own audit trail (V14-CST-002).
/// </summary>
public sealed class CustomerDataModule : IModule
{
    public string Id => "CustomerData";
    public string DisplayName => "Customer Data (PII Profile Boundary)";
    public IReadOnlyCollection<string> DependsOn => ["Audit"];

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

        // V1-RMD-435: IAnonymizationRetentionGuard is registered by the module
        // that owns the financial records it checks (CustomerAccounts.BillCharges),
        // not here - see the interface's own doc comment.
        context.RegisterTransient<ICustomerAnonymizationRequestStore, PostgresCustomerAnonymizationRequestStore>();
        context.RegisterTransient<CustomerAnonymizationService, CustomerAnonymizationService>();
    }
}
