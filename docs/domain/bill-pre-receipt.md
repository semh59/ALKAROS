# Hesap ön fişi (ödeme öncesi bilgi fişi)

Karar sahibi: ürün sahibi (2026-11-18). Kayıt: V1-RMD-1151.

## Ne olduğu

Kasiyerin ödeme almadan önce müşteriye verdiği "Hesap" fişidir. Mali olmayan bir bilgi fişidir; fişte "Bu belge mali fiş değildir." yazar ve hiçbir mali değer, KDV dökümü, fiş sıra numarası ya da ödeme belgesi iddiası taşımaz.

## Ödenmiş hesabın fişinden farkı

| | Ödenmiş hesap fişi | Hesap ön fişi |
|---|---|---|
| Uç | `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/receipt/print` | `POST /api/v1/terminals/{terminalId}/orders/{orderId}/pre-receipt` |
| Sayaç | `billing.bills.receipt_first_printed_at`: ilk basım orijinal, sonrakiler KOPYA | Ayrı: `order.pre-receipt.printed` denetim kayıtlarının sayısı; fişte "Hesap fişi no: N" |
| Etki | Orijinal hakkını tüketir | Orijinal/kopya akışına dokunmaz |
| Hesap | Ödenmiş hesap gerekir | Hesap açmaz; açık hesap varsa ondan, yoksa siparişten okur |

Ödenmiş hesabı olan sipariş için ön fiş reddedilir (`BILL_NOT_UNPAID`); gönderilmemiş (taslak), iptal, reddedilmiş ya da tamamlanmış sipariş için `ORDER_NOT_OPEN` döner (taslak sipariş hesaba da çevrilemez).

## Kurallar

- Numara siparişe özgüdür: birleşik hesabı oluşturan siparişlerin her biri kendi sayacını tutar.
- Sayaç, sipariş satırı kilidi altında `order.pre-receipt.printed` denetim kayıtları sayılarak verilir; kayıtlar silinmediği sürece numara kararlıdır.
- Aynı tekrar anahtarıyla gelen istek aynı numarayı alır; fişin satırları ve saati o anki veriden yeniden kurulur.
- Yetki `bills.split` (hesap açma ve bölme yetkisiyle aynı): Öde düğmesini gösteren ekranlar bu yetkiyle çalışır.
- Hesap başka bir kasada açıksa yalnız o kasa ya da `cash.session.override` yetkisi olan kullanıcı basabilir.

## Numara ve yazdırma penceresi (karar: V1-RMD-1158)

- "Hesap fişi no", yazdırma penceresi açılmadan önce verilir: sunucu `order.pre-receipt.printed` kaydını yazıp işlemi kapatır, sonra istemci `window.print()` çağırır (`preReceipt.ts`; masa ekranı `TableOrderBridge.tsx` ile Bekleyen hesaplar `PendingChecksWorkspace.tsx` aynı işlevi çağırır). Numara bu yüzden "basılan" değil "verilen" numaradır.
- Kasiyer yazdırma penceresini iptal ederse numara kullanılmış kalır; sonraki dokunuş bir sonraki numarayı alır. Her numaranın `order.pre-receipt.printed` kaydı vardır (iptal edilen yazdırmanın da); kayıtlarda boşluk oluşmaz, boşluk yalnız basılı fişlerle kayıtlar karşılaştırılınca görünür. Ön fiş mali değildir ve sayaç ödenmiş hesabın orijinal/kopya hakkından ayrıdır; repoda ön fiş numarası için ardışıklık kuralı yoktur. Ön fişin bulut yolundaki "Hesap Özeti Bilgi Fişi" yerine geçip geçmediği (V13-FSC-004) ve boşluğun o belge için sorun olup olmadığı aşağıdaki mali müşavir teyidine bağlıdır. Numara denetim kayıtlarından sayıldığı için, kayıtlar saklama süresi sonunda silinirse numara 1'den başlar. Denetim kaydı iptali ayırt etmez: boşluğun nedeni kayıttan okunmaz.
- "Yalnız gerçekten yazdırılınca artsın" tarayıcı penceresiyle uygulanamaz: HTML standardı (8.9.2 Printing) `afterprint` olayını yazdırma teklifinden sonra koşulsuz tetikler ve `print()` sonucu döndürmez; kullanıcının basıp basmadığı sayfaya bildirilmez. Doğrudan yazıcıya basım (iptalin bilindiği yol) bu kararın kapsamı dışındadır.
- Yazdırma penceresi açıkken sayfa beklemesi uygulama hatası değildir: standart, tarayıcının kullanıcı kabul ya da ret edene kadar `print()` çağrısını durdurmasına izin verir. Uygulamada zamanlayıcı ya da yeniden deneme yoktur; fiş içeriği yalnız `@media print` içinde görünür (`pre-receipt.css`), bu yüzden `afterprint` hiç gelmeyen bir ortamda (otomasyon) ekran etkilenmez. Yerel yazdırma penceresini göremeyen otomasyon araçları bu yüzden takılır; gerçek kasiyer pencereyi kapatınca devam eder.

## Dış bağımlılık: mali müşavirle teyit edilecek

Aşağıdakiler henüz doğrulanmadı ve mali müşavirle teyit edilecektir; teyide kadar fişte mali değer iddiası bulunmaz:

- "Bu belge mali fiş değildir." ibaresinin ve fiş içeriğinin yeterliliği.
- e-Fatura/e-Arşiv mükellefi işletmeler için kâğıt ön fişin e-Adisyon (VUK 509 IV.12) yerine geçip geçmediği; ilgili kayıtlar V0-CMP-001, V20-CMP-001 ve V13-FSC-004'tür.
