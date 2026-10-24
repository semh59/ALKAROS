# V1-RMD-724 - Orders doküman sapmaları, down betikleri ve sınır istisnası

- Task ID: V1-RMD-724
- Status: Done
- Assignee: claude-code-session_01XYCVWVTbShZKMmSzdAGR6K
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B008, B022, B023, B030 ve B031 kapanır. Dokümanlar koda hizalanır (kanonik katalogda Nfc ve Held, sipariş numarası biçimi, OrderStateChanged, api-contract örneği, void politikası, Measurements, projection-ownership adı); 077 ve 105 down betikleri değerleri geri alırken çökmez; Host'un Tables durumuna ham SQL yazması denetim aracında gerekçeli dar istisnaya bağlanır; B031'in alt kalemleri tek tek sınıflanır: doküman ve runbook olanlar burada kapanır, kod gerektirenler V1-RMD-725'e taşınır.

## Owned surface

- `plan/v1/remediation/V1-RMD-724-orders-doc-and-migration-drift.md`
- `evidence/V1-RMD-724/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/** - yalnız Orders ile ilgili sapmalar
- `database/migrations/V1/V1-RMD-724/**`
- `tests/Host/MigrationComposition/OrdersRollbackTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py - yalnız Tables durum yazımı istisnasının daraltılması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/** - yeni geri alma testi ve manifest listesine 206
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız 206 girişi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax 206
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Doküman hizalama, down betiği düzeltmeleri, denetim istisnası ve B031 alt kalemlerinin tek tek kapatılması. Mevcut migration dosyaları düzenlenmez; down düzeltmesi yeni migration ile yapılır.

## Out of scope

- İptal ve ikramın tek işlem ve idempotency işi V1-RMD-722'de yapıldı.
- Üretim davranışı değişikliği.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-724/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Denetim kanıtı: consistency_audit sıfır hata ve uyarı; down betikleri Held/Nfc satırı varken geri alınır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-725
