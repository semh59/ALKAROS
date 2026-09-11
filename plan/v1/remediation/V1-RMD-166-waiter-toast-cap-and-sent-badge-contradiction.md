# V1-RMD-166 - Garson ekranı: sınırsız toast ve çelişkili "Gönderildi" rozeti

- Task ID: V1-RMD-166
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümünden iki
Medium bulguyu kapatır:

1. **Toast sayısı sınırsız, 9 kalemlik masada ekranı kaplıyor.** `toast()`
   her çağrıda `el.toasts`'a yeni bir düğüm ekliyordu, hiçbir üst sınır
   yoktu. Yeni `MAX_VISIBLE_TOASTS = 4`: bu sınıra ulaşıldığında en eski
   **sıradan** (geri-al düğmesi olmayan) toast hemen kaldırılıyor. Bir
   "geri al" toast'ı asla erken kapatılmıyor — kesmek, bir kaldırmayı geri
   alma şansını sessizce elden alır.
2. **Draft kalem "GÖNDERİLDİ" başlığı altında "Gönderilmedi" rozetiyle
   görünüyor.** `renderBill()`, `activeItems()`'ın döndürdüğü HER
   iptal-edilmemiş kalemi (iptal/zayi hariç her şey) tek bir "Gönderildi"
   grubunda gösteriyordu — bunun içinde `kitchenState: NotSent` olan bir
   kalem de olabiliyordu (sunucu kaydetti ama mutfağa hiç gitmedi;
   V1-RMD-164'ün düzeltme yolunun tam ürettiği durum). Bölüm başlığı
   "Gönderildi" derken kalemin kendi rozeti "Gönderilmedi" diyordu — doğrudan
   çelişki. Artık iki ayrı grup var: "Gönderilmeyi bekliyor" (kitchenState
   NotSent olanlar) ve "Gönderildi" (gerçekten mutfağa gitmiş olanlar).

## Owned surface

- `plan/v1/remediation/V1-RMD-166-waiter-toast-cap-and-sent-badge-contradiction.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — toast() sınırı, renderBill()'in grup ayrımı.

## Out of scope

Frontend bölümünün kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-165

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- Repoda bu dosya için otomatik bir JS test paketi yok (önceki
  V1-RMD-157/160/163 istemci değişiklikleriyle aynı durum) — kod gözden
  geçirildi, `toast()`'ın çağrı yerleri ve `activeItems()`/`kitchenState`
  alan adları gerçek `OrderItemDto` sözleşmesiyle (`ItemId`/`KitchenState`
  JSON'da camelCase `itemId`/`kitchenState` olarak serileşir) eşleştiği
  doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
