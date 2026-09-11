# V1-RMD-173 - Kilit ve sayfalarda gerçek odak tuzağı: `inert`, Escape

- Task ID: V1-RMD-173
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden iki
Medium bulguyu kapatır:

1. **PIN kilidi klavyeyi engellemiyor.** Bluetooth klavyeyle Tab tuşu,
   `el.lockOverlay.hidden = false` yalnızca GÖRSEL bir gizleme olduğu için
   perdenin arkasındaki düğmelere ulaşabiliyordu — güvenlik açığı, yalnızca
   bir erişilebilirlik kusuru değil.
2. **Sayfalarda odak tuzağı, Escape ve `inert` yok.** Aynı kusur genel
   olarak `optionsSheet` (ürün/iptal/devir/kasa/hatalı-siparişler vb.
   paylaşılan sayfa) ve `loginOverlay` için de geçerliydi — hiçbiri
   arkasındaki içeriği gerçekten devre dışı bırakmıyor, hiçbiri Escape'e
   yanıt vermiyordu.

Düzeltme: standart `inert` özelliği (bu uygulamanın tarayıcı tabanında polyfill
gerektirmeyen, platformun kendi "odak tuzağı" çözümü) kullanıldı — elle
bir Tab-döngüsü yeniden icat edilmedi:

- Yeni `trapBackgroundExcept(...activeElements)` / `releaseTrap()`: aktif
  perde dışındaki tüm `document.body` doğrudan çocuklarını (`toasts` hariç
  — bir toast'ın "geri al" düğmesi her zaman erişilebilir kalmalı) `inert`
  yapar/geri alır.
- `lockScreen()`: arka planı tuzaklıyor, ilk PIN tuşuna odaklanıyor.
  Bilinçli olarak Escape'e yanıt vermiyor — bu perdeden çıkmanın tek yolu
  doğru PIN veya sunucunun 409 "PIN tanımlı değil" cevabı olmalı.
- `showLogin()`/`submitLogin` başarı yolu: aynı desen.
- `openOptions()`/`closeOptions()`: aynı desen + Escape ile kapanma +
  kapanınca odağı sayfayı açan öğeye geri veriyor + sayfa hiç açılmamışken
  bile (DOM'da her zaman var, CSS ekran dışına taşıyor) `inert` ile
  baştan tab-durağı olmaktan çıkarılıyor.

**Bilinçli olarak kapsam dışı bırakıldı:** `billSheet` (adisyon sayfası).
Tablet düzeninde bu sabit bir sütun — `openBill`/`closeBill` orada
no-op (CSS medya sorgusu görünümü zaten değiştirmiyor) ve garson menü ile
adisyonu AYNI ANDA kullanabilmeli. JS'den hangi düzenin aktif olduğunu
güvenle ayırt etmeden koşulsuz bir tuzak eklemek tablet deneyimini
bozma riski taşıyordu — denendi, riski fark edilip geri alındı. Ayrı,
düzen-farkında bir görev gerektiriyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-173-focus-trap-inert-and-escape.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `trapBackgroundExcept`/`releaseTrap`, `lockScreen`/`showLogin`/
    `submitLogin`/`submitPin`/`openOptions`/`closeOptions`/`bindEvents`
    güncellendi.

## Out of scope

- `billSheet` için düzen-farkında bir tuzak — ayrı görev.
- Frontend'in kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-172

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu dosya için JS
  test altyapısı yok, `inert`'in gerçek Tab-döngüsü etkisini kara-kutu
  testte doğrulamak da gerçek bir tarayıcı gerektirir) — kod gözden
  geçirildi: `document.body`'nin doğrudan çocukları (`index.html`'den)
  tek tek listelendi (`header`, `ribbon`, `pendingBanner`, `screens`,
  `billBackdrop`, `billSheet`, `optionsBackdrop`, `optionsSheet`,
  `toasts`, `loginOverlay`, `lockOverlay`) ve `trapBackgroundExcept`'in
  bunlardan yalnız aktif olanları hariç tuttuğu, `toasts`'ı her zaman
  atladığı elle doğrulandı; `billSheet`'e uygulanan koşulsuz bir tuzağın
  tablet düzeninde menü+adisyon eşzamanlı kullanımını bozabileceği
  fark edilip o değişiklik bilinçli olarak geri alındı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
