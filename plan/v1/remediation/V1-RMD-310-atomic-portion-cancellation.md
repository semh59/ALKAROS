# V1-RMD-310 - Porsiyon iptal telafisini tek transaction'da atomik yap

- Task ID: V1-RMD-310
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin tek Yüksek stok bulgusunu ve ona bağlı iki Orta bulguyu kapatmak (Semih:
"en küçük hata bile kritik"):

- `PortionCancellationDecisionService` (V11) fire yolunu üç ayrı commit ile yapıyor. Sıra: durum geçişi, ayrılmış
  miktarın düşülmesi, fire hareketi. İkinci ve üçüncü adım arasında `available` yükseliyor. Bu arada kasa son
  porsiyonu satabiliyor, fire hareketi hiç yazılamıyor ve yeniden deneme bunu tamamlamıyor (`ReplayWaste`).
- Serbest bırakma yolu da iki commit. Aralarında kesilirse porsiyon ayrılmış kalıyor ve tekrar deneme
  (`ReplayRelease`) projeksiyonu uygulamıyor.
- `PostgresCrossChannelPortionArbiter.CompensateAsync` (V12-STK-001) çağıranın transaction'ına katılamıyor.
  V12-ONL-003'ün yerel iptali dış transaction'ı geri alsa bile tutmalar serbest kalmış oluyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-310-atomic-portion-cancellation.md`
- `evidence/V1-RMD-310/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Inventory/PortionReservations/CancellationEffects/ — iptal servisi ve mutfak durumu okuması.
  - src/Modules/Inventory/PortionReservations/Lifecycle/ — transaction'lı durum geçişi.
  - src/Modules/Inventory/ReservationBalanceProjection/ — transaction'lı projeksiyon.
  - src/Modules/Inventory/WasteRecording/ — transaction'lı fire kaydı.
  - src/Modules/Inventory/CrossChannelReservation/ — `CompensateAsync` transaction overload'u (V12-STK-001).
  - src/Modules/Inventory/InventoryModule.cs ve src/Host/Experience/Orders/OrderManagementEndpoints.cs — DI.
  - src/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncService.cs (V12-ONL-003) — telafi iptal
    transaction'ına katılır.
  - tests/Modules/Inventory/PortionReservations/ tests/Modules/Inventory/ReservationBalanceProjection/
    tests/Modules/Inventory/WasteRecording/ tests/Modules/Inventory/CrossChannelReservation/ ve
    tests/Host/Experience/OnlineOrdering/ — yeni atomiklik testleri ve gerekirse sahte sınıf uyarlamaları.

## In scope

1. Durum geçişi, projeksiyon, fire kaydı ve mutfak durumu okuması için çağıranın bağlantı/transaction'ını alan
   overload'lar. Repository arayüzlerinde varsayılan uygulama, eski imzaya düşer; sahte sınıflar derlenmeye devam
   eder, Postgres uygulamaları gerçek transaction'ı kullanır.
2. `IPortionCancellationDecisionService.ProcessCancellationAsync(command, connection, transaction)`. Sıra:
   - stok satırı kilidini al;
   - mutfak durumunu kilitli oku;
   - fire kaydını ve on-hand düşüşünü yap;
   - durum geçişini yaz;
   - ayrılmış miktarı düş.
   Hepsi tek transaction'dadır. Ara durum hiçbir zaman görünmez. Fire, ayrılmış miktar düşülmeden önce
   yazıldığı için `available` hiçbir adımda yükselmez. Eski imza kendi transaction'ını açıp aynı yolu kullanır.
3. Tekrar yolları kendini onarır. Önceki bir yarım çalışmadan `Released`/`Waste` durumunda kalmış tutma
   tekrarlanırsa projeksiyon (applied-event ile tek sefer) ve fire (idempotency key ile tek sefer) yeniden
   uygulanır.
4. `ICrossChannelPortionArbiter.CompensateAsync(orderId, actor, reason, connection, transaction)`.
   V12-ONL-003'ün yerel iptali bunu kendi transaction'ıyla çağırır.

## Out of scope

- Arbiter'ın tüketilmiş tutma tekrarı, kilit sırası ve test güçlendirmesi (V12-RMD-003).
- QR reddinde mutfak iptalinin sırası (V1-RMD-313).

## Dependencies

- V12-GOV-005

## Deliverables

- Yukarıdaki overload'lar ve atomik iptal yolu.
- Gerçek Postgres testleri: dış transaction geri alınınca telafinin tamamı geri alınır; fire sırasında eşzamanlı
  satış son porsiyonu alamaz; yarım kalmış Release/Waste tekrar ile tamamlanır.

## Acceptance evidence

- İlgili Inventory ve Host test projeleri yeşil; yeni atomiklik testleri var.
- Mutasyon kontrolü: sıra geri çevrilince, kilit kaldırılınca, tekrar onarımı kaldırılınca testler kırmızıya döner.
- `task_scope_tool.py --task-id V1-RMD-310 --diff-base <InProgress commit>` exit 0.
- Kanıt: `evidence/V1-RMD-310/`.

## Handoff

- None
