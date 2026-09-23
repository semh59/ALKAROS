# V1-RMD-258 - Faz 2 payments independent audit remediation (money-safety, architecture, dead-hook fixes)

- Task ID: V1-RMD-258
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Yeni kapanan "Faz 2" ödeme inisiyatifinin (`V13-GOV-008`, `V13-PAY-003/004/005`,
`V13-PUI-001/004`, `V13-TBL-001`, `V13-REC-001`, `V13-RPT-001`, commit'ler
`b5c35544`..`763f6ce8`) 6 bağımsız ajanla (domain mantığı,
database/SQL/transaction, mimari/DI/modül sınırları, frontend/UI,
plan/governance, finansal güvenlik) denetimi gerçek, bağımsız doğrulanmış
sorunlar buldu. Bu görev, domain-mantığı/database/finansal-güvenlik
açısından doğrulanmış olanları düzeltiyor — önce kök neden, sonra
sonuçları:

1. **`ICardSettlementOrchestrator` (V13-PAY-004) hiçbir gerçek çağırana
   sahip değildi** — tam olarak inşa edilmiş, DI'a kayıtlı ve test edilmiş,
   ama generic tender HTTP endpoint'i (V13-PUI-001) BankCard
   `TenderHandlerResult`'ını hiç kalıcılaştırmadan doğrudan çağırana
   yansıtıyordu. `AGENTS.md`'nin yasakladığı, gerçek anlamda kullanılmayan
   bir "future hook".
2. **1 numaranın sonucu**: Unknown/RequiresReconciliation durumundaki bir
   BankCard denemesi veritabanında hiçbir iz bırakmıyordu — "mükerrer
   tahsilata tekrar denemeyi engelle" kilidi yalnız tarayıcının kendi JS
   belleğinde (`split-payment.js`'in `state.locked`'ı) yaşıyordu ve sıradan
   bir sayfa yenilemesinde bile sessizce kayboluyordu.
3. **`PaymentAwareTableTopologyPolicy` (V13-TBL-001) kendi kodunun
   sağlamadığı bir eşzamanlılık garantisi iddia ediyordu** — kilitsiz düz
   bir `SELECT`, ödeme-yazma tarafıyla paylaşılan hiçbir kilit yok. Üç
   bağımsız denetçi tarafından gerçek, yapısal bir TOCTOU boşluğu olarak
   doğrulandı (bu görevden ÖNCE sömürülemezdi, çünkü hiçbir canlı yol bir
   Payment'ı gözlemlenebilir bir süre `Unknown` durumunda bırakmıyordu —
   ama 1 numara bu düzeltme olmadan yapılsaydı gerçek bir risk olacaktı).
4. **`OverAllocationException` HTTP katmanında hiç yakalanmıyordu** —
   eşzamanlılık kaynaklı bir over-allocation (istemci-taraflı ön kontrol
   geçmiş, eşzamanlı bir istek gerçek per-bill advisory kilidi önce
   kazanmış) ham, Türkçe olmayan bir 500 olarak ortaya çıkıyordu, istemci
   taraflı tespit edilen aşırı tahsilatın aldığı aynı `TENDER_OVER_ALLOCATION`
   409 yerine.
5. **`payments.card_settlement_attempts`'te `bill_id` sütunu yoktu** — iki
   farklı hesap için tekrar kullanılan bir idempotency key (bir
   key-üretim hatası) yanlış hesabın gerçek sonucunu geçerli bir replay
   gibi döndürebilirdi. Aynı sınıftan bir boşluk, savunma amaçlı olarak
   `PostgresPaymentAllocationRepository`'nin kendi idempotency-key replay
   yoluna da eklendi.
6. **`card_settlement_attempts.allocation_id`'nin `UNIQUE` kısıtı yoktu** —
   şemanın kendisinde iki farklı deneme satırının aynı tahsisi
   referanslamasını engelleyen hiçbir şey yoktu (bugün orkestratörün kendi
   mantığı üzerinden erişilemez, ama gerçek, ucuza kapatılabilir bir
   veritabanı seviyesi boşluk).

Aynı denetimden iki bulgu daha (çakışan bir `C101` traceability kimliği ve
`V13-PAY-003`'ün `V13-GOV-008`'in ayrılmış `plan_audit_tool.py` yüzeyine
beyan etmeden yazması) ayrı, paralel bir governance-odaklı görevde
düzeltiliyor ve bilinçli olarak **bu görevin kapsamı dışında** — bu görevin
kendi Owned surface'ında tekrarlanmadı.

## Owned surface

- `database/migrations/V1/V1-RMD-258/141-card-settlement-attempts-bill-scoping.up.sql`
- `database/migrations/V1/V1-RMD-258/141-card-settlement-attempts-bill-scoping.down.sql`
- `evidence/V1-RMD-258/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs,
  src/Modules/Payments/CardSettlement/CardSettlementAttempt.cs,
  src/Modules/Payments/CardSettlement/CardSettlementExceptions.cs,
  src/Modules/Payments/CardSettlement/PostgresCardSettlementAttemptRepository.cs
  (V13-PAY-004 sahipliğinde kalır) — bill_id alanı, bill-scoped kilit ve
  zaten-çözülmemiş-ödeme kontrolü eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/EftTender/EftTenderHandler.cs,
  src/Modules/Payments/EftTender/EftTenderExceptions.cs
  (V13-PAY-005 sahipliğinde kalır) — aynı bill-scoped kilit ve
  zaten-çözülmemiş-ödeme kontrolü eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/PaymentTopology/PaymentAwareTableTopologyPolicy.cs
  (V13-TBL-001 sahipliğinde kalır) — aynı bill-scoped kilit gerçek transfer/merge/unmerge
  çağrılarından önce alınacak şekilde eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PostgresPaymentAllocationRepository.cs
  (V13-ALC-001 sahipliğinde kalır) — yalnız idempotency-key replay yoluna
  cross-bill mismatch kontrolü eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde kalır) — BankCard yolu artık ICardSettlementOrchestrator'a
  bağlandı, yeni exception eşlemeleri ve GET yanıtına gerçek unsettledPayment alanı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs, tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (V1-FND-004 sahipliğinde kalır) — yeni migration pozisyonu (141) kaydı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/CardSettlement/Fixtures/CardSettlementTestDatabase.cs,
  tests/Modules/Payments/CardSettlement/ALKAROS.Payments.CardSettlement.Tests.csproj
  (V13-PAY-004 sahipliğinde kalır) — migration 141'in fixture'a bağlanması.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/PaymentTopology/PaymentAwareTableTopologyPolicyTests.cs
  (V13-TBL-001 sahipliğinde kalır) — yeni bill-scoped kilit eşzamanlılık testi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reconciliation/Payments/ALKAROS.Reconciliation.Payments.Tests.csproj,
  tests/Modules/Reconciliation/Payments/PaymentReconciliationScannerTests.cs
  (V13-REC-001 sahipliğinde kalır) — yeni bill_id sütununu besleyen fixture düzeltmesi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj,
  tests/Modules/Reporting/Payments/Fixtures/PaymentReportTestDatabase.cs
  (V13-RPT-001 sahipliğinde kalır) — yeni bill_id sütununu besleyen fixture düzeltmesi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/Allocations/Persistence/PaymentAllocationTests.cs
  (V13-ALC-001 sahipliğinde kalır) — yeni cross-bill idempotency-key regresyon testi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde kalır) — yeni kalıcılık/kilit/eşzamanlı-over-allocation testleri.

## In scope

1. `CardSettlementOrchestrator` gerçekten çağrılıyor: `DualScreenApplication.Payments.cs`'in
   BankCard yolu artık `ICardSettlementOrchestrator.HandleAsync` üzerinden
   kalıcılaştırıyor (Approved/Declined/RequiresReconciliation, V13-PAY-004'ün
   tam kendi tasarımıyla).
2. Yeni, gerçek bir server-side "zaten çözülmemiş ödeme var" kontrolü
   (`CardSettlementUnsettledPaymentExistsException`/`EftUnsettledPaymentExistsException`)
   — hem `CardSettlementOrchestrator` hem `EftTenderHandler`'da, aynı
   bill-scoped advisory kilit altında.
3. GET `.../tenders/` artık gerçek `unsettledPayment` alanını dönüyor
   (`UnsettledPaymentV1`) — istemci artık kilidini sayfa yenilemesinde
   sunucudan yeniden türetebiliyor.
4. `PaymentAwareTableTopologyPolicy`, `CardSettlementOrchestrator` ve
   `EftTenderHandler` artık aynı `"bill-settlement:{billId:N}"` advisory
   kilidini paylaşıyor (Tables'ın Payments'a derleme-zamanı referansı
   olmadan, yalnız string kuralıyla).
5. `OverAllocationException`/`CrossBillPaymentAllocationException`/yeni
   mismatch exception'ları artık HTTP katmanında yakalanıp mevcut Türkçe
   409 sözleşmesine eşleniyor.
6. Migration 141: `card_settlement_attempts.bill_id` (NOT NULL, gerçek
   verilerden backfill edilmiş, FK) + `allocation_id`'ye kısmi UNIQUE index.
7. `EnsureReplayMatches`/`AllocateAsync`'in idempotency replay yolu artık
   `BillId` uyuşmazlığını da kontrol ediyor.

## Out of scope

- `V13-PUI-004`'ün kendi checkbox/focus-yönetimi kusurları (odak kaybı,
  checkbox boyutu, accessibility-target sınıflandırması) — ayrı, paralel
  bir remediation görevinin kapsamında.
- `plan/TRACEABILITY.md`'nin `C101` çakışması ve `V13-PAY-003`'ün
  `plan_audit_tool.py`'ye izinsiz yazımı — ayrı, paralel bir governance
  remediation görevinin kapsamında.
- Cash'in kendi tender akışına (`CashTenderHandler`, V13-CSH-003) bu görevin
  bill-scoped kilidini eklemek — Cash zaten tek bir atomik transaction
  içinde senkron olarak `Approved`'a ulaşıyor, hiçbir zaman `Unknown`/`Pending`
  durumunda gözlemlenebilir kalmıyor, bu yüzden aynı TOCTOU riski taşımıyor;
  bilinçli olarak dokunulmadı, gelecekte Cash de asenkron bir yola
  taşınırsa yeniden değerlendirilmeli.

## Dependencies

- None

## Deliverables

- Migration 141 (up/down, gerçek Postgres'te doğrulandı).
- `CardSettlementOrchestrator`/`EftTenderHandler`/`PaymentAwareTableTopologyPolicy`
  güncellemeleri.
- `DualScreenApplication.Payments.cs`'in BankCard yolu + yeni exception
  eşlemeleri + `unsettledPayment` alanı.
- 9 yeni/güncellenmiş test (1 cross-bill allocation testi, 1 bill-scoped
  kilit eşzamanlılık testi — devre dışı bırakılıp gerçekten kırıldığı
  kanıtlandıktan sonra geri eklendi, 4 yeni HTTP testi — 2'si kalıcılık/kilit
  ispatı, 1'i eşzamanlı over-allocation ispatı, 1'i boş unsettledPayment
  kontrolü, 3 fixture düzeltmesi).

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (tam solution).
- Gerçek Postgres'e (`alkaros-test-pg`, 55432) karşı, kişisel olarak
  çalıştırılıp izlenen sonuçlar:
  - `ALKAROS.Payments.CardSettlement.Tests` → 8/8
  - `ALKAROS.Payments.EftTender.Tests` → 11/11
  - `ALKAROS.Payments.TenderComposition.Tests` → 8/8
  - `ALKAROS.Tables.PaymentTopology.Tests` → 11/11 (yeni eşzamanlılık testi dahil)
  - `ALKAROS.Reconciliation.Payments.Tests` → 7/7
  - `ALKAROS.Reporting.Payments.Tests` → 9/9
  - `ALKAROS.Payments.Allocations.Persistence.Tests` → 14/14 (yeni cross-bill testi dahil)
  - `ALKAROS.Cash.TenderHandler.Tests` → 6/6
  - `ALKAROS.Payments.PaymentAggregate.Tests` → 43/43
  - `ALKAROS.Payments.TenderRouting.Tests` → 28/28
  - `ALKAROS.Architecture.Tests` (ModuleBoundaries) → 9/9
  - `ALKAROS.Host.Experience.PaymentTender.Tests` → 16/16 (11 önceki + 5 yeni)
- **Eşzamanlılık kilidi testi gerçekten doğrulandı (V1-WTR-024/028 disiplini)**:
  `PaymentAwareTableTopologyPolicy`'nin kilit çağrısı geçici olarak
  yorum satırına alındı → yeni test gerçekten FAIL etti (`stillHeld` true
  döndü) → kilit geri eklendi → test tekrar geçti (11/11). Bu, testin
  gerçekten bir şey kanıtladığını, boş (vacuous) olmadığını gösteriyor.
- **Eşzamanlı over-allocation testi gerçek iki eşzamanlı HTTP isteğiyle**
  (`Task.WhenAll`, sıralı değil) çalıştırıldı: kazanan 200, kaybeden gerçek
  Türkçe `TENDER_OVER_ALLOCATION` 409 (ham 500 değil) — 1/1 geçti.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
