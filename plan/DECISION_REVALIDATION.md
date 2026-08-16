# Decision Evidence Revalidation Register

2026-08-02 bağımsız kaynak denetimi, aşağıdaki V0 decision/validation
çıktılarında zorunlu erişim tarihi, named approver veya reddedilen alternatif
kanıtının eksik olduğunu doğruladı. Bu kayıtlar, `V0-REV-001` .. `V0-REV-030`
supplement görevleriyle gerçek kaynak kanıtı, named approver ve reddedilen
alternatiflerle tek tek doğrulanmış ve kapatılmıştır.

## Revalidation & Gate Uzlaştırma Tablosu

| Task ID | Revalidation Task | Artifact | Eksik Kanıt | Revalidation Durumu | Verdict | Named Approver |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `V0-DAT-001` | `V0-REV-001` | `docs/data/migration-dependency-graph.md` | Named approver ve decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DAT-002` | `V0-REV-002` | `docs/data/canonical-value-catalog.md` | PDF state uyumu ve named approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DAT-003` | `V0-REV-003` | `docs/data/nullable-unique-policy.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DAT-004` | `V0-REV-004` | `docs/data/projection-ownership.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DAT-005` | `V0-REV-005` | `docs/data/single-branch-key-strategy.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DAT-006` | `V0-REV-006` | `docs/data/migration-rehearsal-profile.md` | Named approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOC-001` | `V0-REV-007` | `docs/specification/restaurant-pos-master.md` | Dependency closure ve source-bound baseline | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-001` | `V0-REV-008` | `docs/domain/lifecycle-transition-contracts.md` | PDF state uyumu ve provider boundary | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-002` | `V0-REV-009` | `docs/domain/bill-order-cardinality.md` | Tutarlı cardinality seçimi | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-003` | `V0-REV-010` | `docs/domain/refund-ledger.md` | Partial refund model kanıtı | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-004` | `V0-REV-011` | `docs/domain/payment-allocation-integrity.md` | Reversal representation seçimi | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-005` | `V0-REV-012` | `docs/domain/table-reservation-policy.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-006` | `V0-REV-013` | `docs/domain/void-complimentary-discount-policy.md` | Business approval ve threshold kaynağı | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-007` | `V0-REV-014` | `docs/domain/customer-credit-invoice-semantics.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-008` | `V0-REV-015` | `docs/domain/reporting-metrics.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-009` | `V0-REV-016` | `docs/domain/receipt-variance-policy.md` | Business approval | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-010` | `V0-REV-017` | `docs/domain/inventory-cost-basis.md` | Business approval | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-DOM-011` | `V0-REV-018` | `docs/domain/printer-routing-precedence.md` | Named approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-LIC-001` | `V0-REV-019` | `docs/licensing/licensing-contract.md` | Product/legal decision evidence | Blocked (Deferred) | Superseded (V20) | User approval (C40) |
| `V0-ARC-001` | `V0-REV-020` | `docs/architecture/module-dependency-rules.md` | Tek iletişim modeli ve approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-002` | `V0-REV-021` | `docs/architecture/local-first-sync-contract.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-003` | `V0-REV-022` | `docs/architecture/idempotency-inbox-outbox.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-004` | `V0-REV-023` | `docs/architecture/api-contract-standard.md` | Technology/policy source ve approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-005` | `V0-REV-024` | `docs/architecture/settings-ownership.md` | Decision provenance | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-006` | `V0-REV-025` | `docs/architecture/notification-delivery-matrix.md` | Transport/threshold business decision | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-007` | `V0-REV-026` | `docs/architecture/deployment-compatibility-matrix.md` | Named approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-008` | `V0-REV-027` | `docs/architecture/release-evidence-contract.md` | Named approver | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-ARC-009` | `V0-REV-028` | `docs/architecture/qr-relay-topology.md` | Topology decision evidence | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-CMP-002` | `V0-REV-029` | `docs/compliance/money-tax-business-date.md` | Tax/business decision evidence | Done | Confirms | Semih (product owner) — 2026-08-03 |
| `V0-CMP-005` | `V0-REV-030` | `docs/compliance/accessibility-target.md` | Conformance approver | Done | Confirms | Semih (product owner) — 2026-08-03 |

Tüm 30 revalidation görevi `V0-GOV-036` ile uzlaştırılmış olup, hiçbir açık conflict kalmamıştır.
