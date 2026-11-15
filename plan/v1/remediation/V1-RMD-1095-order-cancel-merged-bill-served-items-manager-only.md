# V1-RMD-1095 - Kasa kuyruğundaki sipariş iptali birleştirilmiş hesapta servis edilmiş kalemi yalnız yetkili yöneticiye bırakır

- Task ID: V1-RMD-1095
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-15 (V1-RMD-1093 ile aynı kural): servis edilmiş ürünlü hesabı yalnız payments.refund.approve yetkisi olan yönetici iptal eder. Sipariş iptali (POST orders/{id}/cancel, yetki bills.cancel) yalnız iptal edilen siparişin kendi kalemlerine bakar; birleştirilmiş hesapta başka siparişin servis edilmiş kalemi varsa o hesabı da iptal eder ve kasiyer bu kuralı atlatır. Düzeltme: sipariş iptali, iptal edilecek açık hesapların (birleştirilmiş hesabın diğer siparişleri dahil) servis edilmiş etkin kalemini denetler; çağıranda payments.refund.approve yoksa 409 ORDER_HAS_SERVED_ITEMS döner ve hiçbir şey değişmez. Siparişin kendi servis edilmiş kalemi bugünkü gibi herkese kapalıdır; hesap iptali ve kayıt temizliği davranışı değişmez.

## Owned surface

- `evidence/V1-RMD-1095/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderCancellation/OrderCancellationStore.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - tests/Host/Experience/Billing/MergedBillOrderLinkTests.cs
  - tests/Host/Experience/Billing/OrderCancellationHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Hesap iptali (BillCancellation) ve kayıt temizliği (OrderCleanupStore) davranışı; masaya geri gönderme (V1-RMD-1093); kasa kuyruğu ekranı

## Dependencies

- V1-RMD-1094

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
