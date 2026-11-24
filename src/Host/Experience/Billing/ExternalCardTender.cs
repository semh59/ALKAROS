using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.ManualResolution;
using ALKAROS.Payments.PaymentAggregate;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>A card payment taken on a bank POS that is not connected to the system; the slip number is the proof.</summary>
public sealed record ExternalCardTenderRequestV1(decimal Amount, string SlipNumber, string IdempotencyKey);

/// <summary>A request the external card tender refused; <see cref="Code"/> and <see cref="UserMessage"/> are what the screen shows.</summary>
public sealed class ExternalCardTenderException(string code, string userMessage, int status) : Exception(code)
{
    public string Code { get; } = code;

    public string UserMessage { get; } = userMessage;

    public int Status { get; } = status;
}

/// <summary>
/// Records a card payment that went through a bank POS the system is not connected to. Like the transfer tender it
/// creates an approved payment and its allocation, but it also keeps the slip number (a bounded code, never a card
/// number) in <c>payments.external_card_receipts</c>, which is what makes the payment a card payment in every report.
/// It never touches a cash session or the drawer. Payment, allocation, receipt and audit event commit together under
/// the shared per-bill settlement lock.
/// </summary>
public sealed class ExternalCardTenderStore(
    IBillRepository bills,
    IBillAdjustmentRepository adjustments,
    IPaymentRepository payments,
    IPaymentAllocationRepository allocations,
    IAuditEventStore audit,
    NpgsqlDataSource dataSource)
{
    public const int MinSlipLength = 4;
    public const int MaxSlipLength = 32;
    public const int MaxKeyLength = 128;
    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public async Task<decimal> RecordAsync(
        Guid billId, Guid terminalId, Guid userId, ExternalCardTenderRequestV1 request, CancellationToken cancellationToken)
    {
        var amount = request.Amount;
        if (amount <= 0 || amount > MaxAmount || decimal.Round(amount, 2) != amount)
            throw new ExternalCardTenderException(
                "VALIDATION_FAILED", "Tutar sıfırdan büyük olmalı ve en çok iki ondalık içermeli.", StatusCodes.Status400BadRequest);
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
            throw new ExternalCardTenderException(
                "IDEMPOTENCY_KEY_INVALID", "İşlem anahtarı boş olamaz ve 128 karakteri aşamaz.", StatusCodes.Status400BadRequest);
        var slip = NormalizeSlip(request.SlipNumber);

        var bill = await bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new ExternalCardTenderException("BILL_NOT_FOUND", "Hesap bulunamadı.", StatusCodes.Status404NotFound);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // The bill lock first, then the key lock: the order every tender handler uses, so this serializes against
        // a cash, card, transfer or account tender on the same bill.
        await LockAsync(connection, transaction, $"bill-settlement:{billId:N}", cancellationToken);
        await LockAsync(connection, transaction, $"external-card-tender:{key}", cancellationToken);

        var earlier = await allocations.GetByIdempotencyKeyAsync(key, connection, transaction, cancellationToken);
        if (earlier is not null)
        {
            var recordedSlip = await ExternalCardReceipts.FindSlipAsync(connection, transaction, earlier.PaymentId, cancellationToken);
            if (recordedSlip != slip || earlier.BillId != billId || earlier.Amount != amount)
                throw new ExternalCardTenderException(
                    "TENDER_IDEMPOTENCY_KEY_REUSED", "İşlem kimliği başka bir ödeme için zaten kullanılmış.", StatusCodes.Status409Conflict);
            await transaction.CommitAsync(cancellationToken);
            return earlier.Amount;
        }

        if (await ExternalCardReceipts.SlipExistsOnBillAsync(connection, transaction, billId, slip, cancellationToken))
            throw new ExternalCardTenderException(
                "EXTERNAL_CARD_SLIP_REUSED",
                "Bu fiş numarası bu hesapta zaten kayıtlı. Aynı fişi iki kez girmeyin.", StatusCodes.Status409Conflict);

        // A card attempt left unresolved may really have charged the customer; adding a payment on top could charge twice.
        var existingPayments = await payments.GetByBillIdAsync(billId, cancellationToken);
        if (existingPayments.Any(p => p.Status is PaymentStatus.Pending or PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired))
            throw new ExternalCardTenderException(
                "TENDER_UNSETTLED_PAYMENT_EXISTS",
                "Bu hesapta çözülmemiş bir ödeme var; yeni bir tahsilat eklemeden önce mutabakat tamamlanmalı.",
                StatusCodes.Status409Conflict);

        var alreadyAllocated = (await allocations.GetByBillIdAsync(billId, cancellationToken)).Sum(a => a.Amount);
        var adjusted = AdjustmentCalculator.Calculate(bill, await adjustments.GetByBillIdAsync(billId, cancellationToken)).AdjustedPayableAmount;
        if (amount > adjusted - alreadyAllocated)
            throw new OverAllocationException(billId, amount, adjusted - alreadyAllocated);

        var payment = new Payment(Guid.NewGuid(), billId, amount)
            .Tender(amount, changedBy: userId)
            .Approve(amount, changedBy: userId);
        await payments.AddAsync(payment, connection, transaction, cancellationToken);
        await allocations.AllocateAsync(payment, bill, amount, key, connection, transaction, cancellationToken);

        await ExternalCardReceipts.AddAsync(connection, transaction, payment.Id, billId, terminalId, slip, amount, userId, cancellationToken);

        await audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "payment.external-card-recorded", "Payment", payment.Id, "User", billId.ToString("N"), userId,
                afterStateJson: JsonSerializer.Serialize(new { method = "ExternalCard", billId, terminalId, slipNumber = slip, amount })),
            connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return amount;
    }

    internal static string NormalizeSlip(string? slipNumber)
    {
        string slip;
        try
        {
            slip = ManualCardConfirmationService.NormalizeSlip(slipNumber);
        }
        catch (ManualResolutionSlipInvalidException)
        {
            throw new ExternalCardTenderException(
                "EXTERNAL_CARD_SLIP_INVALID",
                "Fiş numarası 4-32 karakter olmalı (harf, rakam, - ve /). Kart numarasını yazmayın.",
                StatusCodes.Status400BadRequest);
        }

        if (LooksLikeCardNumber(slip))
            throw new ExternalCardTenderException(
                "EXTERNAL_CARD_SLIP_IS_CARD_NUMBER",
                "Bu bir kart numarasına benziyor. Kart numarasını yazmayın; yalnız POS fişindeki fiş numarasını yazın.",
                StatusCodes.Status400BadRequest);
        return slip;
    }

    /// <summary>13 to 19 digits that pass the Luhn check: what a card number looks like, with or without separators.</summary>
    private static bool LooksLikeCardNumber(string slip)
    {
        var digits = slip.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 19 || digits.Length != slip.Count(c => c is not ('-' or '/')))
            return false;
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i];
            if (i % 2 == 1 && (digit *= 2) > 9)
                digit -= 9;
            sum += digit;
        }

        return sum % 10 == 0;
    }

    private static async Task LockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string lockKey, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue(lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
