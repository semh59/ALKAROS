# V1-CDP-002 - Müşteri ekranı boşta iken özel görseli göster

- Task ID: V1-CDP-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

`CustomerDisplay.tsx`'in Idle durumunda (`presentation === "idle"`), V1-CDP-001'in
`GET .../screensaver` uç noktasından işletmenin yüklediği görsel varsa onu
tam ekran gösterir; yoksa (404) bugünkü sabit markalı "Siparişiniz için
hazırız" kartı DEĞİŞMEDEN kalır — regresyonsuz varsayılan.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx
  (V1-RMD-097 ailesinin sahipliğinde kalır) — yalnız Idle durumunun render
  bloğuna görsel getirme/gösterme eklenir; pairing, snapshot, diğer durumlar
  (Active/Paying/Completed/Unavailable) değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/styles.css
  (V1-CUI-009 sahipliğinde kalır) — gerekiyorsa yalnız .idle-screen altına
  kapsamlı (scoped) tek bir arka plan/object-fit kuralı, foundations.md'yi
  ihlal etmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts
  (V1-RMD-097 ailesinin sahipliğinde kalır) — bir fetchIdleScreensaver
  çağrısı eklenir, mevcut fonksiyonlar değişmez.
- `evidence/V1-CDP-002/**`

## In scope

- Idle durumuna girildiğinde (veya pairing onaylandığında, bir kere) görsel
  URL'i/varlığı sorgulanır; `404` sessizce varsayılana düşer, hata göstermez.
- Görsel varsa tam ekran gösterilir (`object-fit: cover`), "Bu ekran yalnız
  sipariş içeriğini gösterir" gizlilik notu ve ALKAROS markası küçük bir
  köşe rozeti olarak KORUNUR (görsel bilgiyi tamamen gizlemez).
- Bağlantı kaybı/yeniden bağlanma göstergeleri (`ConnectionBanner`) görselin
  üzerinde okunur kalır (mevcut z-index/kontrast düzeni bozulmaz).

## Out of scope

- Görselin canlı (SignalR) güncellenmesi — yalnız Idle'a her girişte/sayfa
  yenilendiğinde tekrar sorgulanır.
- Slayt gösterisi/birden fazla görsel — V1-CDP-001 zaten tek görsel.

## Dependencies

- V1-CDP-001

## Acceptance evidence

- `corepack pnpm typecheck` (`tsc --noEmit`) → hatasız.
- `corepack pnpm test` (`vitest run`, `vanilla-clients-a11y.test.ts` dahil
  PosTerminal test suite'inin tamamı) → 23 dosya / 175 test, hepsi geçti,
  regresyon yok.
- `corepack pnpm build` (`tsc --noEmit && vite build`) → 0 hata,
  `CustomerDisplay-*.js` bundle'ı başarıyla üretildi.
- **Dürüstlük notu:** bu oturumda gerçek bir tarayıcıda görsel doğrulama
  YAPILMADI — bu ortamda ekran görüntüsü alabilen bir tarayıcı aracı yok.
  Kanıt yalnız typecheck/test/build seviyesinde; `screensaverUrl` state'inin
  Idle'a her yeni girişte (`idleShown`'un false→true geçişinde) sıfırdan
  sorgulandığı, `null` (404) durumunda mevcut markalı karta hiçbir JSX/CSS
  değişikliği olmadan düştüğü ve `object-fit: cover` ile tam ekran
  render edildiği kodun kendisinden ve testlerden izlenebilir, ama
  Semih'in kendi tarayıcısında gerçek bir yükleme/kaldırma döngüsüyle
  bizzat doğrulaması gerekiyor (aşağıdaki senaryo).
- Semih'in elle deneyebileceği senaryo: V1-CDP-001'de bir görsel yükle,
  müşteri ekranını Idle durumuna getir (aktif siparişi kapat/tamamla),
  görselin tam ekran göründüğünü, ALKAROS rozetinin ve gizlilik notunun
  görsel üzerinde okunur kaldığını doğrula; sonra görseli kaldır, ekranın
  bugünküyle birebir aynı markalı karta döndüğünü doğrula.
