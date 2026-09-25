# V1-RMD-294 - Müşteri ekranı ödeme, indirim ve iptalde güncellenir

- Task ID: V1-RMD-294
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`CustomerDisplayHub.SnapshotChanged` yalnız sipariş oluşturma, kalem ekleme/çıkarma ve gönderme uç noktalarından yayınlanıyor (`DualScreenApplication.Endpoints.cs`); ödeme (`DualScreenApplication.Payments.cs`), nakit tahsilat ve hesap indirimi sonrası yayın görünmüyor. Bu durumda müşteri ekranı ödeme sonrası eski tutarı gösterebilir. Kodda yalnız okuma ile saptandı; önce yerelde doğrulanır, doğruysa ödeme ve hesap değişikliği uç noktalarına yayın eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-294-customer-display-refresh-on-payment.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (yalnız yayın çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (yalnız yayın çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/16-customer-display-refresh-on-payment.spec.js

## In scope

1. Yerelde tekrar üretme, ödeme ve nakit tahsilat sonrası yayın, testler.

## Out of scope

- Müşteri ekranı yeni tasarımı.

## Dependencies

- V1-RMD-282

## Acceptance evidence

### Yerel doğrulama (görevin şart koştuğu ilk adım)

- `CustomerDisplaySnapshotDto` tamamen `orders.orders`/`order_items`'tan kuruluyor (`DualScreenStore.Display.cs`). Hesap indirimi yalnız `billing.bill_adjustments`'a yazıyor, `orders.orders`'a hiç dokunmuyor — bu yüzden müşteri ekranının okuduğu hiçbir alan indirimle değişmiyor. Kodda doğrulandı, indirim tarafında yayın eklenmedi çünkü yenilenecek bir şey yok.
- Ödeme ve nakit tahsilat ise gerçek: son tahsilat hesabı kapatıp siparişi `Completed` yapınca (`OrderSettlementService.CompleteForPaidBillAsync`) `GetSnapshotAsync` `state="Completed"`, `message="Teşekkür ederiz."` döndürüyor, ama hiçbir yayın gitmiyordu.

### Uygulama

- `TryCloseBillAsync` (`Payments.cs`, kart/EFT/manuel kart onayı ve `CashSession.cs`'in nakit tahsilatının paylaştığı tek kapanış noktası) artık hesap kapanınca `CustomerDisplayHub.SnapshotChanged`'ı best-effort yayınlıyor; başarısız yayın ödemeyi asla hataya çevirmiyor.
- Host testleri (UTF8 Postgres 18): `PaymentTender` 30/30, `CashSession` 14/14, `Composition` 10/10; üçü de değişmeden geçti, hiçbir regresyon görülmedi.
- Cashier E2E (gerçek Host + Chromium): 37/37 (36 mevcut + 1 yeni `16-customer-display-refresh-on-payment`). Yeni senaryo: gerçek bir eşleştirilmiş ekran, PosTerminal'in gerçek DualScreen sipariş akışıyla (yalnız bu akış `active_order_id`'yi set ediyor) açılan bir siparişin son tahsilatından sonra sayfa yenilenmeden, ekranın kendi 5 sn'lik yedek sorgusundan çok daha kısa sürede (2 sn altı) 'Teşekkür ederiz.' gösterdiğini kanıtlıyor.
- Mutasyon kontrolü: `Payments.cs`/`CashSession.cs` eski hâline döndürülünce yeni senaryo kırıldı (ekran 2 sn içinde güncellenmedi).
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

### Kapsam notu

- Hesap indirimi kanadında hiçbir kod değişikliği yapılmadı; yukarıdaki bulgu bunun neden gerekmediğini açıklıyor. Bill-level indirimin müşteri ekranına yansıması istenirse bu, `CustomerDisplaySnapshotDto`'nun veri kaynağını `orders.orders`'tan bill/tahsilat verisine genişletmeyi gerektirir — ayrı, daha büyük bir tasarım kararı, bu görevin kapsamı dışında.

## Handoff

- None
