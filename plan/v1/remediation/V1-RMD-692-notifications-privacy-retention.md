# V1-RMD-692 - KVKK ve saklama: bildirim tabloları, anonimleştirme, PIN özeti, vergi kimliği izi

- Task ID: V1-RMD-692
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B006, B007, B014, B043, B106, B109, B113, B115, B138 kapanır. notifications ve customer_display tabloları KVKK envanterine girer, süreli temizlenir ve anonimleştirmede ele alınır; anonimleştirilen personelin PIN özeti silinir; müşteri vergi kimliği değişikliği ve çözümlemesi denetim kaydı bırakır; temizlik parçalı çalışır; kullanıcı başına tek bekleyen devir notu şemada zorlanır. Süreler Semih sorularına (S1, S2, S3, S5) göre ayarlanabilir; yanıt gelene kadar ayar değeri önerilen varsayılandır.

## Owned surface

- `plan/v1/remediation/V1-RMD-692-notifications-privacy-retention.md`
- `evidence/V1-RMD-692/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız temizlik ve anonimleştirme planı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/CustomerAccounts/** - yalnız denetim kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/** - yalnız saklama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/WebPush/** - yalnız saklama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Privacy/** - yalnız plan ve envanter
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/V1/V1-RMD-692/** - yeni göç dosyaları
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/** - yalnız KVKK envanteri ve runbook
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/data-housekeeping.md - yalnız yeni tablolar
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız PhaseBMax
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/migration-rehearsal-profile.md - yalnız yeni göç satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-691-push-notification-hardening.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Envanter ve runbook, temizlik kapsamı ve parçalama, anonimleştirme planı, PIN özeti, denetim olayları, devir notu tekilliği, silme yolu, testler.

## Out of scope

- Yedekten geri yüklenen verinin silme taleplerine uygunluğu: Semih sorusu S7 yanıtlanınca ayrı görev olur.

## Dependencies

- V1-RMD-691

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-692/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-693
