namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// The one SQL definition of "this bill belongs to this order". A bill opened from one order carries its id in
/// <c>order_id</c>; a bill merged from several orders carries none, so its orders are the ones its items come from.
/// Both are matched here so no consumer sees a merged bill as an orphan.
/// </summary>
public static class BillOrderLink
{
    /// <param name="bill">Bill row reference: an alias, or the unaliased table name inside an UPDATE.</param>
    /// <param name="order">Order id expression: a column such as <c>o.order_id</c> or a parameter such as <c>@order_id</c>.</param>
    public static string Matches(string bill, string order) =>
        $"""
        ({bill}.order_id = {order} OR EXISTS (
            SELECT 1 FROM billing.bill_items bli
            JOIN orders.order_items oli ON oli.order_item_id = bli.order_item_id
            WHERE bli.bill_id = {bill}.bill_id AND oli.order_id = {order}))
        """;

    /// <summary>The ids of every order the bill is made of, in id order (the order callers lock and cancel them); for @bill_id.</summary>
    public const string OrderIdsOfBill =
        """
        SELECT order_id FROM billing.bills WHERE bill_id = @bill_id AND order_id IS NOT NULL
        UNION
        SELECT oli.order_id FROM billing.bill_items bli
        JOIN orders.order_items oli ON oli.order_item_id = bli.order_item_id
        WHERE bli.bill_id = @bill_id
        ORDER BY 1;
        """;
}
