# V13-PAY-001 - Implement Payment aggregate

- Task ID: V13-PAY-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.11-I.15
- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8

## Goal

V0 finansal sözleşmeleri kapsamında payment kimliğini, kanonik status geçişlerini ve para alanlarını uygulayın.

## Owned surface

- `src/Modules/Payments/PaymentAggregate/**`, `src/Modules/Payments/ALKAROS.Payments.csproj`,
  `tests/Modules/Payments/PaymentAggregate/**`, `database/migrations/V13/V13-PAY-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - ALKAROS.slnx — yeni `ALKAROS.Payments.csproj` ve
    `ALKAROS.Payments.PaymentAggregate.Tests.csproj` girdileri eklenir.
  - src/Host/ALKAROS.Host.csproj — `ALKAROS.Payments.csproj`'a bir
    `ProjectReference` eklenir (diğer tüm modüllerle aynı desen).
  - src/Host/Composition/Modules/ModuleRegistry.cs — `DefaultCatalog`'a
    `PaymentAggregateModule` tek satır eklenir (diğer modüllerle aynı desen).
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration
    120 kaydı, `PhaseBMax` güncellemesi ve testin sabit id/count listesine
    120 eklenmesi (V1-CDP-001'in migration 119 için yaptığıyla aynı desen).
  - `**/packages.lock.json` — `ALKAROS.Host.csproj`'a yeni bir
    `ProjectReference` eklenince `dotnet restore --force-evaluate`,
    `RestoreLockedMode` altında Host'a geçişli olarak bağımlı her test
    projesinin kilit dosyasına yalnız `ALKAROS.Payments` girdisini ekledi
    (mekanik, paket sürümü değişikliği yok — `git diff --stat` ile
    doğrulandı).

## In scope

- Talep edilen, onaylanan, tender edilen, değişiklik ve para birimi değişmezleri; geçiş geçmişi ve satır sürümü.

## Out of scope

- PaymentAllocation, provider aramaları ve geri ödemeler.

## Dependencies

- V0-CMP-002

## Deliverables

- `src/Modules/Payments/PaymentAggregate/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kanonik geçiş matrisi (V0-DOM-001) `Payment.CanTransitionTo`/adlandırılmış
  geçiş metotları (`Tender`/`Approve`/`Decline`/`Cancel`/`MarkUnknown`/
  `RequestReconciliation`) ile birebir uygulandı: `Initiated→Pending`,
  `Pending→Approved|Declined|Cancelled|Unknown`, `Unknown→
  ReconciliationRequired`, `ReconciliationRequired→Approved|Declined|
  Cancelled`. `Refunded`/`PartiallyRefunded` enum'da var ama bu görevden
  ulaşılamaz (V0-DOM-003/V13-ALC-003/004'ün kapsamı — testle kanıtlandı).
- Geçersiz status/para kombinasyonları reddedildi: talep edilen tutar
  ≤0, `Initiated` iken tendered dolu/`Pending`+ iken tendered boş,
  onaylanan tutar tendered'ı aşıyor, `change_amount` `tendered-approved`
  ile uyuşmuyor, onaydan önce change_amount≠0 — hepsi
  `InvalidPaymentAmountException` ile reddediliyor (hem C# constructor'da
  hem veritabanı CHECK kısıtlarında, defense-in-depth).
- Sıfır/negatif tutarlı tender reddedilir (`TenderRejectsZeroAmount`,
  `TenderRejectsNegativeAmount`); V0-DOM-004'ün overpayment örneği
  (payable 80, tendered 100 → approved 80, change 20) ve multi-payment cap
  örneği (A:50/50, B:50→30 capped, change 20) testle birebir doğrulandı.
- Payment geçmişi değişmez: her geçiş `PaymentStatusHistoryEntry` olarak
  eklenir, önceki entry'ler asla değişmez/silinmez (append-only, Order'ın
  `OrderStatusHistoryEntry` deseniyle aynı); satır sürümü (`row_version`)
  optimistic concurrency ile korunur (`SaveWithStaleRowVersionFailsClosed`).
- Para birimi açık: `CurrencyCode` alanı zorunlu, boş bırakılamaz.
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata (yeni
  `ALKAROS.Payments` modülü dahil tüm çözüm).
- Gerçek Postgres'e karşı (`ALKAROS_TEST_PG_PORT=55432`)
  `ALKAROS.Payments.PaymentAggregate.Tests` → 37/37 geçti (30 saf domain
  testi + 7 gerçek repository testi: round-trip, tender/approve sonrası
  save+history, stale row-version reddi, bill başına çoklu payment, FK
  ihlali reddi, DB seviyesinde CHECK kısıtı reddi).
- `ManifestTests` → 16/16 (migration 120 kaydı sayaç/id listesini
  bozmadı); `DualScreenStoreTests` (tam migration zincirini gerçek
  Postgres'e uygulayan bağımsız bir test seti) → 12/12, migration 120'nin
  gerçek migration sırasına sorunsuz eklendiğinin kanıtı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID (yeni
  `ALKAROS.Payments`/`ALKAROS.Payments.PaymentAggregate.Tests`
  projelerinin slnx/disk/ProjectReference tutarlılığı doğrulandı).
- `git status --short` → yalnız Owned surface ve deklare edilen Sınırlı
  ek yollarında değişiklik var (slnx, Host.csproj, ModuleRegistry.cs,
  order.json/MigrationManifest.cs/ManifestTests.cs, ve
  `--force-evaluate` restore'un mekanik olarak güncellediği
  `packages.lock.json` dosyaları).

## Handoff

- V13-PAY-002
- V13-ALC-001
