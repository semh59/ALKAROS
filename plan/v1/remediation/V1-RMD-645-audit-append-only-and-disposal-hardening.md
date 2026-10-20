# V1-RMD-645 - Denetim tablosunun değiştirilemezlik ve imha sağlamlaştırması

- Task ID: V1-RMD-645
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20). Tablo, toptan boşaltma komutuna karşı bölümler dahil ifade düzeyi tetikleyiciyle korunur; audit-disposal çalıştıran operatörün adı zorunlu olur ve olaya yazılır, eşzamanlı ikinci koşu güvenle reddedilir, silinen bölümün satır sayısı kilit altında tek kez sayılır ve hem konsola hem olaya aynı değer yazılır, varsayılan bölümde satır varsa komut uyarı koduyla biter, komut hataları Türkçe ve runbook bağlantılı verilir; kurtarma ve yedek runbook'ları ana anahtar, kurtarma sonrası imha, WAL budama ve uyarı yolu adımlarını içerir; runbook'taki eski "ertelendi" anlatımı düzeltilir. Kaynak: V1-RMD-595 bulguları F11, F12, F13, F14, F24, Y2.

## Owned surface

- `plan/v1/remediation/V1-RMD-645-audit-append-only-and-disposal-hardening.md`
- `evidence/V1-RMD-645/**`
- `database/migrations/V1/V1-RMD-645/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-595-audit-module-quality-audit.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız 194 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız 194 girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/PartitionDisposal/AuditPartitionDisposal.cs - yalnız imha sağlamlaştırması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız audit-disposal kolu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/HostComposition.cs - yalnız uyarı çıkış kodu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Program/AuditDisposalTests.cs - yalnız imha ve tetikleyici testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.ops.yaml - yalnız audit-disposal servisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Deployment/test_container_contract.py - yalnız audit-disposal sözleşmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md - yalnız audit bölümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/recovery/backup-restore-runbook.md - yalnız kurtarma sonrası adımlar
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/recovery/rpo-rto-targets.md - yalnız WAL budama
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız 173 ve 194 girdisi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yeni geri alınabilir göç 194: ana tabloda ve her mevcut bölümde ifade düzeyi boşaltma koruması; imha sınıfı: operatör adı, danışma kilidi, kilit altında tek sayım, uyarı çıkış kodu; Program.cs audit-disposal kolunda genişletilmiş hata yakalama ve Türkçe mesajlar; compose.ops.yaml servisine operatör adı; gerçek PostgreSQL testleri; runbook ve kurtarma dokümanı güncellemeleri.

## Out of scope

- Ayrı veritabanı rolü ve parola dağıtımı (uygulama ve imha aynı sahip rolle çalışır; kalan risk README'de belirtilir).
- Yasal tutma sınıfı (Ürün Sahibi kararı bekleyen konu).
- Başka görevin kaynak bulguları.

## Dependencies

- V1-RMD-644

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Göç 194 up ve down gerçek veritabanında çalıştırılmış, idempotency gösterilmiş.
3. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-645/` altındadır.

## Handoff

- None
