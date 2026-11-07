# V1-RMD-892 - Bekleyen ve reddedilen ikram/indirim istekleri denetim olayı yazar (yoklamada tekilleştirilmiş)

- Task ID: V1-RMD-892
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Uygulanan ikram/indirim `bill.comp.applied` / `bill.discount.applied` denetim olayı yazar; ama yönetici onayına düşen (202 Pending) ve politika tarafından reddedilen (403 GRANT_DENIED) istekler denetim izinde iz bırakmaz. Hesap uç noktası bu iki sonuç için `bill.comp.pending`, `bill.discount.pending`, `bill.comp.denied`, `bill.discount.denied` olaylarını yazar (hesap, isteği yapan, gerekçe, tutar, hibe kimliği). Kasiyer ekranı bekleyen isteği aynı anahtarla tekrar göndererek yokladığı için aynı hibenin olayı tekrar tekrar yazılmamalıdır: olay kimliği hibe kimliği ve olay adından deterministik türetilir; denetim tablosunun birincil anahtarı ikinci yazımı reddeder (23505) ve bu yazım yutulur. Böylece tekilleştirme eşzamanlı yoklamada ve yazım yarıda kalıp tekrarlandığında da doğrudur.

## Owned surface

- `evidence/V1-RMD-892/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/BillAdjustmentGrantAuditHttpTests.cs — yeni test
  - src/Host/Experience/Billing/BillingSplitApplication.cs — Pending ve Refused sonuçlarında denetim olayı
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: bekleyen ikram ve indirim için olay yazılır; aynı anahtarla beş yoklama sonrası tek olay vardır; reddedilen istek için olay yazılır; onay sonrası uygulanan istek `applied` olayını yazar ve `pending` olayı tek kalır
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Yöneticinin bekleyen hibeyi onaylaması/reddetmesi (`AuthorizationDecisionStore`) kararının denetimi: ayrı iş, RESULT.md'de raporlanır

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
