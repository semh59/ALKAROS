# V1-RMD-174 - "Eklendi" animasyonu artık gerçek düğüme uygulanıyor

- Task ID: V1-RMD-174
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki son
Medium bulguyu kapatır: "eklendi" animasyonu yok edilmiş düğüme
uygulanıyor ve klavye odağı kayboluyor". Frontend bölümünün 17 Medium
bulgusunun tamamı artık kapalı.

Bir ürüne (seçeneksiz) dokunulunca: `addToDraft(product, 1, [], '')`
çağrılıyor, bu da `afterDraftChange()`'i tetikliyor, bu da
`renderProducts()`'ı çağırıyor — ürün listesinin TAMAMI `innerHTML` ile
yeniden yazılıyor, çünkü her satırın kendi `draftQuantityOf(product.id)`'i
değişti. `button` (tıklama olayından `event.target.closest(...)` ile
yakalanan referans), `addToDraft` döndüğünde artık belgede olmayan, ESKİ
bir düğüm. `.just-added` sınıfını ona eklemek görsel olarak hiçbir şey
yapmıyordu; ve bu düğüm klavye odağını tutuyorsa (Bluetooth klavye veya
Tab ile gezinme), o odak eski düğüm atılır atılmaz sessizce `<body>`'ye
düşüyordu.

Düzeltme: `addToDraft()`'tan sonra, gerçek/güncel düğüm `data-product` özniteliğiyle
(ürünün kimliği hâlâ onu tanımlayan tek şey) yeniden bulunuyor —
animasyon sınıfı ve, odak eski düğümdeydiyse (veya zaten kaybolmuşsa),
odağın kendisi bu YENİ düğüme uygulanıyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-174-added-animation-on-live-node.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — ürün listesi tıklama işleyicisi.

## Out of scope

`/comp` + `/transfer-server` istemcisi ve Frontend'in Low bulguları —
ayrı görev/görevler.

## Dependencies

- V1-RMD-173

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Bu görev için ayrı bir otomatik test eklenmedi (repoda bu dosya için JS
  test altyapısı yok, DOM yeniden oluşturma + odak kaybı senaryosunu
  kara-kutu testte doğrulamak gerçek bir tarayıcı gerektirir) — kod gözden
  geçirildi: `renderProducts()`'ın gerçekten `data-product="${escapeHtml(product.id)}"`
  ile aynı öznitelik adını/değerini ürettiği, `CSS.escape()`'in bu
  değerleri güvenli şekilde bir öznitelik seçicisine gömdüğü doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
