# V1-RMD-313 - QR reddinde mutfak iptali siparişle aynı transaction'da olsun

- Task ID: V1-RMD-313
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetimi şunu buldu: `PendingOrderConfirmationStore.RejectAsync` mutfak fişi kalemlerini
kendi bağlantısında iptal edip commit ediyor, reddi ise ardından ayrı bir transaction'da yazıyor. Eşzamanlı bir
onay satır sürümü yarışını kazanırsa sipariş `Accepted` olur, ama mutfak kalemleri iptal edilmiş kalır. Mutfak
onaylanmış siparişi pişirmez. Bu kusur V12-QRO-003'ten önce de vardı ve QRO-003 onu yerinde bıraktı. Semih: "en
küçük hata bile kritik".

## Owned surface

- `plan/v1/remediation/V1-RMD-313-qr-reject-kitchen-cancel-atomic.md`
- `evidence/V1-RMD-313/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Kitchen/TicketLifecycle/IKitchenTicketRepository.cs ve
    src/Modules/Kitchen/TicketLifecycle/PostgresKitchenTicketRepository.cs — sipariş fişlerini okuma ve kaydetme
    için transaction overload'ları.
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs (V12-QRO-003) — mutfak
    iptali, ret ve masa bırakma tek transaction'da.
  - tests/Host/Experience/Orders/Confirmation/ — testler.

## In scope

1. `IKitchenTicketRepository.GetByOrderIdAsync(orderId, connection, transaction)` (fiş satırları `FOR UPDATE`) ve
   `SaveAsync(ticket, expectedRowVersion, connection, transaction)`.
2. `RejectAsync` aynı transaction'da şunları yapar:
   - siparişi yeniden okur ve sürümünü doğrular;
   - mutfak kalemlerini iptal eder;
   - reddi yazar;
   - masayı bırakır.
   Yarışı onay kazanırsa mutfak kalemleri dokunulmadan kalır.
3. Test: kaybeden bir ret mutfak kalemlerini iptal etmez.

## Out of scope

- V12-ONL-003 yerel iptalinin mutfak kaydı (V12-RMD-005 bu görevin overload'larını kullanır).

## Dependencies

- None

## Deliverables

- Overload'lar, atomik ret, test.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V1-RMD-313/` altında.
- `task_scope_tool.py --task-id V1-RMD-313 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
