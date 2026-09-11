# V1-RMD-171 - Hatalı siparişler listesi artık ulaşılabilir ve temizlenebilir

- Task ID: V1-RMD-171
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden bir
Medium bulguyu kapatır: "hatalı siparişler listesi ulaşılamaz (yalnız sayı
görünüyor, ne masa ne içerik, temizlenemiyor)".

Şerit üzerindeki `N hatalı` metni düz bir `<span>` idi — tıklanamaz, hiçbir
yere açılmıyordu. Hangi masa, ne sipariş edilmişti, neden reddedildi —
hiçbiri görünmüyordu, ve kalıcı olarak reddedilmiş (4xx) bir kayıt bir kez
`state.failedOrders`'a düşünce oradan çıkmanın tek yolu tarayıcının
localStorage'ını elle temizlemekti.

Düzeltme:

- `#ribbonQueue`, `<span>`'den gerçek bir `<button>`'a çevrildi; yalnızca
  bekleyen veya hatalı bir şey varken görünür.
- Yeni `openFailedOrdersSheet()`: mevcut `openOptions()` sayfası
  üzerinden, hem hâlâ tekrar denenen (çevrimdışı kuyruk, bilgi amaçlı) hem
  de kalıcı olarak reddedilmiş (hatalı) turları listeler — her satırda
  masa numarası, kalem adları/adedi, ve hatalı olanlar için sunucunun
  reddetme mesajı.
- Her hatalı satırın kendi "Sil" düğmesi var (`dismissFailedOrder`); sayfa
  başlığında "Hatalı olanların tümünü temizle" (`clearAllFailedOrders`).
  Bekleyen (henüz kalıcı olarak başarısız olmamış) kayıtlar bilgi amaçlı
  gösteriliyor, silinemiyor — hâlâ tekrar denenecekleri için silmek
  siparişi gerçekten kaybetmek olurdu.

## Owned surface

- `plan/v1/remediation/V1-RMD-171-reachable-failed-orders-list.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/index.html (V1-WTR-010 sahipliğinde) —
    `#ribbonQueue` span'den button'a.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — yeni sayfa, dismiss/clear-all, `onOptionsConfirm`/`onOptionsBodyClick`'e
    yeni mode.

## Out of scope

Frontend'in kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-170

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu dosyalar için
  JS test altyapısı yok) — kod gözden geçirildi: `queuedOrderRow`'un
  `payload.items`'daki alan adlarının (`productName`/`name`,
  `tableNumber`) gerçek `draftToPayload()` çıktısıyla eşleştiği, kullanılan
  SVG ikonlarının (`ico-alert`, `ico-bell`) `index.html`'in kendi
  sprite'ında gerçekten var olduğu, ve iç içe `<button>` oluşmadığı
  (satır `<div class="opt">`, "Sil" içindeki tek `<button>`) elle
  doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
