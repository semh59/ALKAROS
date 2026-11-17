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

## Dış bağımlılık: mali müşavirle teyit edilecek

Aşağıdakiler henüz doğrulanmadı ve mali müşavirle teyit edilecektir; teyide kadar fişte mali değer iddiası bulunmaz:

- "Bu belge mali fiş değildir." ibaresinin ve fiş içeriğinin yeterliliği.
- e-Fatura/e-Arşiv mükellefi işletmeler için kâğıt ön fişin e-Adisyon (VUK 509 IV.12) yerine geçip geçmediği; ilgili kayıtlar V0-CMP-001, V20-CMP-001 ve V13-FSC-004'tür.
