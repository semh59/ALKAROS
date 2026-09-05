using System;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

/// <summary>
/// Implements the Receipt Variance Policy approved under V0-DOM-009 (PDF:III.15, CORR:C11).
/// Single binding policy:
/// 1. Short delivery (delivered < ordered): Auto-accept actual delivered, reason required, no approval needed.
/// 2. Within-tolerance over-receipt (excess <= 5% of ordered): Auto-accept full delivered, reason required, no approval needed.
/// 3. Above-tolerance over-receipt (excess > 5%):
///    - If approved by Manager: posts full delivered quantity, reason required.
///    - If unapproved: posts ordered quantity, rejects excess quantity into rejected lines, reason required.
/// 4. Every variance (delivered != ordered) requires a non-empty variance reason (Audit-first).
/// </summary>
public static class ReceiptVariancePolicy
{
    public const decimal TolerancePercent = 0.05m; // 5%

    public static VarianceEvaluationResult Evaluate(
        decimal orderedQuantity,
        decimal deliveredQuantity,
        bool isManagerApproved,
        string? varianceReason)
    {
        if (deliveredQuantity < 0)
        {
            throw new InvalidGoodsReceiptException("Delivered quantity cannot be negative.");
        }

        // Case A: Exact delivery
        if (deliveredQuantity == orderedQuantity)
        {
            return new VarianceEvaluationResult(
                AcceptedQuantity: deliveredQuantity,
                RejectedQuantity: 0m,
                VarianceQuantity: 0m,
                VarianceReason: varianceReason,
                RequiresManagerApproval: false,
                IsApproved: isManagerApproved);
        }

        // Variance exists (short delivery or over-delivery) -> Reason is mandatory per PDF:III.1.7 audit-first
        if (string.IsNullOrWhiteSpace(varianceReason))
        {
            throw new VarianceReasonRequiredException(
                $"A mandatory variance reason must be provided when delivered quantity ({deliveredQuantity}) does not match expected quantity ({orderedQuantity}).");
        }

        // Case B: Short delivery (delivered < ordered)
        if (deliveredQuantity < orderedQuantity)
        {
            var shortVariance = deliveredQuantity - orderedQuantity;
            return new VarianceEvaluationResult(
                AcceptedQuantity: deliveredQuantity,
                RejectedQuantity: 0m,
                VarianceQuantity: shortVariance,
                VarianceReason: varianceReason.Trim(),
                RequiresManagerApproval: false,
                IsApproved: isManagerApproved);
        }

        // Case C: Over-receipt (delivered > ordered)
        var excess = deliveredQuantity - orderedQuantity;
        var toleranceThreshold = orderedQuantity * TolerancePercent;

        // Subcase C.1: Within tolerance (excess <= 5%)
        if (excess <= toleranceThreshold)
        {
            return new VarianceEvaluationResult(
                AcceptedQuantity: deliveredQuantity,
                RejectedQuantity: 0m,
                VarianceQuantity: excess,
                VarianceReason: varianceReason.Trim(),
                RequiresManagerApproval: false,
                IsApproved: isManagerApproved);
        }

        // Subcase C.2: Above tolerance (excess > 5%)
        // If approved by Manager: post full delivered
        if (isManagerApproved)
        {
            return new VarianceEvaluationResult(
                AcceptedQuantity: deliveredQuantity,
                RejectedQuantity: 0m,
                VarianceQuantity: excess,
                VarianceReason: varianceReason.Trim(),
                RequiresManagerApproval: true,
                IsApproved: true);
        }

        // If unapproved: auto-cap to ordered, reject excess
        return new VarianceEvaluationResult(
            AcceptedQuantity: orderedQuantity,
            RejectedQuantity: excess,
            VarianceQuantity: excess,
            VarianceReason: varianceReason.Trim(),
            RequiresManagerApproval: true,
            IsApproved: false);
    }
}

public sealed record VarianceEvaluationResult(
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    decimal VarianceQuantity,
    string? VarianceReason,
    bool RequiresManagerApproval,
    bool IsApproved);
