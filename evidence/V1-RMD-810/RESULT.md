# V1-RMD-810 - Sonuç

Müşteri ekranı verisi tek kaynaktan ve toplamı tutacak şekilde üretilir; indirim, bahşiş ve ödeme anında ekrana haber verilir.

## Kök nedenler (kodda doğrulandı)

- Satırlar indirim sonrası, Ara toplam indirim öncesi, büyük rakam hesaptan geliyordu; hesap indirimi ve bahşiş hiç görünmüyordu.
- `/tip` hiç, `/discount` yalnız bakiye 0 olunca hub'a haber veriyordu.
- `Completed` siparişte ödenen ve toplam sıfırlanıyordu (`Apply` erken dönüyordu).
- Hesap iptalinde sipariş kapanmadığı için ekran "Siparişiniz alındı" kalıyordu.

## Değişiklik

- `CustomerDisplayPaymentProgress`: hesap varken satırlar hesap kalemlerinden (indirim öncesi), `Subtotal` = satır toplamı, `DiscountTotal` = kalem + hesap indirimi, `TipTotal`, `FeeTotal`, `Total` = ayarlanmış ödenecek tutar, `PaidTotal`/`RemainingTotal`; eşitlik: `Subtotal − DiscountTotal + FeeTotal + TipTotal = Total = PaidTotal + RemainingTotal`. Tek ayarlama okuması hem dökümü hem ödeme projeksiyonunu (`BillPaymentClosureCalculator`) besler. `Completed` siparişte de uygulanır. Yalnız iptal hesaplı ve düzenlenmeyen sipariş `Idle` olur (satır/tutar sızmaz).
- `DualScreenStore.Display`: hesapsız siparişte satır = gross + indirim, ikram tam fiyat ve tam indirimli; satır `DiscountAmount` taşır.
- `DualScreenApplication.NotifyCustomerDisplayAsync` (tek en-iyi-çaba bildirim): ödeme, kapanış (kapanış hatasında da), hesabı ekrana bağlama, `/tip`, `/discount`.
- Sözleşme: satır `discountAmount`, ekran `tipTotal`, `feeTotal`; PosTerminal tipleri.

## G5 ile bağlantı noktası

Hesap iptal ucu (`/billing/bills/{id}/cancel`) hâlâ hub'a bildirim göndermiyor; G5 iptal işini yaparken `DualScreenApplication.NotifyCustomerDisplayAsync(hub, loggerFactory, terminalId, billId, "PaymentChanged", ct)` çağırmalı (bu görevde dokunulmadı). Anlık görüntü tarafı hazır: siparişin tüm hesapları iptalse veya sipariş iptal/reddedildiyse ekran boşa döner.

## Doğrulama (gerçek Postgres, port 55432; çıktı dizini `artifacts/g6`)

- Kırmızı: `red-host/` (statement testleri, derleme + 11 başarısız); mağaza testleri uygulamayla birlikte yazıldığı için mutasyonla kanıtlandı (m9, m10).
- Yeşil: Host `DualScreen` sınıfları 183 geçti, 5 başarısız = yalnız G5'in yarım-porsiyon testleri (`DualScreenHalfPortionTests`, kendi görevi sürüyor); PaymentTender 80/80 + düzeltme sonrası alt küme 11/11; Billing 154/154; ApiConventions 17/17; ModuleBoundaries 17/17; PosTerminal tsc 0.
- Mutasyon (hepsi `cmp` ile geri alındı, `mut/*.result`): bill indirimi atlama, bahşiş, toplam, iptal temizleme, Completed, satırlar, negatif kalan, Completed mesajı, satır indirim formülü (m9, m10), `/tip` ve `/discount` bildirimi, iptal bayrağı, iptal hesabı canlı sayma (m15), ilk hesaba indirgeme (m16-18), bildirim hatasını yutma: hepsi test kırdı; ilk geçen iki mutant (negatif kalan, Completed mesajı) için test eklendi ve sonra kırıldı.
- Atlanan test yok.

## İncelemeler

- qa-backend: ücret dengesi (FeeTotal eklendi), çift okuma (hesaplayıcıya geçildi, 2 sorgu az), mutant boşlukları, kapanış hatasında bildirim (düzeltildi). Reddedilen: aynı kalemin iki hesapta görünmesi (tekil kısıt nedeniyle imkânsız); hesapsız dalda saklı toplamlar (aggregate tutarlı; gerçek akış testi eklendi).
- qa-test-kalitesi: çok hesaplı toplamlar, iptal+yeni hesap karışık gerçek test, BillSettled türü, hub kapalı deneme sayısı, reddedilen indirim, sabit değerli mağaza testi (hepsi eklendi).
- Doğrulama turu (qa-bulgu-dogrulayici): 5 madde KAPANDI. Yeni: aşırı tahsisatta `Total = Ödenen + Kalan` tutmaz (sözleşme notu "aşırı tahsisat yoksa" diye düzeltildi); ekranda bahşiş/ücret satırı V1-RMD-812'de.
