# V1-CDP-004 - Ekran koruyucuda video oynatma desteği

- Task ID: V1-CDP-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

V1-CDP-001/002/003, ekran koruyucuyu yalnız statik görsele (PNG/JPEG/WEBP,
5 MB) izin verecek şekilde kurmuştu. Semih'in sorusu üzerine netleşen gerçek
istek: işletme, müşteri ekranının boşta (Idle) kalan sürede kısa, sessiz
döngülü bir TANITIM VİDEOSU da oynatabilmeli — yalnız görsel değil. Bu görev
mevcut depolama/uç nokta/oynatma/yükleme zincirinin dördünü de (V1-CDP-001'in
uç noktaları, V1-CDP-002'nin Idle render'ı, V1-CDP-003'ün yükleme ekranı)
video içerik türünü kabul edecek şekilde genişletir — yeni bir tablo veya
şema değişikliği GEREKMİYOR: `customer_display.screensaver_images` tablosunun
`content_type VARCHAR(64)` sütununda zaten hiçbir CHECK kısıtı yok, izin
verilen MIME listesi ve boyut sınırı tamamen uygulama katmanında (C#
sabitleri) tutuluyor.

Semih'in kararı: video için azami boyut **20 MB** (görsel için mevcut 5 MB
sınırı değişmez — türe göre ayrı sınır).

## Owned surface

- `src/Host/DualScreen/DualScreenApplication.Screensaver.cs`
  (V1-CDP-001'den 2026-09-16 tarihinde devralındı — bkz. o görevin dosyası) —
  `AllowedScreensaverContentTypes`'a `video/mp4` eklenir; sabit tek
  `MaxScreensaverBytes` yerine içerik türüne göre (görsel 5 MB / video 20 MB)
  ayrı sınır uygulanır.
- `tests/Host/MigrationComposition/DualScreen/DualScreenScreensaverTests.cs`
  (V1-CDP-001'den devralındı) — video yükleme round-trip, video için 20 MB
  üstü red, görsel sınırının (5 MB) video'yu etkilemediğinin kanıtı, izin
  verilmeyen video türü (ör. `video/quicktime`) reddi.
- `src/Clients/PosTerminal/src/routes/CustomerDisplayScreensaverSettings.tsx`
  (V1-CDP-003'ten devralındı) — dosya seçici `video/mp4`'ü de kabul eder,
  seçilen video için `<video controls>` önizlemesi, istemci tarafı
  boyut/tür kontrolü türe göre ayrı sınırla çalışır.
- `src/Clients/PosTerminal/src/routes/customer-display-screensaver-settings.css`
  (V1-CDP-003'ten devralındı) — video önizleme kutusunun boyutlandırması.
- `evidence/V1-CDP-004/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - src/Clients/PosTerminal/src/api.ts (V1-RMD-097 sahipliğinde kalır) —
    `fetchIdleScreensaver`'ın dönüş tipi `{ url, contentType } | null`
    olacak şekilde genişler (çağıran V1-CDP-002'nin kendi dosyasında
    güncellenir); `uploadScreensaver` değişmez (Content-Type zaten
    `File.type`'tan geliyor, tür ayrımı sunucu ve istemci doğrulamasında).
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx
    (V1-RMD-097 ailesinde kalır) — Idle render bloğu, dönen `contentType`
    `video/` ile başlıyorsa `<video autoPlay muted loop playsInline>`,
    değilse mevcut `<img>` render eder; pairing/snapshot/diğer durumlar
    değişmez.
  - src/Clients/PosTerminal/src/styles.css (V1-CUI-009 sahipliğinde kalır) —
    `.idle-screensaver-image` seçicisi `img, video` ikisini de kapsar
    (aynı `position: fixed; inset: 0; object-fit: cover` kuralı).

## In scope

- Sunucu: `video/mp4` MIME türü kabul edilir; azami boyut görsel için 5 MB,
  video için 20 MB (türe göre ayrı sınır — büyütülmüş tek bir ortak sınır
  DEĞİL).
- Müşteri ekranı (Idle): içerik türü video ise sessiz, otomatik, döngülü
  oynatma (`autoPlay muted loop playsInline` — tarayıcıların sessiz olmayan
  autoplay'i zaten engellediği, sinyalizasyon/dijital tabela ekranlarının
  standart davranışı).
- Yükleme ekranı: video seçildiğinde `<video controls>` ile önizleme;
  istemci tarafı doğrulama türe göre doğru sınırı uygular.
- ALKAROS rozeti ve gizlilik notu video üzerinde de köşe rozeti olarak
  korunur (V1-CDP-002'nin mevcut overlay deseni video için de kullanılır).

## Out of scope

- `video/webm` veya başka codec'ler — yalnız `video/mp4` (H.264), en yaygın
  tarayıcı/donanım uyumluluğu.
- Sesli oynatma — ekran koruyucu her zaman sessizdir (autoplay politikaları
  zaten sesli otomatik oynatmayı engeller, bu icat edilmiş bir kısıtlama
  değil).
- Video kırpma/dönüştürme/sıkıştırma — işletme kendi hazırladığı dosyayı
  olduğu gibi yükler.
- Yönetici ekranındaki "mevcut görsel/video" önizlemesinin sunucudan canlı
  okunması — V1-CDP-003'ün kendi kapsam sınırı (yalnız oturum içi son işlem)
  bu görevde de aynen geçerli, genişletilmiyor.

## Dependencies

- V1-CDP-001
- V1-CDP-002
- V1-CDP-003

## Acceptance evidence

- Gerçek Postgres'e karşı (`ALKAROS_TEST_PG_PORT=55432`)
  `DualScreenScreensaverTests` → 14/14 geçti (10 mevcut + 4 yeni): video
  yükleme+okuma round-trip (`Content-Type: video/mp4` doğru geri dönüyor),
  20 MB üstü video reddi (DB'ye yazılmadığının kanıtıyla), izin verilmeyen
  video türü (`video/quicktime`) reddi, 5 MB'den büyük (6 MB) ama 20 MB'den
  küçük bir `video/mp4` dosyasının kabul edildiğinin — yani görsel sınırının
  (5 MB) video'yu etkilemediğinin, iki sınırın gerçekten ayrı olduğunun —
  kanıtı.
- `corepack pnpm typecheck` → hatasız.
- `corepack pnpm test` (`vitest run`) → 23 dosya / 175 test, regresyonsuz.
- `corepack pnpm build` → 0 hata; `CustomerDisplayScreensaverSettings-*`
  hâlâ kendi ayrı chunk'ında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `git status --short` → yalnız Owned surface + Sınırlı ek yollarında
  değişiklik var; kapsam dışı hiçbir dosyaya dokunulmadı.
- **Dürüstlük notu:** V1-CDP-002/003'te olduğu gibi, bu ortamda ekran
  görüntüsü alabilen bir tarayıcı yok — videonun gerçek bir tarayıcıda
  sessiz/döngülü oynadığı bizzat GÖRÜLMEDİ. Kanıt yalnız
  typecheck/test/build + gerçek Postgres HTTP testleri seviyesinde;
  `autoPlay muted loop playsInline` özniteliklerinin doğru `<video>`
  elemanına uygulandığı kod incelemesiyle doğrulanabilir ama görsel
  doğrulama Semih'in kendisinde.
- Semih'in elle deneyebileceği senaryo: `/settings/screensaver`'dan kısa
  bir MP4 yükle (önizlemenin `<video controls>` ile oynadığını doğrula),
  müşteri ekranını Idle durumuna getir, videonun sessiz ve döngülü
  oynadığını doğrula; sonra bir PNG yükleyip görsele geri dönebildiğini
  doğrula; 20 MB'den büyük bir dosya seçmeyi dene, istemcinin dosyayı
  göndermeden reddettiğini doğrula.
