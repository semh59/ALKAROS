# V1-RMD-481 - Denetim kayıtlarının 10 yıl sonra bölüm bırakılarak silinmesi

- Task ID: V1-RMD-481
- Status: Done
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`audit.audit_events` tablosu veritabanı tetikleyicisiyle yalnız eklemeye açıktır; satır güncellenemez ya da silinemez. KVKK veri envanteri denetim kayıtları için
10 yıl sonra anonimleştirmeyi öngörür, bu yüzden yerinde anonimleştirme mümkün değildir. `kvkk-retention` komutu ve `V15-KVK-002` bu tabloyu kapsam dışı bırakır.
Bu görev tabloyu yıla göre bölümlemeyi ve 10 yılı dolan bölümü bırakmayı (tüm bölümü silmeyi) tasarlar ve uygular. Tasarım, mevcut tetikleyici korumasını
zayıflatmamalı ve yasal saklama gerektiren kayıtları saklama süresinden önce silmemelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-481-audit-log-ten-year-disposal.md`
- `database/migrations/V1/V1-RMD-481/**`
- `src/Modules/Audit/PartitionDisposal/**`
- `evidence/V1-RMD-481/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs — yalnız `audit-disposal` komutu
- `tests/Host/MigrationComposition/Program/AuditDisposalTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md — yalnız denetim kaydı bölümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md ve tools/consistency-audit/consistency_audit.py — yalnız gerekirse yeni yüzeyin kaydı
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Migration: `audit.audit_events` tablosu `occurred_at` yılına göre bölümlenir (birincil anahtar `(id, occurred_at)`); veri kaybetmeden taşınır, bölümler uzak geleceğe kadar önceden açılır, kapsam dışı tarihler için varsayılan bölüm vardır (denetim yazımı hiçbir zaman bölüm eksikliğinden hata vermez); ekleme-yalnız tetikleyicisi her bölümde korunur; geri alma satırları korur.
- Bir yıl bölümü, o yılın bitiminden 10 yıl geçtikten sonra (yıl Y için 1 Ocak Y+11, UTC) `DROP` ile bırakılır; varsayılan bölüm asla bırakılmaz. Komut varsayılan olarak kuru çalıştırmadır; `--apply` ile bırakır ve bırakılan bölümün adını ve satır sayısını (içerik olmadan) `audit.audit_events` içine olay olarak yazar. Yasal tutma tablosunda denetim sınıfı yoktur; bu görev genişletmez.

## Out of scope

- Denetim kayıtlarının içeriğinin değiştirilmesi; `V15-KVK-002` alanları.

## Dependencies

- V15-KVK-002

## Acceptance evidence

- Testler ve gerçek Postgres denemesi; çıktılar `evidence/V1-RMD-481/` altındadır.

## Handoff

- None
