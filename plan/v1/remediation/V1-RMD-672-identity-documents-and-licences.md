# V1-RMD-672 - Identity belgeleri, lisans politikası, saklama süreleri ve yedekten dönüş adımı

- Task ID: V1-RMD-672
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B033, B034, B036 ve B027 kapanır; Ürün Sahibinin 2026-10-21 kararları belgeye girer. Lisans politikası Npgsql'i (PostgreSQL Lisansı) ve npm paketlerini kapsar: MPL-2.0 değiştirilmeden kullanıldıkça, Unlicense, MIT-0 ve ISC serbesttir. Yetki modeli belgesi 26 izin kodunu, karar ekranının izni olan `reports.view`'ü, davranışsal sıkılaştırmanın gerçek ölçütünü (onaylı talep sayısı), kişisel ikram bütçesini (talep başı 50, gün başı 150 TL, UTC günü) ve kararlaştırılan başlangıç limitlerini anlatır. DESIGN.md PIN kilidini koddaki gibi anlatır. KVKK saklama belgesi personelin e-posta, telefon ve PIN özetini ve 10 yıllık erişim kaydı ile kişisel bütçe kaydı süresini içerir. Yedekten dönüş belgesi, geri gelen iptal edilmiş oturumları ve pasif kullanıcıları kapatan Identity adımını içerir. Test yardımcısının sınıf özeti güncel göç listesini söyler.

## Owned surface

- `plan/v1/remediation/V1-RMD-672-identity-documents-and-licences.md`
- `evidence/V1-RMD-672/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/engineering/third-party-license-policy.md - yalnız Npgsql ve npm lisansları
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md - yalnız izin kodları, karar izni, ölçüt, kişisel bütçe, başlangıç limitleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): DESIGN.md - yalnız §4 PIN kilidi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md - yalnız personel alanları ve saklama süreleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/recovery/backup-restore-runbook.md - yalnız Identity adımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs - yalnız sınıf özeti
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-671-offline-reconciliation-chain.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B034 ve Npgsql kararı, B033 belge ve süre kararı, B036'nın depodaki belgeleri, B027 yedekten dönüş adımı.

## Out of scope

- Başlangıç limitlerinin veritabanına tohumlanması, hibe tabloları ve KVKK anonimleştirmenin PIN özetini silmesi (V1-RMD-673). ApprovedHostOrchestrationEdges listesine Roles ağacının eklenmesi, kullanılmayan test paketi başvurusunun kaldırılması (kilit dosyası değişir) ve depo dışındaki beceri dosyası. Lisans tarama aracı (ayrı iş; elle doğrulanamadı olarak belgelenir).

## Dependencies

- V1-RMD-671

## Acceptance evidence

1. Plan-audit ve tutarlılık denetimi sıfır hata, sıfır uyarı.
2. `evidence/V1-RMD-672/` altında belge farkı özeti.

## Handoff

- V1-RMD-598
