# V1-RMD-951 Result

Status: Done.

- Pending approval cards now name what the request is about: "Hesap H-1042 · Masa 5" for a bill, "<product> · Masa N" for an order item; no line when the subject is gone or of an unknown kind.
- Server: `PendingGrantV1.SubjectLabel` filled by two lookups (bills, order items) with a left join to tables. Client: the card shows it.
- Tests: Authorization server project 21/21, PosTerminal authorization-decisions 11/11. Red/green logs and 4 killed mutants in this folder.
