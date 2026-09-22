namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// The nine data categories from V0-CMP-003's KVKK data inventory
/// (evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md). Every value
/// here must have an entry in <see cref="DisposalMatrix.Actions"/> —
/// enforced by <see cref="RetentionCoverageVerifier"/>.
/// </summary>
public enum DataCategory
{
    CustomerPii = 0,
    UserCredentials = 1,
    OrderNotes = 2,
    ProviderPayloads = 3,
    AuditLogs = 4,
    FiscalData = 5,
    InvoiceData = 6,
    SupplierData = 7,
    DeviceData = 8,
}
