# V1-RMD-519 - Ürün bazlı kısmi iade (PO kararları ve zincir)

- Task ID: V1-RMD-519
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: planning
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Bugün iade yalnız ödemenin tamamı içindir. Ürün bazlı kısmi iade için PO kararları kayda geçer; uygulama görevleri bu kararlara göre ayrı açılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-519-item-based-partial-refund-decisions.md`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- PO kararları (2026-10-13): iade ürün bazlıdır; her iadede kasiyer ürün için "stoğa geri dön" seçimini yapar; birden fazla ödeme yöntemi olan hesapta kasiyer iadenin hangi ödemeden düşeceğini seçer; limit altında kasiyer iadeyi kendisi tamamlar, limit üstünde başka bir yetkili onaylar; varsayılan limit 100 TL'dir ve müdür tarafından değiştirilebilir.
- Bilinen çelişkiler: veritabanında iade isteğini açanın aynı kişi tarafından karara bağlanmasını yasaklayan kısıt vardır (`ck_refund_requests_decider_not_requester`); nakit iadede "kasiyer ister, müdür onaylar" kararı vardır (2026-10-13). Limit altında kendi kendine tamamlama bu iki kuralı bilerek gevşetir; uygulama görevi bunu açıkça ele alır.
- Doğrulanacaklar: stok geri alma servisinin kalem içinde kısmi miktarı destekleyip desteklemediği; sayısal işletme ayarı için kayıt yeri bulunmadığı (yeni tablo ve yönetim ekranı gerekebilir).

## Out of scope

- Uygulama kodu; bu görev yalnız kararları ve zinciri kaydeder.

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Uygulama görevleri açıldığında bu kayıt güncellenir.

## Handoff

- None
