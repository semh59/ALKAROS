namespace ALKAROS.Privacy.RetentionExecution;

/// <summary>
/// The one place that decides which records are due. Each query yields (subject_id, basis_at): the moment the class's
/// window starts. The anonymized marker is the one the V1 command writes, so records it already scrubbed are not due again.
/// </summary>
internal static class RetentionSelection
{
    public const string AnonymizedMarker = "[anonymized]";

    private const string StaffAccount =
        """
        SELECT u.user_id AS subject_id, u.updated_at AS basis_at
        FROM identity.users u
        WHERE u.active = false AND u.display_name <> @marker
        """;

    private const string OrderNotes =
        """
        SELECT o.order_id AS subject_id, o.created_at AS basis_at
        FROM orders.orders o
        WHERE o.status IN ('Completed', 'Cancelled', 'Rejected')
          AND ((o.notes IS NOT NULL AND o.notes <> @marker)
               OR EXISTS (SELECT 1 FROM orders.order_items i
                          WHERE i.order_id = o.order_id AND i.notes IS NOT NULL AND i.notes <> @marker))
        """;

    private const string ReservationReason =
        """
        SELECT r.table_reservation_id AS subject_id, r.reserved_at AS basis_at
        FROM table_mgmt.table_reservations r
        WHERE r.status IN ('Claimed', 'Cancelled', 'Expired') AND r.reason <> @marker
        """;

    // A customer with an open balance is never due; the window starts at the last ledger movement or invoice.
    private const string CustomerProfile =
        """
        SELECT p.customer_id AS subject_id,
               GREATEST(p.created_at,
                        COALESCE((SELECT max(t.occurred_at) FROM customer_account.account_transactions t WHERE t.customer_id = p.customer_id), p.created_at),
                        COALESCE((SELECT max(i.created_at) FROM invoicing.invoices i WHERE i.customer_id = p.customer_id), p.created_at)) AS basis_at
        FROM customer_data.profiles p
        WHERE NOT p.anonymized
          AND COALESCE((SELECT b.current_balance FROM customer_account.balances b WHERE b.customer_id = p.customer_id), 0) = 0
          AND NOT EXISTS (SELECT 1 FROM customer_data.anonymization_requests r WHERE r.customer_id = p.customer_id AND r.status = 'Pending')
        """;

    private const string Supplier =
        """
        SELECT s.supplier_id AS subject_id,
               GREATEST(s.created_at, COALESCE((SELECT max(o.updated_at) FROM purchasing.purchase_orders o WHERE o.supplier_id = s.supplier_id), s.created_at)) AS basis_at
        FROM purchasing.suppliers s
        WHERE s.name <> @marker
        """;

    public static string Query(RetentionClass dataClass) => dataClass switch
    {
        RetentionClass.StaffAccount => StaffAccount,
        RetentionClass.OrderNotes => OrderNotes,
        RetentionClass.ReservationReason => ReservationReason,
        RetentionClass.CustomerProfile => CustomerProfile,
        RetentionClass.Supplier => Supplier,
        _ => throw new ArgumentOutOfRangeException(nameof(dataClass), dataClass, "Unknown retention class."),
    };

    /// <summary>Wraps a class query with the exclusions every class shares: window elapsed, no hold, no work item yet.</summary>
    public static string Due(RetentionClass dataClass) =>
        $"""
        SELECT c.subject_id, c.basis_at
        FROM ({Query(dataClass)}) c
        WHERE c.basis_at < @cut
          AND NOT EXISTS (SELECT 1 FROM privacy.legal_holds h
                          WHERE h.data_class = @class AND h.subject_id = c.subject_id AND h.released_at IS NULL)
          AND NOT EXISTS (SELECT 1 FROM privacy.retention_work_items w
                          WHERE w.data_class = @class AND w.subject_id = c.subject_id)
          AND c.subject_id <> ALL(@extra_held)
        ORDER BY c.basis_at, c.subject_id;
        """;
}
