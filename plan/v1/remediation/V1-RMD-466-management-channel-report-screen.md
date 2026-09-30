# V1-RMD-466 - Yönetim: online kanal raporu ekranı

- Task ID: V1-RMD-466
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

`GET /api/v1/management/reports/channels` (QR ve online kanal raporu: gün ve kanal bazında alınan, kabul edilen, reddedilen,
iptal edilen sipariş sayısı ve tutarı, platformun oluşturamadığımız siparişleri, uzlaştırma farkı) bugün hiçbir ekrandan
çağrılmıyor. Semih 2026-09-30'da online satışların ciroya yazılmayıp ayrı raporda görünmesine karar verdi. Bu görev
Yönetim alanına "Kanal raporu" bölümü ekler: tarih aralığı (en çok 31 gün), kanal süzgeci (Hepsi, QR, Online), gün satırları
ve toplam; brüt ve net tutar; rapor dengesiz ise (`check.isBalanced` false) Türkçe uyarı. Yalnız okur.

## Owned surface

- `plan/v1/remediation/V1-RMD-466-management-channel-report-screen.md`
- `src/Clients/PosTerminal/src/features/management-channel-report/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts - yalnız yeni bölümün kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni bölümün Türkçe metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ManagementArea.test.tsx - yalnız bölüm listesi beklentisi

## In scope

- Bölüm, `reports.view` yetkisi olan oturuma görünür; yükleniyor, boş, hata durumları; sunucu hata gerekçesi Türkçe (ortak
  `createRequester`); İngilizce sözcük (Qr, Online, Accepted) ekrana çıkmaz, sözlükten geçer.
- Testler: tablo ve toplam doğru, kanal süzgeci istek adresine yansır, 31 günü aşan aralık istemeden Türkçe uyarı, dengesiz
  rapor uyarısı, erişilebilirlik (axe).

## Out of scope

- Komisyon ve platform ödeme uzlaştırması; dışa aktarma; sunucu tarafı değişiklik. Gün sonu sipariş sayısı `V1-RMD-465`tedir.

## Dependencies

- None

## Acceptance evidence

- Vitest, typecheck ve lint exit code 0; gerçek Host denemesi (kabul edilmiş online sipariş kanal raporunda görünür);
  çıktılar `evidence/V1-RMD-466/` altındadır.

## Handoff

- None
