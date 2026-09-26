# V12-OUI-003 - Online platform kimlik bilgisi ekranı

- Task ID: V12-OUI-003
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Goal

Yemeksepeti API bilgileri bugün yalnız ortam değişkeniyle veriliyor; yeni platformlar da aynı ihtiyacı taşıyacak.
Yetkili yönetici her platformun API bilgilerini şifreli saklanan bir ekrandan girer (QNB ve Token ekranlarındaki
desen); platform istemcileri bilgiyi bu depodan okur. V12-OUI-002'den bölme testi gereği ayrıldı.

## Owned surface

- `src/Modules/OnlineOrdering/Credentials/**`
- `src/Clients/PosTerminal/src/features/online-platform-credentials/**`
- `src/Host/Experience/OnlineOrdering/OnlinePlatformCredentialEndpoints.cs`
- `database/migrations/V12/V12-OUI-003/**`
- `evidence/V12-OUI-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/ (V12-ONL-003) — istemcinin bilgiyi depodan okuması.
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ (V12-ONL-001) — webhook sırrının depodan okunması.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - src/Host/DualScreen/DualScreenApplication.cs — uç nokta kaydı.
  - src/Clients/PosTerminal/src/routes/ — ekran bağlantısı.
  - src/Clients/PosTerminal/src/shell/ — menü bağlantısı.
  - database/MigrationComposition/order.json — migration konumu.
  - src/Host/Composition/Migrations/MigrationManifest.cs — migration konumu.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration konumu.
  - tests/Host/Experience/OnlineOrdering/ — testler ve fikstür bağlantıları.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ — testler.

## In scope

1. Platform başına kimlik bilgisi deposu: AES-256-GCM zarf, maskeli okuma, her değişiklik için denetim kaydı;
   okuma ve yazma yalnız yönetici izniyle.
2. PosTerminal ekranı: platform seçimi, alanların girilmesi, maskeli gösterim; tüm metin Türkçe.
3. Yemeksepeti istemcisi ve webhook doğrulaması bilgiyi önce depodan, yoksa ortam değişkeninden okur.

## Out of scope

- Platforma gerçek bağlantı testi (platform adaptör görevlerinde).

## Dependencies

- V12-OUI-002

## Acceptance evidence

- İlgili test projeleri ve PosTerminal testleri yeşil; mutasyon kontrolü `evidence/V12-OUI-003/` altında.
- `task_scope_tool.py --task-id V12-OUI-003 --diff-base <InProgress commit>` exit 0.
- Migration ileri ve geri boş veritabanında denenir.

## Handoff

- None
