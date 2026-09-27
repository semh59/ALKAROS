# V1-RMD-359 - Manuel kart onayı: kararı veren/karar zamanı kısıtı artık tek yönlü boolean eşitliği kullanmıyor

- Task ID: V1-RMD-359
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Derin denetim taraması (2026-09-27) sırasında bulundu: `payments.manual_card_confirmations` tablosunun
(`V1-RMD-283`, migration 143) `ck_manual_card_decided_consistency` kısıtı,
`(status = 'Pending') = (decided_by IS NULL AND decided_at IS NULL)` şeklinde tek yönlü bir boolean eşitliğiydi
— bu, YALNIZCA "Pending ise ikisi de NULL olmalı" kuralını zorluyordu, "Approved/Rejected ise ikisi de NOT NULL
olmalı" kuralını HİÇ zorlamıyordu (`status <> 'Pending'` yalnızca "ikisi birlikte NULL değil" demek, "ikisi
birlikte SET" demek değil). Bu, aynı gün V12-ONL-011'de bulunup düzeltilen TAM AYNI hata sınıfı
(`ck_platform_store_status_closure`) — bu kez PARA ile ilgili bir tabloda: bir müdürün "kart çekildi" onayı/reddi
(dört-göz ilkesi), kim onayladığı VEYA ne zaman onaylandığı bilgisinden biri eksik kalarak veritabanına
yazılabiliyordu.

Gerçek test Postgres'ine karşı doğrulandı: eski kısıt, `status='Approved'` + `decided_by` SET + `decided_at`
NULL (ve simetriği, ve `Rejected` için aynı iki eksik-yarı durumu) gibi 4 geçersiz satırın TAMAMINI yanlışlıkla
kabul ediyordu.

Migration 143'ün kendi `.up.sql` dosyası DOĞRUDAN düzenlenmedi: `MigrationExecutor.cs` her uygulanan `.up.sql`
dosyasını hem apply hem rollback sırasında checksum'lıyor — geçmiş bir forward dosyasını değiştirmek, onu zaten
çalıştırmış herhangi bir ortamın checksum kontrolünü bozardı. Düzeltme, kısıtı yerinde onaran YENİ bir migration
(158) olarak eklendi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- `database/migrations/V1/V1-RMD-359/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reconciliation/ManualCardConfirmationConstraintTests.cs
- `plan/v1/remediation/V1-RMD-359-manual-card-decided-consistency-constraint-fix.md`

## In scope

1. Yeni migration 158: `ck_manual_card_decided_consistency`'yi düşürüp iki mutlaka ayrık dala bölünmüş haliyle
   yeniden ekliyor — `(status = 'Pending' AND ikisi de NULL) OR (status <> 'Pending' AND ikisi de NOT NULL)`.
2. `order.json`/`MigrationManifest.cs` (`PhaseBMax` 157→158)/`ManifestTests.cs` migration konumu güncellemeleri.
3. Yeni `ManualCardConfirmationConstraintTests.cs` (`tests/Host/Experience/Reconciliation/` — bu proje ZATEN
   tam migration manifestini uygulayan `ReconciliationCaseTestDatabase`'i kullanıyor): gerçek bir `billing.bills`
   ve `payments.payments` satırı tohumlayıp (yabancı anahtar atlanmadan), 4 geçersiz kombinasyonun reddedildiğini
   ve 3 geçerli kombinasyonun (Pending/Approved/Rejected, her biri doğru şekilde) kabul edildiğini kanıtlıyor.

## Out of scope

1. `payments.manual_card_confirmations` için C# tarafında (uygulama katmanında) mevcut olmayan hiçbir davranış
   değişikliği — bu tablo için mevcut servis (`ManualCardConfirmationService`) zaten ikisini HER ZAMAN birlikte
   yazıyor; bu tamamen veritabanı seviyesinde bir savunma-derinliği düzeltmesi.
2. Denetim sırasında AYRICA bulunan, bu göreve tamamen alakasız iki başarısız test
   (`OnlineOrderReconciliationHttpTests.ARefusedProviderOrderBecomesACaseItsRetryReprocessesAndItResolvesOnlyWhenTheSourceAgrees`,
   `OnlineProblemsHttpTests.AManagerRetriesTheSafeActionAndCanResolveOnlyOnceTheSourceAgrees`, ikisi de
   "Expected: OK, Actual: ServiceUnavailable") — `git stash` ile temiz (bu görevden önceki) ağaç üzerinde de
   birebir aynı şekilde başarısız olduğu doğrulandı; önceden var olan, bu görevin kapsamı dışında bir bulgu,
   ayrı olarak takip edilmeli.

## Dependencies

- None

## Acceptance evidence

- Gerçek test Postgres'ine (`alkaros-test-pg`) karşı doğrudan SQL doğrulaması: eski kısıt metniyle 4 geçersiz
  satırın (Approved/Rejected × decided_by-veya-decided_at eksik) TAMAMI yanlışlıkla kabul edildi; yeni kısıt
  metniyle aynı 4 satır `23514 check_violation` ile reddedildi, 2 geçerli satır (Open ⟺ boş, Approved/ClosedToday
  benzeri ⟺ dolu) kabul edildi.
- `tests/Host/Experience/Reconciliation/ManualCardConfirmationConstraintTests.cs`: 7/7 geçti (4 reddedilen +
  3 kabul edilen kombinasyon).
- `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (`ManifestTests` dahil): regresyon kontrolü için
  ayrıca çalıştırıldı.
- Mutation-check: `order.json`'dan migration 158 girdisi geçici olarak kaldırıldı (dosya yedeklendi), yeni
  testin 4 "reddet" senaryosunun TAMAMI gerçekten kırmızı oldu (`Assert.Throws() Failure: No exception was
  thrown` — eski kısıt gerçekten kabul ediyordu); `order.json` yedekten geri yüklendi, `diff` ile bayt-eşit
  olduğu doğrulandı, testler tekrar 7/7 yeşile döndü.
- Migration 158, gerçek boş bir Postgres veritabanında ileri, geri (eski kısıt metnine döner), tekrar ileri
  denendi — üçü de sıfır hata.
- `dotnet build ALKAROS.slnx`: 0 uyarı, 0 hata.

## Handoff

- None
