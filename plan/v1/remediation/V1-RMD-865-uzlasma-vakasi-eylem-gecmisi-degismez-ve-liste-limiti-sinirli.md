# V1-RMD-865 - Uzlaşma vakası eylem geçmişi veritabanı düzeyinde değişmez; vaka listesi limiti sınırlı

- Task ID: V1-RMD-865
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G10 (F0243, F0244, F0734, F0735, F1009): (1) reconciliation.case_actions için "geçmiş güncellenemez ve silinemez" kuralı yalnız uygulama kodunda: tablo üzerinde trigger ya da koruma yok (cash_transactions ve account_transactions gibi), bu yüzden bir hata ya da el müdahalesi denetim izini sessizce değiştirebilir. Satır tetikleyicisi (UPDATE/DELETE) ve tablo boşaltmaya karşı ifade tetikleyicisi (TRUNCATE) eklenir; yeni migration 220. (2) GET /api/v1/management/reconciliation/cases limit parametresi sınırsızdır: negatif değer Postgres hatası (500), çok büyük değer sınırsız sorgu verir; depo limiti 1 ile 500 arasına kısar (elle kart onayı listesiyle aynı sınır). (3) F0734, F0735 ve F1009 güncel kodda doğrulandı: aynı anahtarın Resolved ya da Dismissed vakadan sonra yeni vaka açmaması planın bilinçli kuralıdır (PostgresReconciliationRepository.CreateOrDeduplicateCaseAsync, sınama: mevcut depo testleri), "uq_..._active_dedup" kısmi indeksi bu kuralla birlikte duruyor; kusur değil, davranış değişmez ve kanıtla kapatılır (RESULT.md).

## Owned surface

- `evidence/V1-RMD-865/**`
- `database/migrations/V1/V1-RMD-865/220-reconciliation-case-actions-append-only.up.sql` — satır ve TRUNCATE tetikleyicileri.
- `database/migrations/V1/V1-RMD-865/220-reconciliation-case-actions-append-only.down.sql` — geri alma.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/MigrationComposition/order.json — 220 girdisi.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax 220.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — kimlik listesi, sayı, son girdi tabloları.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs — şema testi.
  - src/Modules/Reconciliation/CaseFoundation/PostgresReconciliationRepository.cs — limit kelepçesi.
  - tests/Modules/Reconciliation/CaseFoundation/PostgresReconciliationRepositoryTests.cs — limit ve değişmezlik testleri.
  - tests/Modules/Reconciliation/CaseFoundation/ALKAROS.Reconciliation.CaseFoundation.Tests.csproj — migration 220 dosyası.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Migration: case_actions üzerinde BEFORE UPDATE OR DELETE satır tetikleyicisi (istisna) ve BEFORE TRUNCATE ifade tetikleyicisi; down betiği ikisini ve işlevi kaldırır.
2. Depo: limit değeri 1..500 arasına kısılır.
3. Testler: UPDATE, DELETE ve TRUNCATE reddedilir; negatif ve çok büyük limit hata vermez, en çok 500 satır döner; manifest ve şema testleri.
4. Mutasyon: UPDATE tetikleyicisi, DELETE kolu, TRUNCATE tetikleyicisi, kelepçe alt ve üst sınırı (cp+cmp ile geri alınır).

## Out of scope

- Aynı anahtar için Resolved/Dismissed sonrası yeni vaka açma kuralı (bilinçli kabul, değişmez).
- Elle kart onayı listesi (zaten kısıtlı).

## Dependencies

- None

## Acceptance evidence

1. CaseFoundation, Host MigrationComposition ve Host Reconciliation testleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-865/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
