# V13-TBL-001 - Implement payment-aware table topology

- Task ID: V13-TBL-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: integration
- Surface state: Existing

## Source basis

- PDF:I.49
- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

Payment durumu bulunan Bill için table transfer, merge ve bill mutation işlemlerini fail-closed policy ile yönetmek.

## Owned surface

- `src/Modules/Tables/PaymentTopology/**`, `tests/Modules/Tables/PaymentTopology/**`
  (görevin özgün metni "src/Modules/TableManagement/**" yazıyordu — bu dizin
  hiç var olmadı, repodaki gerçek modül dizini "Tables" idi; kapanışta
  düzeltildi, V13-PUI-001/PUI-004/bu görev serisindeki diğer görevlerin
  aynı türden düzeltmeleriyle aynı desen).
- Bu görev temel table, Bill, Payment veya allocation schema'sını değiştiremez
  (hiçbir migration eklenmedi — yalnız mevcut `payments.payments` tablosu
  okunuyor).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs,
  src/Modules/Tables/TableTransfer/TableTransferExceptions.cs,
  src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Merge.cs,
  src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Unmerge.cs,
  src/Modules/Tables/TableMerge/TableMergeExceptions.cs,
  tests/Modules/Tables/TableTransfer/PostgresTableTransferTests.cs,
  tests/Modules/Tables/TableTransfer/ALKAROS.Tables.TableTransfer.Tests.csproj,
  tests/Modules/Tables/TableMerge/PostgresTableMergeTests.cs,
  tests/Modules/Tables/TableMerge/ALKAROS.Tables.TableMerge.Tests.csproj,
  ALKAROS.slnx
  (V1-TBL-002/V1-TBL-003 sahipliğinde kalır — bu görev yalnız her ikisinin
  kendi "V1.3 payment policy" placeholder çağrısını, kendi doc-comment'lerinin
  zaten bu görevi işaret ettiği yeni policy çağrısıyla değiştirdi; domain
  mantığının geri kalanı, migration'lar ve transfer/merge/unmerge'in kendi
  akışı değişmedi).

## In scope

- Pending/Unknown lock, partially-paid transfer/merge policy, optimistic concurrency ve auditable typed rejection.

## Out of scope

- Unpaid table topology, payment provider transport, allocation hesaplama ve UI.

## Dependencies

- V1-TBL-002
- V1-TBL-003
- V13-PAY-004
- V13-ALC-002
- V1-FND-005

## Deliverables

- `src/Modules/Tables/PaymentTopology/PaymentAwareTableTopologyPolicy.cs` — Pending/Unknown/
  ReconciliationRequired kilidi (`EnsureNoUnsettledPaymentAsync`/`EnsureNoUnsettledPaymentForMergeAsync`),
  transfer/merge/unmerge'in kendi transaction'ı içinde, kendi table-row kilitlerinden SONRA çağrılır.
- `PostgresTableTransferRepository.ExecuteTransferAsync`, `PostgresTableMergeRepository.
  ExecuteMergeAsync`, `PostgresTableMergeRepository.ExecuteUnmergeAsync`'in kendi eski
  "herhangi bir allocated/paid tutar veya Open-olmayan status engeller" placeholder'ı
  (V1-TBL-002/003'ün kendi `PaymentPolicyRequiredException` doc-comment'lerinin zaten bu
  görevi işaret ettiği yer) yeni policy çağrısıyla değiştirildi.
- `tests/Modules/Tables/PaymentTopology/**` (yeni proje, 10 test) + `TableTransfer`/`TableMerge`
  test projelerinde güncellenmiş/eklenmiş testler (Pending/Unknown/ReconciliationRequired
  reddi, partially-paid+settled başarı, concurrent mutation ve stale version — mevcut
  testler zaten kapsıyordu, değişmedi).

## Acceptance evidence

**Kritik mimari bulgu (kodu okuyarak doğrulandı, varsayılmadı):** Bu domain'de bir Bill'in
`bill_id`'si transfer/merge/unmerge sırasında ASLA değişmiyor — yalnız hangi masanın
`current_bill_id` pointer'ı o Bill'i gösterdiği (senkron adım) ve, event teslim edildiğinde,
`billing.bills.table_id`'nin kendisi (`IBillRepository.ReparentActiveBillsToTableAsync`,
YALNIZ `TableEventBillConsumer`'dan çağrılır — bu da YALNIZ bu görevin kapısından geçmiş bir
transfer/merge'ün ürettiği `TableTransferred`/`TableMerged` outbox event'i teslim edildiğinde
tetiklenir). Merge, iki ayrı Bill'i tek bir YENİ Bill'de BİRLEŞTİRMİYOR — her iki Bill kendi
kimliğini koruyor, yalnız birincil masa artık ikisine de (ardışık olarak) "current" pointer'ı
taşıyabiliyor (`ConsolidatedBillIds` listesi). Bu yüzden `billing.bill_allocations` (bill_id'ye
sabit-anahtarlı) transfer/merge/unmerge tarafından ASLA taşınmıyor/yeniden atfedilmiyor — "yanlış
Bill'e taşınmaz" acceptance kriteri, bir taşıma rutini YAZARAK değil, hiçbir taşıma rutini
YAZMAYARAK sağlanıyor (allocation hesaplama zaten out-of-scope'tu). **Tasarım kararı:** Politika
yalnız gerçek riski (bir Bill'in Pending/Unknown/ReconciliationRequired durumunda bir Payment'ı
varken masasının değişmesi — sonucun hangi masa/context'e karşı geldiği belirsizleşir) engelliyor;
salt kısmi ödenmiş (allocated/paid>0 ama bilinen her Payment zaten Approved/Declined/Cancelled)
bir Bill artık serbestçe taşınabiliyor, önceki placeholder'ın aksine.

- Pending veya Unknown/ReconciliationRequired payment sırasında transfer, merge ve unmerge —
  `payments.payments` üzerinden gerçek zamanlı okunarak — hiçbir ilişkiyi değiştirmeden
  reddedilir (`PaymentAwareTableTopologyPolicy` + gerçek Postgres testleri: `ExecuteTransferPendingPaymentOnBillThrowsPaymentPolicyRequiredExceptionAndRollsBack`,
  `ExecuteTransferUnsettledPaymentOnBillThrowsPaymentPolicyRequiredException`,
  `MergeWithPendingPaymentOnParticipantThrowsPaymentPolicyRequiredExceptionAndRollsBack`).
  "Bill mutation" yüzeyi araştırıldı: tek gerçek mutasyon `ReparentActiveBillsToTableAsync`
  (async outbox tüketicisi) — senkron kapı geçilmeden event hiç kuyruklanmadığı için bu da
  dolaylı olarak korunuyor; ayrı bir "bill content" mutasyon yüzeyinde (kalem ekleme/çıkarma)
  payment-aware bir kilit gerektiren gerçek bir kod yolu bulunamadı.
- Partially-paid, tüm bilinen Payment'ları settled (Approved/Declined/Cancelled) bir Bill artık
  serbestçe transfer/merge ediliyor, allocation'ı bozulmadan
  (`ExecuteTransferPartiallyPaidBillWithSettledPaymentSucceeds`,
  `MergeWithAllocatedBillAndNoUnsettledPaymentSucceeds` — allocation tutarı işlem sonrası
  aynı, aynı bill_id'ye bağlı doğrulandı).
- Optimistic concurrency (stale row-version reddi) ve gerçek Postgres kanıtı: mevcut
  `TableTransferConcurrencyException`/`TableMergeConcurrencyException` testleri değişmeden
  geçmeye devam ediyor (bu görev bu mekanizmayı hiç değiştirmedi, zaten doğruydu).
- Gerçek testler: `ALKAROS.Tables.PaymentTopology.Tests` 10/10 (yeni),
  `ALKAROS.Tables.TableTransfer.Tests` 35/35, `ALKAROS.Tables.TableMerge.Tests` 29/29,
  `ALKAROS.Payments.Allocations.Persistence.Tests` 13/13, `ALKAROS.Architecture.Tests`
  (ModuleBoundaries) 9/9, tam `ALKAROS.Host.Tests` (MigrationComposition) 161/161 — hepsi
  gerçek `alkaros-test-pg` Postgres'e (port 55432) karşı, bizzat çalıştırılıp izlendi.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı (919 markdown,
  897 task dosyası, 2001 dependency edge).
- `python tools/consistency-audit/consistency_audit.py` → temiz (bir Türkçe karakter kaçağı
  bir doc-comment'te bulunup düzeltildi, ikinci koşuda temiz).
- `python tools/project-manifest/project_manifest_tool.py` → VALID (0 fark).
- Yeni bir `IModule`/DI kaydı gerekmedi — `PaymentAwareTableTopologyPolicy` mevcut
  `ALKAROS.Tables` derlemesi içinde plain bir static class; `ModuleRegistry.DefaultCatalog`
  ve `HostModuleReachabilityTests`'in modül sayısı dokunulmadı.

## Handoff

- V20-UAT-001
- V20-UAT-002
