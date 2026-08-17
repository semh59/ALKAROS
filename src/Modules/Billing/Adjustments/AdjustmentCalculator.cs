using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// Domain calculator for bill adjustments (discounts, fees, tips).
/// Computes modified totals and verifies mathematical consistency and non-negativity.
/// </summary>
public static class AdjustmentCalculator
{
    /// <summary>
    /// Computes the adjusted bill summary accounting for all applied adjustments.
    /// </summary>
    public static AdjustedBillSummary Calculate(
        Bill bill,
        IReadOnlyList<BillAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(adjustments);

        var discountGross = 0m;
        var discountTax = 0m;
        var feeGross = 0m;
        var feeTax = 0m;
        var tipGross = 0m;

        foreach (var adj in adjustments)
        {
            if (adj.BillId != bill.Id)
            {
                throw new InvalidOperationException(
                    $"Adjustment {adj.Id} belongs to Bill {adj.BillId}, not Bill {bill.Id}.");
            }

            if (adj.IsDeduction)
            {
                discountGross += adj.GrossAmount;
                discountTax += adj.TaxAmount;
            }
            else if (adj.AdjustmentType == AdjustmentType.Tip)
            {
                tipGross += adj.GrossAmount;
            }
            else
            {
                feeGross += adj.GrossAmount;
                feeTax += adj.TaxAmount;
            }
        }

        discountGross = BillMath.RoundCurrency(discountGross);
        discountTax = BillMath.RoundCurrency(discountTax);
        feeGross = BillMath.RoundCurrency(feeGross);
        feeTax = BillMath.RoundCurrency(feeTax);
        tipGross = BillMath.RoundCurrency(tipGross);

        var totalBasePayable = bill.PayableAmount;
        if (discountGross > (totalBasePayable + feeGross))
        {
            throw new InvalidOperationException(
                $"Total discount ({discountGross}) cannot exceed bill base payable plus fees ({totalBasePayable + feeGross}).");
        }

        var adjustedDiscountTotal = BillMath.RoundCurrency(bill.DiscountTotal + discountGross);
        var adjustedTaxTotal = BillMath.RoundCurrency(Math.Max(0m, bill.TaxTotal - discountTax + feeTax));
        var adjustedPayableAmount = BillMath.RoundCurrency(totalBasePayable - discountGross + feeGross + tipGross);

        return new AdjustedBillSummary(
            BillId: bill.Id,
            OriginalSubtotal: bill.Subtotal,
            OriginalPayableAmount: bill.PayableAmount,
            TotalDiscounts: discountGross,
            TotalFees: feeGross,
            TotalTips: tipGross,
            AdjustedDiscountTotal: adjustedDiscountTotal,
            AdjustedTaxTotal: adjustedTaxTotal,
            AdjustedPayableAmount: adjustedPayableAmount);
    }
}

/// <summary>
/// Computed summary of a Bill after applying adjustments.
/// </summary>
public sealed record AdjustedBillSummary(
    Guid BillId,
    decimal OriginalSubtotal,
    decimal OriginalPayableAmount,
    decimal TotalDiscounts,
    decimal TotalFees,
    decimal TotalTips,
    decimal AdjustedDiscountTotal,
    decimal AdjustedTaxTotal,
    decimal AdjustedPayableAmount);
