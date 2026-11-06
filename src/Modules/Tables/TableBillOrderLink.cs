namespace ALKAROS.Tables.TableRelease;

/// <summary>
/// "This bill belongs to this order" for the table queries: a bill opened from one order carries it in <c>order_id</c>, a bill
/// merged from several orders carries none, so its orders are the ones its items come from. The Tables module may not
/// reference Billing, so the definition of <c>BillOrderLink.Matches</c> is repeated here.
/// </summary>
internal static class TableBillOrderLink
{
    public static string Matches(string bill, string order) =>
        $"""
        ({bill}.order_id = {order} OR EXISTS (
            SELECT 1 FROM billing.bill_items bli
            JOIN orders.order_items oli ON oli.order_item_id = bli.order_item_id
            WHERE bli.bill_id = {bill}.bill_id AND oli.order_id = {order}))
        """;
}
