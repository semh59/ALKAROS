namespace ALKAROS.Payments.Token.Draft;

/// <summary>
/// V13-GOV-006 DRAFT. Mirrors the SHAPE of the real
/// `ALKAROS.Payments.TenderRouting.ITenderHandler`/`TenderHandlerResult`
/// (see `src/Modules/Payments/TenderRouting/`) so that adopting this into
/// `V13-HUG-001`'s real Owned surface later is a rename, not a redesign —
/// but it does NOT reference the real assembly (this project is
/// deliberately standalone, not wired into ALKAROS.slnx; see README.md).
///
/// Scope matches `V13-HUG-001`'s own In/Out of scope exactly: request
/// mapping + Approved/Declined normalization only. Timeout/unknown recovery
/// is `V13-HUG-002`'s job, refund/cancel is `V13-HUG-003`'s, and committing
/// the result to a real Payment/allocation is `V13-PAY-004`'s — this class
/// does none of those.
/// </summary>
public sealed class TokenTenderHandler
{
    private readonly TokenBasketClient _client;
    private readonly string _terminalId;

    public TokenTenderHandler(TokenBasketClient client, string terminalId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);
        _terminalId = terminalId;
    }

    /// <param name="paymentId">ALKAROS's own Payment id — used only to derive
    /// the basket's `checkNumber` for cross-referencing in provider evidence;
    /// never sent as-is (Token's `checkNumber` is a max-10-digit int per the
    /// public docs, a raw GUID does not fit).</param>
    /// <param name="amountKurus">Tender amount in kuruş (Token's own examples
    /// use integer minor-unit amounts throughout, e.g. `15000` = 150,00 TL —
    /// this project never assumed a decimal amount format).</param>
    public async Task<TokenTenderOutcome> HandleAsync(
        Guid paymentId,
        long amountKurus,
        CancellationToken cancellationToken = default)
    {
        if (amountKurus <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountKurus), "Tender amount must be greater than zero.");

        var basketId = Guid.NewGuid();
        var checkNumber = unchecked((int)(paymentId.GetHashCode() & 0x7FFFFFFF)) % 1_000_000_000;

        var basketRequest = new TokenAddInstantBasketRequest(
            BasketId: basketId,
            CheckNumber: checkNumber,
            Items: [new TokenBasketItem("ALKAROS Payment", amountKurus, SectionNo: 1, TaxPercent: 0, Quantity: 1000)],
            PaymentItems: [new TokenPaymentRoutingItem(amountKurus, TokenPaymentRoutingItem.PaymentTypeCreditCard, OperatorId: 0)]);

        await _client.AddInstantBasketAsync(_terminalId, basketRequest, cancellationToken);

        var sale = await _client.PollUntilSettledAsync(_terminalId, basketId, cancellationToken: cancellationToken);

        if (sale is null)
            return new TokenTenderOutcome.RequiresReconciliation(
                $"Token basket {basketId} zaman aşımına uğradı, sonuç sorgulanamadı.");

        var sanitizedEvidence = new TokenProviderEvidence(
            BasketId: basketId,
            ReceiptNo: sale.ReceiptNo,
            SaleStatus: sale.Status);

        return sale.Status switch
        {
            TokenSaleResult.SaleStatusSuccessful => new TokenTenderOutcome.Approved(amountKurus, sanitizedEvidence),
            TokenSaleResult.SaleStatusFailed => new TokenTenderOutcome.Declined(
                sale.Message ?? "Token terminali ödemeyi reddetti.", sanitizedEvidence),

            // status 99 (Fiş iptal/void) mid-flight is outside this task's
            // scope (V13-HUG-003 owns cancel/refund) — surfaced as
            // "requires reconciliation" rather than silently mapped to
            // Approved or Declined, matching CORR:C29 (never guess).
            _ => new TokenTenderOutcome.RequiresReconciliation(
                $"Token basket {basketId} beklenmeyen sale.status={sale.Status} döndürdü."),
        };
    }
}

/// <summary>
/// Mirrors `ALKAROS.Payments.TenderRouting.TenderHandlerResult`'s 3-case
/// shape (Approved/Declined/RequiresReconciliation) exactly, standalone.
/// </summary>
public abstract record TokenTenderOutcome
{
    public sealed record Approved(long ApprovedAmountKurus, TokenProviderEvidence Evidence) : TokenTenderOutcome;

    public sealed record Declined(string Reason, TokenProviderEvidence Evidence) : TokenTenderOutcome;

    public sealed record RequiresReconciliation(string Reason) : TokenTenderOutcome;
}

/// <summary>
/// "Arındırılmış provider evidence" (V13-HUG-001's own In scope wording) —
/// deliberately carries NO card PAN/track data/cardholder name (Token's own
/// `sale.paymentItems` example never included any either, but this type
/// exists so a future real implementation cannot accidentally widen it to
/// carry raw card data without a visible type change here).
/// </summary>
public sealed record TokenProviderEvidence(Guid BasketId, int? ReceiptNo, int SaleStatus);
