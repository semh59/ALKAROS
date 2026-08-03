# Bill and Order Cardinality — decision pending

> **Task:** V0-DOM-002
> **Status:** Blocked
> **Source basis:** PDF:I.11-I.15, PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
> **Access date:** 2026-08-02
> **Approver:** None — decision is not approved

The PDF distinguishes Bill, Order and OrderItem but does not select the target
restaurant's split/merge cardinality. No implementation invariant is approved.

The decision record must select one internally consistent model, prove it with
one-to-many, many-to-one and split examples, identify rejected alternatives,
and name the accountable business approver. Until then no Bill-to-Order schema
or migration is authorized.
