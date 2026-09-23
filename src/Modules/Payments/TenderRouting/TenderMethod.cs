namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Canonical tender methods (PDF:II.5.3). `SplitPayment` is deliberately not
/// a member here — it is neither a Payment state nor a Payment method
/// (V0-DOM-001's own invariant, docs/domain/lifecycle-transition-contracts.md);
/// splitting is how a Bill's payable is divided across owners/segments
/// before any of these methods tenders against it.
/// </summary>
public enum TenderMethod
{
    Cash,
    BankCard,
    MealCard,

    /// <summary>
    /// EFT/Havale (bank transfer, V13-PAY-005, PO:2026-09-16) — not in the
    /// original PDF baseline, a direct Semih product decision. No
    /// provider/Open Banking integration exists; the cashier declares that
    /// the amount landed in the business's own account statement.
    /// </summary>
    Eft,

    /// <summary>
    /// Recognized by name in V1.3 but never routable here — every request
    /// for this method gets a typed "version not enabled" rejection
    /// (V13-PAY-002). A real handler is registered only once V14-ACC-008
    /// wires the V1.4 CustomerAccount composition extension.
    /// </summary>
    CustomerAccount,
}

/// <summary>
/// Parses a raw method name into <see cref="TenderMethod"/> without ever
/// throwing — an unknown method name (a typo, "SplitPayment", or any other
/// text outside the closed enum) is a routine rejection, not an exception.
/// </summary>
public static class TenderMethodCatalog
{
    public static bool TryParse(string? raw, out TenderMethod method)
    {
        // Enum.TryParse also accepts a bare numeric ordinal ("1"), and it
        // trims surrounding whitespace before parsing (" 1", "1 ", " 1 "),
        // any of which would silently accept a value no caller ever spelled
        // out — a tender method arrives as an exact canonical name, never a
        // magic number or a whitespace-padded variant of one. Requiring the
        // parsed value's own name to round-trip back to the raw input
        // exactly rejects every such variant without guessing at guard
        // conditions for each one.
        if (!string.IsNullOrEmpty(raw)
            && Enum.TryParse(raw, ignoreCase: false, out TenderMethod parsed)
            && Enum.IsDefined(parsed)
            && parsed.ToString() == raw)
        {
            method = parsed;
            return true;
        }

        method = default;
        return false;
    }
}
