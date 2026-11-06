# V1-RMD-859 sonuç

Yalnız belgeler değişti (kod esas). Her satırın kod kanıtı:

| Belge | Düzeltme | Kanıt |
|---|---|---|
| cash-session-design.md | `ClosingDifference` yalnız fark sıfır değilse yazılır | src/Modules/Cash/SessionLifecycle/CashSessionLifecycleService.cs:165-170 (`if (difference != 0)`) |
| refund-ledger.md | `payment_reversals.reversal_id`; mali bağlantı alanı tabloda yok, `idempotency_key` var | database/migrations/V1/V1-RMD-507/182-payment-reversals.up.sql:3-19 |
| payment-allocation-integrity.md | fazla ödeme yalnız nakit; kalan tutar düzeltilmiş (indirim/bahşiş sonrası) tutardan; `change_amount = tendered_amount - approved_amount` | src/Modules/Cash/TenderHandler/CashTenderHandler.cs:129-134 (nakit: tutar kalanı aşarsa ret, el verilen tutar fazlası değişiklik alanında), src/Modules/Payments/Allocations/PaymentAllocationFactory.cs:48 ve src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs:143-145 (kart aşım reddi), src/Modules/Payments/EftTender/EftTenderHandler.cs:133 (havale aşım reddi), src/Modules/Payments/Allocations/Persistence/PostgresPaymentAllocationRepository.cs:86-89 (`AdjustedPayableAmount`), database/migrations/V1/V1-RMD-230/121-payments-cross-field-invariants.up.sql:19-20 |
| canonical-value-catalog.md | `payments.payment_method` kolonu yok; yöntem `PaymentMethodLookup` ile türetilir (Cash, Card, Eft, Account); `TenderMethod` Cash, BankCard, MealCard, Eft; yemek kartı işleyicisi yok | src/Host/Experience/Billing/PaymentMethodLookup.cs:12-35, src/Modules/Payments/TenderRouting/TenderMethod.cs:10-19 |
| migration-dependency-graph.md | PhaseBMax 219; `refund_ledger_entries` = `payments.payment_reversals`; `payment_providers` yok | src/Host/Composition/Migrations/MigrationManifest.cs:40; payment_providers için database/ altında CREATE TABLE yok |
| canonical-value-catalog.md (ikinci tur) | `reconciliation.cases`, `operations.backups`, yemek kartı tablosu kurulmadı, SplitPayment örneği | database/migrations/V1/.../030-reconciliation-cases.up.sql:10 ve `CREATE TABLE` taraması |
| payment-allocation-integrity.md (ikinci tur) | örnekler: kırpma yok, aşım reddedilir | CashTenderHandler.cs:129-134 |
| reporting-metrics.md | gerçek tablolar: `invoicing.invoices`, `reconciliation.cases`, `operations.backups` + `operations.offsite_backup_receipts`, `inventory.stock_movements` + `inventory.waste_records`; `bill_closures` yok (kapanış `bills.closed_at`); `meal_card_settlements` kurulmadı | database/migrations altında `CREATE TABLE` taraması |
| projection-ownership.md | yedi olay adının kodda karşılığı yok; gerçek yazarlar listelendi | `grep -rl <olay adı> src --include=*.cs` yedi adın hepsinde sıfır sonuç verdi; CatalogPriceRecomputeHostedService.cs:8-10, DailyMenuCounterIntegrationEvents.cs, PostgresAccountBalanceProjection.cs, BillClosureService.cs |

## Bu görevde değiştirilmeyenler
Token/QNB gerekçe notu (token-integration-path-decision.md, V13-GOV-007): 2026-10-17 araştırmasıyla çelişen bir karar belgesi; karar ürün sahibinindir, ana oturuma bildirilir.
