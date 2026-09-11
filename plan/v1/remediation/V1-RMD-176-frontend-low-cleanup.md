# V1-RMD-176 - Frontend Low bulgularının kalanı

- Task ID: V1-RMD-176
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki Low
bulgular satırında (12 madde) adı geçen kalan kategorileri kapatır
(`#userRole` ayrı görevde, V1-RMD-175, zaten kapatıldı).

1. **Ölü CSS.** `waiter-app.css` içinde `index.html`/`waiter-app.js`'in hiç
   üretmediği ve hiç okumadığı dört seçici bulundu: `.btn-quiet`,
   `.pin-key.is-blank`, `.awaiting`/`.awaiting .spinner` (+ yalnız onun için
   var olan `@keyframes spin`), `.tag-kitchen`/`.tag-kitchen.is-ready`.
   Doğrulama: her sınıf adı `index.html`, `waiter-app.js`, `sw.js` içinde
   `grep` ile arandı, hiçbirinde eşleşme yok — statik string ya da
   şablon-değişmez (`` `is-${x}` `` gibi) üretim yok. Hepsi silindi (ve
   `prefers-reduced-motion` medya sorgusundaki `.awaiting .spinner`
   referansı da).
2. **Katalog vardiya boyunca yenilenmiyor.** `loadCatalog()` yalnız
   `start()`'ta ve (yalnız uygulama çevrimdışı başladıysa) `online`
   olayında çağrılıyordu — masalar/bekleyenler her ilgili eylemden sonra
   yeniden çekilirken katalog bir vardiya boyunca hiç tazelenmiyordu; bir
   yöneticinin ortadaki fiyat/stok değişikliği açık bir uygulamaya hiç
   ulaşmıyordu. `catalogLoadedAt` + `refreshCatalogIfStaleAsync()` eklendi:
   uygulama görünür hâle her döndüğünde (`visibilitychange`, zaten
   `flushQueue()`'nun kullandığı aynı an), 15 dakikadan eskiyse katalog
   sessizce yenileniyor.
3. **`nextCursor` yok sayılıyor.** İncelemede bunun zaten V1-RMD-163 (ve
   V1-RMD-167'nin düzelttiği kısmi-başarı hatasıyla) kapatıldığı görüldü —
   `fetchWholeCatalogAsync()` hâlâ `X-Next-Cursor` başlığını `cursor`
   boşalana kadar takip ediyor. Bu satır muhtemelen denetimin o düzeltmeden
   önceki bir anına ait; kod incelendi, yeni bir değişiklik gerekmedi.
4. **Çift dokunma korumasız onayla/reddet.** Bekleyen misafir siparişleri
   sayfasındaki "Onayla"/"Reddet" düğmelerinin `resolvePending()`'i
   doğrudan çağırdığı, hiçbir yeniden-giriş korumasının olmadığı görüldü —
   hızlı bir çift dokunuş aynı sipariş için iki eşzamanlı istek
   gönderiyordu (sunucunun `row_version` kontrolü ikinciyi reddeder, ama
   garson tek dokunuştan beklenmedik bir hata toast'ı görüyordu). Yeni
   `resolvePendingGuarded()` sarmalayıcısı, çağrı süresince aynı satırdaki
   her iki düğmeyi de devre dışı bırakıyor (kodun başka yerlerinde zaten
   kullanılan "ilgili düğmeyi `disabled` yap" deseniyle aynı).
5. **Menü her açılışta klavyeyi açıyor.** `showScreen('menu')` her
   çağrıldığında (her masa dokunuşunda, hesaptan menüye her dönüşte)
   `productSearch` alanını zorla `focus()`'luyor, klavyeyi açıyordu — garson
   yalnız bir ürüne dokunmak istese bile. Bu zorla odaklama tamamen
   kaldırıldı; arama kutusu hâlâ bir dokunuş uzakta, sadece artık dayatılmış
   değil.
6. **Canlı bölge eksikleri.** İki arka-planda-tetiklenen metin güncellemesi
   `aria-live` taşımıyordu: misafir sipariş bandı (`#pendingBanner`, garson
   ekranın başka bir yerindeyken belirebilir) ve PIN kilidi alt metni
   (`#lockSub`, hatalı PIN sonrası metni değişir ama odak tuşta kalır).
   İkisine de `role="status" aria-live="polite"` eklendi (mevcut
   `#ribbon`/`#toasts` deseniyle aynı).

## Owned surface

- `plan/v1/remediation/V1-RMD-176-frontend-low-cleanup.md` (yeni)
- Sınırlı ek (V1-WTR-010 sahipliğinde):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.css
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js
  - src/Clients/WaiterPwa/wwwroot/index.html

## Out of scope

`/comp` + `/transfer-server` istemcisi — ayrı, önemli ölçüde yeni özellik
gerektiren bir görev (bkz. `garson-audit-findings.md`). `cashier-app.js`'te
aynı sınıftan bir katalog-yenileme boşluğu fark edildi ama bu denetimin
Frontend bölümü Garson ekranına odaklı; kapsam dışı bırakıldı.

## Dependencies

- V1-RMD-175

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- `python3 -c "open('waiter-app.css').read().count('{')/'}'"` ile açılan/
  kapanan küme parantezi sayıları eşit doğrulandı (240/240) — CSS silme
  işlemi bir bloğu yarım bırakmadı.
- Her silinen CSS sınıfı için `grep -rl <sınıf> index.html waiter-app.js
  sw.js` → 0 eşleşme (silmeden önce ve sonra tekrar tarandı, yeni bir ölü
  sınıf yüzeye çıkmadı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)
- Bu görev için ayrı bir otomatik test eklenmedi — repoda bu iki dosya için
  JS/DOM test altyapısı yok (V1-RMD-174'te de aynı gerekçeyle kaydedildi);
  her değişiklik kod incelemesiyle (grep ile üretim/okuma noktalarının
  gerçekten yokluğu, mevcut aynı-desenli düzeltmelerle birebir karşılaştırma)
  doğrulandı.

## Handoff

- None
