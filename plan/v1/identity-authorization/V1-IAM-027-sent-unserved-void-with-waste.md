# V1-IAM-027 - Sent-but-unserved item void with waste

- Task ID: V1-IAM-027
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`docs/domain/void-complimentary-discount-policy.md`'nin `## Amendment`
bölümünün (Semih, 2026-09-04) kodlanması: `kitchen.live_sync_enabled` açıkken
(`V1-SET-002`), mutfağa gönderilmiş ama henüz servis edilmemiş
(`KitchenState ∈ {Sent, Preparing, Ready}`, `V1-KIT-005` gerçek değeri
yazdığı için artık bilinebilir) bir kalem, `bills.void` grant'iyle
(politika/delegasyon/yönetici — model §4) iptal edilebilir. İptal, eşleşen
mutfak bilet kalemini de düşürür ve açık bir Bill üzerindeyse `BillItem`'ı
kaldırıp `BillLineType.Waste` satırı yazar (bu enum değeri `III.7.2`
kataloğunda vardı, hiç üretilmemişti).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-027-sent-unserved-void-with-waste.md`
- `evidence/V1-IAM-027/**`
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- `OrderItem.Cancel()`'ın `KitchenState is not KitchenState.NotSent` reddini
  gevşetmek: `Sent/Preparing/Ready`'den de iptale izin verir (yalnız
  `Served`'dan değil — o comp'un alanı).
- İptal, eşleşen `KitchenTicketItem`'ı da `Cancelled`'a taşır
  (`IKitchenTicketRepository.GetByOrderIdAsync` ile bulunup).
- Bir `Bill` zaten varsa (`IsOrderItemBilledAsync`), `BillItem`'ı kaldırır
  (`Bill.RemoveItem`, bugüne kadar hiç çağrılmayan mevcut metot) ve
  `BillLineType.Waste` satırı ekler.
- `bills.void` iznine sahip değilse `IAuthorizationGrantService.RequestAsync`;
  own-check kuralı waiter'a uygulanır (model §3, kendi çeki değilse otomatik
  red).
- `kitchen.live_sync_enabled` kapalıyken bu yol hiç ulaşılamaz
  (`OrderItem.KitchenState` hep `NotSent` kalır) — mevcut duvar davranışı
  değişmeden sürer.

## Out of scope

- Fiscal sonrası iptal (refund yolu, `V0-DOM-003`).
- Pre-send void (`V1-ORD-005`) ve comp (`V1-BIL-005`) — bu görev yalnız
  gönderildi-ama-servis-edilmedi durumunu kapsar.

## Dependencies

- V1-SET-002
- V1-KIT-005
- V1-ORD-005
- V1-BIL-005

## Blocker

- Dokunulacak dosyalar (`OrderItem.cs`, `Bill.cs`, ilgili endpoint) için
  custody devri, önceki dört görev tamamlandıktan ve gerçek dosya listesi
  netleştikten sonra eklenir. Ancak bu eklenip `validate` temiz kaldığında
  görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-GOV-074
