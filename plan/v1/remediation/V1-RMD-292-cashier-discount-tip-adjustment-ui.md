# V1-RMD-292 - Kasada indirim, bahşiş ve düzeltme arayüzü

- Task ID: V1-RMD-292
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

Sunucuda `billing/bills/{id}/discount`, `/tip`, `/adjustments` ve `bills/custom` ile bölünmüş hesap için `split-design/**/discount|tip|adjustments|custom` uç noktaları var (`BillingSplitApplication.cs`); hiçbir istemci bunları çağırmıyor. Gönüllü bahşiş yasal olarak izin verilen tek hizmet bedeli biçimidir; arayüz olmadan kasiyer bahşiş girip indirim uygulayamıyor. Bu görev Kasa ödeme ekranına indirim, bahşiş ve düzeltme akışlarını (yetki ve Türkçe hata çevirisiyle) ekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-292-cashier-discount-tip-adjustment-ui.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/15-discount-and-tip.spec.js

## In scope

1. İndirim, gönüllü bahşiş ve düzeltme formları, yetki kontrolü, Türkçe hata iletileri, testler.

## Out of scope

- Zorunlu hizmet bedeli (yasa dışı, eklenmez).
- Özel hesap (`bills/custom`) oluşturma ekranı ayrı karardır.

## Dependencies

- V13-PUI-001
- V1-RMD-283

## Acceptance evidence

### Kritik bulgu (görevin kendi kabul kanıtı iddiasını yanlışlıyor)

`bill.PayableAmount` hesap oluşturulduktan sonra hiç güncellenmiyor; indirim/bahşiş yalnız `billing.bill_adjustments`'a satır ekliyor. Parayı fiilen kapı gibi denetleyen `PaymentAllocationFactory.Create` ve `BillPaymentClosureCalculator` hâlâ bu ham tutarı kullanıyor:

- Bir bahşiş, orijinal tutarı aşan her tahsilatı `OverAllocationException`'a (409) çarptırdığı için ASLA tahsil edilemiyor.
- İndirimli bir hesap, kapanış için hâlâ orijinal tam tutar gerektirdiği için, indirimli tutar tahsil edilse bile ASLA kapanmıyor.

Bu yüzden görevin 'ödenecek tutar buna göre değişir' kabul kanıtı bugünkü sunucu davranışıyla YANLIŞ. İndirim ve bahşiş gerçek, kalıcı, denetlenen kayıtlar (yetki akışı dahil), ama parasal etkileri yok. Kök neden Host/Modules (Payments/Billing) dosyalarında, bu görevin Owned surface'ı dışında; düzeltme için `V1-RMD-298` açıldı (finansal etkisi olan, Semih onayı önerilen bir tasarım kararı gerektiriyor).

### Uygulama

Bu bulgu ışığında ekran DÜRÜST tasarlandı: yanlış bir 'ödenecek tutar' göstermek yerine, indirim/bahşişi ayrı, açıkça etiketlenmiş bir bilgi satırı olarak gösterir; 'Kalan' hâlâ sunucunun gerçek (tavan uygulayan) `remainingAmount`'ıdır; bilgi satırının altında 'Bu tutar bilgi amaçlıdır; kasa hâlâ aşağıdaki "Kalan" tutarını tahsil eder.' notu vardır.

- İndirim formu: neden (4 katalog kodu, Türkçe etiket), hesap türü (yüzde/tutar), değer, not. Grant akışı: uygulandı/yönetici onayına düştü/reddedildi üç durumu da ele alınır. `GRANT_DENIED`'ın sunucudaki İngilizce mesajı ("Discount request was denied.") istemci tarafında Türkçe metinle geçersiz kılınır (ayrı, küçük bir bulgu, aynı commit'te düzeltildi).
- Bahşiş formu: tutar, not. `bills.split` yeterli, grant akışı yok (görevin doğru okuduğu ayrım). Sunucunun kendi hata mesajları (403 FORBIDDEN, 403 FEATURE_DISABLED) zaten doğru Türkçe, olduğu gibi gösterilir.
- Düzeltme (adjustments) listesi salt-okur: her kalemin türü (İndirim %/tutar/Ücret/Kuver/Bahşiş/Özel ücret), nedeni, tutarı ve kaydedilen düzeltmelerle toplam.
- "Özel hesap" (`bills/custom`) ekranı ve zorunlu hizmet bedeli görevin kendi Out of scope'una göre eklenmedi.

### Kanıt

- Cashier E2E (gerçek Host + Chromium): 41/41 (38 mevcut + 3 yeni `18-discount-and-tip`). Yeni senaryolar: indirim ve bahşiş gerçekten kaydedilir ve görünür (ayrı bir GET ile sunucu gerçeği doğrulanır: `AdjustedPayableAmount` 110); tahsilat tavanı dürüstçe ham tutarda kalır (bahşiş dahil tutar denenince reddedilir); yetkisiz kullanıcının indirim talebi `GRANT_DENIED` ile reddedilir ve Türkçe gösterilir, İngilizce sunucu mesajı asla ekrana çıkmaz.
- Mutasyon kontrolü: `split-payment.js` eski hâline döndürülünce 3 yeni senaryo da kırıldı.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None
