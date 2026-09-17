# V1-CDP-003 - Ekran koruyucu görseli için yönetici yükleme ekranı

- Task ID: V1-CDP-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

İşletmenin kendi ekran koruyucu görselini yükleyip önizleyip kaldırabileceği,
yönetici yetkisi gerektiren küçük bir ayar ekranı — V1-CDP-001'in yükleme/
kaldırma uç noktalarını çağırır. Semih'in isteği: "müşteri kendi istediği
resmi ayarlasın" — bu, işletmenin (ALKAROS'un kendi müşterisi) kendi ekranını
markalayabilmesi.

## Owned surface

- `evidence/V1-CDP-003/**`
- PO:2026-09-16 kararıyla src/Clients/PosTerminal/src/routes/CustomerDisplayScreensaverSettings.tsx
  ve src/Clients/PosTerminal/src/routes/customer-display-screensaver-settings.css
  yüzeyi V1-CDP-004'e devredildi; bu historical task closed kalır.
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - src/Clients/PosTerminal/src/App.tsx (V1-RMD-139 sahipliğinde kalır) —
    gerçek standalone-rota kaydı burada (`/settings/relay` ile aynı desen);
    yalnız `/settings/screensaver` için bir `lazy()` girişi ve bir
    `pathname.startsWith` bloğu eklenir, mevcut rotalar değişmez.
  - src/Clients/PosTerminal/src/api.ts (V1-RMD-097 sahipliğinde kalır) —
    `uploadScreensaver`/`removeScreensaver` çağrıları eklenir.

## In scope

- Dosya seçici (yalnız `image/png`, `image/jpeg`, `image/webp`; 5 MB üstü
  istemci tarafında da engellenir — sunucu zaten reddediyor, bu yalnız UX).
- Canlı önizleme (seçilen dosya, henüz yüklenmeden).
- "Yükle" ve "Kaldır" aksiyonları, V1-CDP-001'in uç noktalarına bağlı.
- Yükleme/kaldırma sonrası mevcut görselin (varsa) gösterildiği bir durum.
- Erişim `catalog.manage` yetkisiyle sınırlı (System-health ile aynı desen).

## Out of scope

- Görsel kırpma/düzenleme aracı — kullanıcı kendi hazırladığı görseli yükler.
- Birden fazla görsel/slayt sırası yönetimi.

## Dependencies

- V1-CDP-001

## Acceptance evidence

- `corepack pnpm typecheck` → hatasız.
- `corepack pnpm test` (`vitest run`) → 23 dosya / 175 test, hepsi geçti,
  regresyon yok.
- `corepack pnpm build` → 0 hata; yeni rota kendi ayrı chunk'ında
  (`CustomerDisplayScreensaverSettings-*.js`/`.css`) üretildi — V1-RMD-139'un
  route-splitting deseni bozulmadı.
- Erişim kontrolü kod seviyesinde doğrulandı: ekran `api.session`/`api.login`
  yanıtındaki `capabilities` dizisinde `catalog.manage` yoksa `"forbidden"`
  durumuna düşer (System-health/Catalog rotalarıyla aynı desen); sunucu
  tarafında zaten `CatalogManagerEndpointFilter` PUT/DELETE'i bağımsız olarak
  reddediyor (V1-CDP-001'in kendi testleri).
- **Bulunan gerçek bir kapsam sınırı (görev kapsamına eklenmedi, Semih'e
  bildiriliyor):** V1-CDP-001'in `GET .../screensaver`'ı kasıtlı olarak
  yalnız display principal'a açık; yönetici oturumunun görseli GERİ OKUYUP
  gösterebileceği bir uç nokta yok. Bu yüzden bu ekrandaki "mevcut görsel"
  önizlemesi yalnız BU oturumda yapılan son yükleme/kaldırmayı yansıtır,
  sayfa yenilendiğinde veya başka bir oturumda sunucudaki gerçek durumu
  otomatik göstermez — ekranın kendisi bunu Türkçe bir notla açıkça belirtir
  ("...yalnızca bu oturumda yaptığınız son işlemi gösterir"). Gerçek zamanlı
  "şu an ayarlı mı" durumunu göstermek istenirse, `DualScreenApplication.
  Screensaver.cs`'e yönetici-gated ayrı bir `GET` eklemek gerekir — bu,
  V1-CDP-001'in Owned surface'ını yeniden açacağı için ayrı bir görev
  (fast-follow) olarak açılmalı, bu görevin kapsamına dahil edilmedi.
- Semih'in elle deneyebileceği senaryo: yönetici olarak giriş yap
  (`/settings/screensaver`), bir görsel seç (önizlemenin göründüğünü
  doğrula), "Yükle"ye bas, müşteri ekranının (V1-CDP-002) bunu Idle'da
  gösterdiğini doğrula; "Kaldır"a bas, müşteri ekranının varsayılan
  markalı karta döndüğünü doğrula. Ayrıca `catalog.manage` yetkisi olmayan
  bir hesapla giriş denemesi yapıp erişimin reddedildiğini doğrula.
