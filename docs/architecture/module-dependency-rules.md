# Module Dependency Rules — decision pending

> **Task:** V0-ARC-001
> **Status:** Blocked
> **Source basis:** PDF:I.0-I.5, PDF:II.0-II.1, PDF:III.0-III.2
> **Access date:** 2026-08-02
> **Approver:** None — decision is not approved

The PDF identifies domain boundaries but does not select the repository's
compile-time dependency graph. The former record is withdrawn because it
allowed direct module references while also requiring all cross-module
communication to use integration events.

The replacement record must choose, per interaction, either an approved
reference contract or an integration event; identify the sole runtime owner;
and prove the graph acyclic. No generic `All` dependency or direct cross-module
call is authorized until then.
