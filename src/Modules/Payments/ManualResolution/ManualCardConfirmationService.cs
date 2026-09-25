using System.Text.Json;
using System.Text.RegularExpressions;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Messaging;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Payments.ManualResolution;

public interface IManualCardConfirmationService
{
    Task<ManualCardConfirmation> RequestAsync(
        Guid paymentId, Guid requesterUserId, string slipNumber, string? note, CancellationToken cancellationToken = default);

    /// <summary><paramref name="billId"/> must be the confirmation's own bill; anything else is "not found" and changes nothing.</summary>
    Task<ManualCardConfirmation> ApproveAsync(
        Guid confirmationId, Guid billId, Guid approverUserId, string? note, CancellationToken cancellationToken = default);

    Task<ManualCardConfirmation> RejectAsync(
        Guid confirmationId, Guid billId, Guid userId, string? note, CancellationToken cancellationToken = default);
}

/// <summary>
/// V1-RMD-283: "the card WAS charged" resolution for a card payment the system could not confirm (no real
/// terminal exists). It creates a money record, so it is two-person: one authorized person claims the charge
/// with the receipt (slip) number, a DIFFERENT authorized person approves. Only the approval turns the payment
/// Approved and allocates it to the bill, in one transaction under the shared per-bill settlement lock, and it
/// queues the same fiscal handoff event a real approval would. The slip number is unique across all live claims.
/// Authorization is the caller's job; this service enforces state, slip, four-eyes and concurrency.
/// </summary>
public sealed partial class ManualCardConfirmationService : IManualCardConfirmationService
{
    public const int MaxNoteLength = 500;

    private static readonly Regex SlipPattern = SlipRegex();

    private readonly IPaymentRepository _payments;
    private readonly IManualCardConfirmationRepository _confirmations;
    private readonly IPaymentAllocationRepository _allocations;
    private readonly IBillRepository _bills;
    private readonly NpgsqlDataSource _dataSource;

    public ManualCardConfirmationService(
        IPaymentRepository payments,
        IManualCardConfirmationRepository confirmations,
        IPaymentAllocationRepository allocations,
        IBillRepository bills,
        NpgsqlDataSource dataSource)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
        _allocations = allocations ?? throw new ArgumentNullException(nameof(allocations));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ManualCardConfirmation> RequestAsync(
        Guid paymentId, Guid requesterUserId, string slipNumber, string? note, CancellationToken cancellationToken = default)
    {
        var slip = NormalizeSlip(slipNumber);
        var trimmedNote = NormalizeNote(note);

        var initial = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(paymentId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockBillAsync(connection, transaction, initial.BillId, cancellationToken);

        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(paymentId);
        if (payment.Status is not (PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired))
            throw new ManualResolutionNotResolvableException(paymentId, payment.Status.ToString());

        var confirmation = new ManualCardConfirmation(
            Guid.NewGuid(), payment.Id, payment.BillId, slip, payment.TenderedAmount ?? payment.RequestedAmount,
            ManualCardConfirmationStatus.Pending, requesterUserId, DateTimeOffset.UtcNow, trimmedNote,
            null, null, null, 1);
        try
        {
            await _confirmations.InsertAsync(confirmation, connection, transaction, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw exception.ConstraintName == "ux_manual_card_slip_number"
                ? new ManualResolutionSlipReusedException(slip)
                : new ManualResolutionAlreadyPendingException(paymentId);
        }

        await transaction.CommitAsync(cancellationToken);
        return confirmation;
    }

    public async Task<ManualCardConfirmation> ApproveAsync(
        Guid confirmationId, Guid billId, Guid approverUserId, string? note, CancellationToken cancellationToken = default)
    {
        var trimmedNote = NormalizeNote(note);
        var actualBillId = await BillOfAsync(confirmationId, cancellationToken);
        if (actualBillId != billId)
            throw new ManualResolutionConfirmationNotFoundException(confirmationId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockBillAsync(connection, transaction, billId, cancellationToken);

        var confirmation = await _confirmations.GetByIdAsync(confirmationId, connection, transaction, cancellationToken)
            ?? throw new ManualResolutionConfirmationNotFoundException(confirmationId);
        if (confirmation.Status != ManualCardConfirmationStatus.Pending)
            throw new ManualResolutionConfirmationDecidedException(confirmationId, confirmation.Status.ToString());
        if (confirmation.RequestedBy == approverUserId)
            throw new ManualResolutionSameActorException();

        var payment = await _payments.GetByIdAsync(confirmation.PaymentId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(confirmation.PaymentId);
        if (payment.Status is not (PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired))
            throw new ManualResolutionNotResolvableException(payment.Id, payment.Status.ToString());
        var bill = await _bills.GetByIdAsync(payment.BillId, cancellationToken)
            ?? throw new ManualResolutionPaymentNotFoundException(payment.Id);

        var reason = $"Manuel onay: fiş {confirmation.SlipNumber}." + (trimmedNote is null ? string.Empty : $" {trimmedNote}");
        var expectedRowVersion = payment.RowVersion;
        var approved = payment;
        if (approved.Status == PaymentStatus.Unknown)
            approved = approved.RequestReconciliation(reason, approverUserId);
        approved = approved.Approve(confirmation.Amount, reason, approverUserId);

        await _payments.SaveAsync(approved, expectedRowVersion, connection, transaction, cancellationToken);
        await _allocations.AllocateAsync(
            approved, bill, confirmation.Amount, $"manual-card:{confirmation.Id:N}", connection, transaction, cancellationToken);
        await OutboxStore.EnqueueAsync(
            new OutboxEnvelope(
                "card-settlement.approved", "ManualCardConfirmation", confirmation.Id,
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    paymentId = approved.Id,
                    billId = approved.BillId,
                    approvedAmount = confirmation.Amount,
                    currencyCode = approved.CurrencyCode,
                    approvedAt = approved.ApprovedAt,
                    manual = true,
                    slipNumber = confirmation.SlipNumber,
                })),
            connection, transaction, cancellationToken);

        var decidedAt = DateTimeOffset.UtcNow;
        await _confirmations.DecideAsync(
            confirmation.Id, ManualCardConfirmationStatus.Approved, approverUserId, decidedAt, trimmedNote,
            confirmation.RowVersion, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return confirmation with
        {
            Status = ManualCardConfirmationStatus.Approved, DecidedBy = approverUserId, DecidedAt = decidedAt,
            DecisionNote = trimmedNote, RowVersion = confirmation.RowVersion + 1,
        };
    }

    public async Task<ManualCardConfirmation> RejectAsync(
        Guid confirmationId, Guid billId, Guid userId, string? note, CancellationToken cancellationToken = default)
    {
        var trimmedNote = NormalizeNote(note);
        var actualBillId = await BillOfAsync(confirmationId, cancellationToken);
        if (actualBillId != billId)
            throw new ManualResolutionConfirmationNotFoundException(confirmationId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockBillAsync(connection, transaction, billId, cancellationToken);

        var confirmation = await _confirmations.GetByIdAsync(confirmationId, connection, transaction, cancellationToken)
            ?? throw new ManualResolutionConfirmationNotFoundException(confirmationId);
        if (confirmation.Status != ManualCardConfirmationStatus.Pending)
            throw new ManualResolutionConfirmationDecidedException(confirmationId, confirmation.Status.ToString());

        var decidedAt = DateTimeOffset.UtcNow;
        await _confirmations.DecideAsync(
            confirmation.Id, ManualCardConfirmationStatus.Rejected, userId, decidedAt, trimmedNote,
            confirmation.RowVersion, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return confirmation with
        {
            Status = ManualCardConfirmationStatus.Rejected, DecidedBy = userId, DecidedAt = decidedAt,
            DecisionNote = trimmedNote, RowVersion = confirmation.RowVersion + 1,
        };
    }

    private async Task<Guid> BillOfAsync(Guid confirmationId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT bill_id FROM payments.manual_card_confirmations WHERE confirmation_id = @id;");
        command.Parameters.AddWithValue("id", confirmationId);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid billId
            ? billId
            : throw new ManualResolutionConfirmationNotFoundException(confirmationId);
    }

    private static async Task LockBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        // The shared per-bill string convention (CardSettlementOrchestrator, EftTenderHandler, topology policy).
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeSlip(string? slipNumber)
    {
        var slip = slipNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(slip) || !SlipPattern.IsMatch(slip))
            throw new ManualResolutionSlipInvalidException();
        return slip;
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > MaxNoteLength)
            throw new ManualResolutionReasonInvalidException($"The note cannot exceed {MaxNoteLength} characters.");
        return trimmed;
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9\\-/]{3,31}$")]
    private static partial Regex SlipRegex();
}
