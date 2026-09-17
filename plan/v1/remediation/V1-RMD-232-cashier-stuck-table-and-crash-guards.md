# V1-RMD-232 - Cashier: KASA-1 asılı kalma riski ve localStorage çökme koruması

- Task ID: V1-RMD-232
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız bir denetim ajanı (2026-09-17, Kasa modülü kapsamlı denetimi),
`cashier-app.js`'de iki gerçek bulgu tespit etti:

1. **`dispatchOrderToKitchen` KASA-1'i asılı bırakabilir**: `draft`
   değişkeni `try` bloğu dışında `null` tanımlı; `draftResponse.json()`
   çağrısı (bağlantı taslak sunucuda başarıyla oluşturulduktan SONRA,
   yanıt gövdesi okunurken) patlarsa `draft` hiç atanmaz, `catch` bloğuna
   düşülür, ve `finally`'deki `if (draft && draft.orderId)` koşulu false
   olduğu için KASA-1 pseudo-masası SERBEST BIRAKILMAZ. Bu, V1-RMD-167'nin
   "artık her durumda serbest bırakma denenir" dediği tam senaryonun,
   yalnız farklı bir hata noktasından (submit yerine JSON parse) hâlâ
   mümkün olan hâli. Sonuç: bir sonraki müşterinin siparişi KASA-1'de kalan
   eski taslakla birleşebilir.
2. **`localStorage` JSON.parse try/catch'siz**: modül yüklenirken (en üst
   seviye, `DOMContentLoaded`'dan önce) `JSON.parse(localStorage.getItem(
   'alkaros_cashier_parked') || '[]')` senkron olarak çalışıyor; bozuk bir
   localStorage değeri TÜM `cashier-app.js`'i sessizce çökertip kasiyer
   ekranını tamamen işlevsiz bırakabilir, hiçbir Türkçe hata mesajı da
   gösterilmez.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
  (V1-RMD-083 ailesinde kalır) — yalnız `dispatchOrderToKitchen`'ın
  orderId kurtarma/serbest bırakma mantığı ve modül-yükleme zamanındaki
  `JSON.parse` çağrısı düzeltilir; mevcut akış/diğer fonksiyonlar
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/Cashier/Frontend/
  altına yeni test(ler) — bu klasör V1-CUI-008 sahipliğinde kalır.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/cashier-app.test.js
  (V1-RMD-109 ailesinde kalır) — bu görevin iki bulgusunun gerçek davranışını
  (fetch/localStorage mock'landığı vitest+jsdom ortamında) kanıtlayan yeni
  testler eklenir; mevcut testler değişmez. Bu dosya, Python string-eşleşme
  testlerinin kanıtlayamayacağı async/DOM davranışını doğrulayan gerçek
  test altyapısıdır (görev yazılırken atlanmış).
- `evidence/V1-RMD-232/**`

## In scope

1. `dispatchOrderToKitchen`: `draftResponse.json()` başarısız olsa bile
   (bozuk gövde, bağlantı kopması) taslağın sunucuda GERÇEKTEN oluşup
   oluşmadığını ayırt edip, oluştuysa KASA-1'i serbest bırakabilecek bir
   kurtarma yolu (ör. `orderId`'yi ayrı yakalamak, ya da parse hatası
   durumunda taslağı sorgulayıp temizlemek).
2. Modül yüklenirken çalışan `JSON.parse(localStorage.getItem(...))`
   çağrısını `try/catch` ile sarmalayıp, hata durumunda boş diziye
   düşürmek (ve bozuk veriyi log'lamak, kullanıcıya sessizce devam
   ettirmek).

## Out of scope

- KASA-1 mekanizmasının kendisi (V1-RMD-167'nin kapsamı) — bu görev yalnız
  aynı korumanın kapsamadığı bir edge-case'i kapatıyor.
- `localStorage`'daki diğer anahtarlar — yalnız `alkaros_cashier_parked`
  okuması düzeltiliyor.

## Dependencies

- None

## Acceptance evidence

- Yeni test: `draftResponse.json()` reddedilen/bozuk bir Promise
  döndürdüğünde, KASA-1'in yine de serbest bırakıldığını (ya da en azından
  asılı kalmadığını) kanıtlar.
- Yeni test: bozuk bir `alkaros_cashier_parked` localStorage değeriyle
  modülün çökmeden yüklendiğini kanıtlar.
- `tests/Clients/Cashier/Frontend/` (tamamı) → regresyonsuz geçer.
- `node --check src/Clients/Cashier/wwwroot/cashier-app.js` → geçti.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
