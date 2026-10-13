using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Refactor step 2/7 (docs/engineering/garson-refactor-plan.md, 2026-09-12):
/// <see cref="GetMyShiftSummaryAsync"/> extracted out of the former
/// god-class <c>OrderManagementStore</c> — it was already a fully
/// self-contained read with no dependency on anything else in that file,
/// the lowest-risk second step once step 1 (<see cref="OrderDtoAssembler"/>)
/// proved the pattern.
/// </summary>
public sealed class ShiftSummaryStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ISettingsService _settings;

    public ShiftSummaryStore(NpgsqlDataSource dataSource, ISettingsService settings)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    // V1-WTR-021: garson-karsilastirma idea #9, a self-view-only shift
    // summary - three read-only numbers a waiter can check on their
    // own shift, session-scoped to the caller (never a manager view, never
    // another waiter's numbers). "Vardiya" = UTC calendar day, the same
    // reset boundary PersonalCompBudgetEscalationResolver already
    // established (Semih, 2026-09-11) rather than inventing a punch-clock
    // shift concept ALKAROS has nowhere else.
    public async Task<MyShiftSummaryV1> GetMyShiftSummaryAsync(
        Guid waiterUserId, CancellationToken cancellationToken = default)
    {
        // V1-SET-004: this deployment turned the self-view shift summary
        // off — refused outright rather than returning zeros, so a client
        // still showing the old menu item gets a clear "not offered here"
        // instead of a misleadingly empty summary.
        if (!await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.ShiftSummary, cancellationToken))
            throw new GarsonFeatureDisabledException(GarsonFeature.ShiftSummary);

        var startOfUtcDay = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

        decimal salesTotal;
        await using (var salesCommand = _dataSource.CreateCommand(
            """
            SELECT COALESCE(SUM(o.total), 0)
            FROM orders.orders o
            WHERE o.serving_user_id = @waiter_user_id
              AND o.status <> 'Cancelled'
              AND o.created_at >= @since
              AND NOT (
                    EXISTS (SELECT 1 FROM billing.bills rb
                            WHERE rb.order_id = o.order_id AND rb.status = 'Cancelled'
                              AND EXISTS (SELECT 1 FROM payments.payments rp
                                          WHERE rp.bill_id = rb.bill_id AND rp.status = 'Refunded'))
                    AND NOT EXISTS (SELECT 1 FROM billing.bills pb
                                    WHERE pb.order_id = o.order_id AND pb.status = 'Paid'));
            """))
        {
            salesCommand.Parameters.AddWithValue("waiter_user_id", waiterUserId);
            salesCommand.Parameters.AddWithValue("since", startOfUtcDay);
            salesTotal = (decimal)(await salesCommand.ExecuteScalarAsync(cancellationToken) ?? 0m);
        }

        decimal compUsed;
        await using (var compCommand = _dataSource.CreateCommand(
            """
            SELECT COALESCE(SUM(amount), 0)
            FROM identity.authorization_grants
            WHERE requester_user_id = @waiter_user_id
              AND permission_code = 'bills.comp'
              AND status = 'granted'
              AND resolved_at >= @since;
            """))
        {
            compCommand.Parameters.AddWithValue("waiter_user_id", waiterUserId);
            compCommand.Parameters.AddWithValue("since", startOfUtcDay);
            compUsed = (decimal)(await compCommand.ExecuteScalarAsync(cancellationToken) ?? 0m);
        }

        // V1-WTR-021 + V1-WTR-020 (Semih, 2026-09-11): equal pool - every
        // voluntary tip recorded today, across every bill, split evenly
        // across every distinct waiter who served at least one order today.
        // "Served an order today" (orders.orders.serving_user_id), not a
        // role check, since that is the same population the sales-total
        // query above already uses as "worked this shift".
        decimal tipPoolTotal;
        long waiterCount;
        await using (var tipCommand = _dataSource.CreateCommand(
            """
            SELECT
                COALESCE((SELECT SUM(a.amount) FROM billing.bill_adjustments a
                          JOIN billing.bills tb ON tb.bill_id = a.bill_id
                          WHERE a.adjustment_type = 'Tip' AND a.created_at >= @since
                            AND tb.status <> 'Cancelled'), 0),
                (SELECT COUNT(DISTINCT o.serving_user_id) FROM orders.orders o
                 WHERE o.serving_user_id IS NOT NULL AND o.status <> 'Cancelled' AND o.created_at >= @since
                   AND NOT (
                    EXISTS (SELECT 1 FROM billing.bills rb
                            WHERE rb.order_id = o.order_id AND rb.status = 'Cancelled'
                              AND EXISTS (SELECT 1 FROM payments.payments rp
                                          WHERE rp.bill_id = rb.bill_id AND rp.status = 'Refunded'))
                    AND NOT EXISTS (SELECT 1 FROM billing.bills pb
                                    WHERE pb.order_id = o.order_id AND pb.status = 'Paid')));
            """))
        {
            tipCommand.Parameters.AddWithValue("since", startOfUtcDay);
            await using var reader = await tipCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            tipPoolTotal = reader.GetDecimal(0);
            waiterCount = reader.GetInt64(1);
        }

        var tipShare = waiterCount > 0 ? Math.Round(tipPoolTotal / waiterCount, 2) : 0m;

        return new MyShiftSummaryV1(salesTotal, compUsed, tipPoolTotal, waiterCount, tipShare);
    }
}
