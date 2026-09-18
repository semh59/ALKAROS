# V1-RMD-235 - Ekran koruyucu uç noktası: savunma derinliği iyileştirmeleri

- Task ID: V1-RMD-235
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız bir denetim ajanı (2026-09-17, Kasa modülü kapsamlı denetimi),
ekran koruyucu uç noktalarında (`DualScreenApplication.Screensaver.cs`)
kritik olmayan ama düzeltilmesi gereken üç savunma-derinliği notu buldu:

1. `.DisableAntiforgery()` çağrısı şu an tamamen işlevsiz — kod tabanında
   hiçbir yerde `AddAntiforgery`/`UseAntiforgery` kurulu değil, yani bu
   minimal API zaten antiforgery zorunluluğuna tabi değildi. Yorum yanıltıcı
   ("multipart formu engelliyordu çünkü antiforgery aktifti" ima ediyor).
2. Dosya boyutu kontrolleri (5 MB görsel, 20 MB video) doğru sırada
   yapılıyor ama repo genelinde açık bir Kestrel `MaxRequestBodySize`/
   `RequestFormLimits` YOK — Kestrel'in ~28.6 MB'lık örtük varsayılanına
   güveniliyor, bu 20 MB video sınırına dar bir marj bırakıyor.
3. Dosya tipi doğrulaması yalnız client'ın beyan ettiği `Content-Type`
   header'ına dayanıyor; gerçek dosya baytı (magic number: PNG `\x89PNG`,
   MP4 `ftyp` box vb.) hiç kontrol edilmiyor.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Screensaver.cs
  (V1-CDP-001/V1-CDP-004 ailesinde kalır) — yalnız yorum netliği, açık
  body-size limiti ve magic-number doğrulaması eklenir; mevcut iş mantığı
  (yükleme/kaldırma/okuma akışı) değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/
  DualScreen/DualScreenScreensaverTests.cs altına yeni testler (V1-CDP-004
  sahipliğinde kalır).
- `evidence/V1-RMD-235/**`

## In scope

1. `.DisableAntiforgery()`'nin gerçek gerekçesini (multipart form-data +
   minimal API varsayılan davranışı) doğru yansıtacak şekilde yorum
   güncellemek, ya da gerçekten antiforgery eklenmesi gerekip
   gerekmediğine karar vermek (öneri: yorum düzeltmesi yeterli, CSRF
   savunması zaten `SameSite=Strict` çerezlere dayanıyor).
2. Ekran koruyucu route'ları için açık bir `MaxRequestBodySize` limiti
   (video sınırının biraz üzerinde, ör. 25 MB) eklemek.
3. Yüklenen dosyanın ilk birkaç baytını (magic number) allowlist'teki
   MIME tipiyle eşleştiren bir doğrulama eklemek — `Content-Type`
   header'ı doğru olsa bile gerçek bayt imzası yanlışsa reddetmek.

## Out of scope

- Genel antiforgery altyapısını tüm ALKAROS'a eklemek — bu görevin
  kapsamı yalnız ekran koruyucu uç noktası.
- Rate-limit partition anahtarının screensaver route'unda `terminalId`
  içermemesi — kozmetik, ayrı bir görev gerektirmez, bu görevde
  düzeltilebilir ama zorunlu değil.

## Dependencies

- V1-CDP-004

## Acceptance evidence

- Yeni test: yanlış magic number taşıyan ama doğru `Content-Type` header'ı
  beyan eden bir dosya yüklemesi reddedilir.
- Yeni test: `MaxRequestBodySize` limiti üstü bir istek erken reddedilir.
- Mevcut `DualScreenScreensaverTests` (tamamı, gerçek Postgres'e karşı) →
  regresyonsuz geçer.
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
