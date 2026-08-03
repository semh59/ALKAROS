# Decision Evidence Revalidation Register

2026-08-02 bağımsız kaynak denetimi, aşağıdaki V0 decision/validation
çıktılarında zorunlu erişim tarihi, named approver veya reddedilen alternatif
kanıtının eksik olduğunu doğruladı. Bu kayıt, eski artifact başlığında `Done`
yazsa bile aşağıdaki görevleri `Blocked` kabul eder; `GATE-V0-EXIT` ve hiçbir
consumer bu kayıtlar kapanmadan ilerleyemez.

| Task ID | Artifact | Eksik kanıt |
| --- | --- | --- |
| `V0-DAT-001` | `docs/data/migration-dependency-graph.md` | Named approver ve decision provenance |
| `V0-DAT-002` | `docs/data/canonical-value-catalog.md` | PDF state uyumu ve named approver |
| `V0-DAT-003` | `docs/data/nullable-unique-policy.md` | Decision provenance |
| `V0-DAT-004` | `docs/data/projection-ownership.md` | Decision provenance |
| `V0-DAT-005` | `docs/data/single-branch-key-strategy.md` | Decision provenance |
| `V0-DAT-006` | `docs/data/migration-rehearsal-profile.md` | Named approver |
| `V0-DOC-001` | `docs/specification/restaurant-pos-master.md` | Dependency closure ve source-bound baseline |
| `V0-DOM-001` | `docs/domain/lifecycle-transition-contracts.md` | PDF state uyumu ve provider boundary |
| `V0-DOM-002` | `docs/domain/bill-order-cardinality.md` | Tutarlı cardinality seçimi |
| `V0-DOM-003` | `docs/domain/refund-ledger.md` | Partial refund model kanıtı |
| `V0-DOM-004` | `docs/domain/payment-allocation-integrity.md` | Reversal representation seçimi |
| `V0-DOM-005` | `docs/domain/table-reservation-policy.md` | Decision provenance |
| `V0-DOM-006` | `docs/domain/void-complimentary-discount-policy.md` | Business approval ve threshold kaynağı |
| `V0-DOM-007` | `docs/domain/customer-credit-invoice-semantics.md` | Decision provenance |
| `V0-DOM-008` | `docs/domain/reporting-metrics.md` | Decision provenance |
| `V0-DOM-009` | `docs/domain/receipt-variance-policy.md` | Business approval |
| `V0-DOM-010` | `docs/domain/inventory-cost-basis.md` | Business approval |
| `V0-DOM-011` | `docs/domain/printer-routing-precedence.md` | Named approver |
| `V0-LIC-001` | `docs/licensing/licensing-contract.md` | Product/legal decision evidence |
| `V0-ARC-001` | `docs/architecture/module-dependency-rules.md` | Tek iletişim modeli ve approver |
| `V0-ARC-002` | `docs/architecture/local-first-sync-contract.md` | Decision provenance |
| `V0-ARC-003` | `docs/architecture/idempotency-inbox-outbox.md` | Decision provenance |
| `V0-ARC-004` | `docs/architecture/api-contract-standard.md` | Technology/policy source ve approver |
| `V0-ARC-005` | `docs/architecture/settings-ownership.md` | Decision provenance |
| `V0-ARC-006` | `docs/architecture/notification-delivery-matrix.md` | Transport/threshold business decision |
| `V0-ARC-007` | `docs/architecture/deployment-compatibility-matrix.md` | Named approver |
| `V0-ARC-008` | `docs/architecture/release-evidence-contract.md` | Named approver |
| `V0-ARC-009` | `docs/architecture/qr-relay-topology.md` | Topology decision evidence |
| `V0-CMP-002` | `docs/compliance/money-tax-business-date.md` | Tax/business decision evidence |
| `V0-CMP-005` | `docs/compliance/accessibility-target.md` | Conformance approver |
| `V0-SEC-001` | `docs/security/security-verification-baseline.md` | Threat model, selected ASVS level, approver |

Her satır ancak ilgili task Markdown'ı `Planned` durumuna alındıktan, artifact
source basis ile eşleştirildikten ve `TASK_STANDARD.md` karar sözleşmesinin tüm
alanları gerçek kanıtla doldurulduktan sonra bu kayıttan çıkarılabilir. Bu belge
business, legal veya provider sonucu seçmez.
