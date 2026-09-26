# V12-RMD-003 - Kanallar arası porsiyon arbiter'ını sertleştir

- Task ID: V12-RMD-003
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin V12-STK-001 ve V12-QRO-003 için kalan bulgularını kapatmak (Semih: "en
küçük hata bile kritik"):

- Tüketilmiş tutmaların tekrarı `Replayed` ("tutuldu") dönüyor. Eşzamanlı ikinci QR onayı stoku kendi
  transaction'ında bir kez daha düşüyor; bunu yalnız siparişin satır sürümü kontrolü geri alıyor. Bu kontrolü
  olmayan bir çağıran stoku iki kez düşürür.
- QR onayında arbiter yalnız ürünün stok satırlarını kilitliyor, tüketim sonra ürün ve değiştirici satırlarını
  sıralı kilitliyor. Bir değiştirici satırı zaten kilitli bir ürün satırından önce sıralanırsa iki transaction
  birbirini bekleyip kilitlenebilir (40P01). Sınıf yorumu "asla kilitlenmez" diyor.
- Kanıt zayıflığı. "Dört kanal" testi her kanalı arbiter'dan geçiriyor; oysa kasa ve garson tüketim guard'ından
  geçer. QR stok kaybı testi arbiter çağrısı olmadan da geçerdi.

## Owned surface

- `plan/v1.2/shared-stock/V12-RMD-003-cross-channel-arbiter-hardening.md`
- `evidence/V12-RMD-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — `AlreadyConsumed` sonucu ve yorum düzeltmesi.
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs (V12-QRO-003) — önce
    bütün satırları sıralı kilitle, `AlreadyConsumed`'u eşzamanlılık çakışması say.
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs — sıralı kilit yardımcısını
    dışa açmak.
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs (V12-ONL-002) — `AlreadyConsumed`'u
    sağlayıcı reddi saymamak.
  - tests/Modules/Inventory/CrossChannelReservation/ ve tests/Host/Experience/Orders/Confirmation/ — testler.

## In scope

1. `CrossChannelReservationOutcome.AlreadyConsumed`. Siparişin aynı tutmaları zaten tüketilmişse hiçbir şey
   tutulmaz ve yazılmaz; `IsHeld` false döner.
2. QR onayı:
   - Arbiter'dan önce tüketimin ihtiyaç duyacağı bütün stok satırları (değiştiriciler dahil) tek sırada kilitlenir.
   - `AlreadyConsumed` sonucu, siparişin güncel sürümüyle `StaleOrderRowVersionException` olur (409).
3. Sipariş alımı `AlreadyConsumed` görürse sağlayıcı iptali istemez; olay hata olarak yeniden denenir.
4. Testler:
   - üretimdeki biçimiyle dört kanal yarışı: kasa ve garson tüketim guard'ından, QR ve online arbiter'dan geçer;
   - tüketilmiş tekrarın sonucu;
   - QR onayının `Qr` kanal kaydıyla tüketilmiş tutma bıraktığı;
   - QR onayının değiştirici satırlarını arbiter'dan önce kilitlediği.

## Out of scope

- QR reddinde mutfak iptalinin sırası (V1-RMD-313).

## Dependencies

- V1-RMD-310

## Deliverables

- Yukarıdaki kod ve testler.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V12-RMD-003/` altında.
- `task_scope_tool.py --task-id V12-RMD-003 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
