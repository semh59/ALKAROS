# V12-GOV-009 karar kaydı - Online yemeğin tek ekrandan yönetilmesi

- Karar tarihi: 2026-09-27
- Onaylayan: Semih (Founder/Product Owner)
- Hazırlayan: Claude Opus 5.5 (session 703155c9)

## İstek

"Benim istediğim şey online yemek tek ekrandan kullanması. Hem sipariş kısmı, hem menü gibi ayarlar."

## Bugünkü durum (2026-09-27)

| Parça | Durum |
| --- | --- |
| Sipariş kuyruğu (kabul, red, teslim, iptal, not, sorunlu olaylar) | "Online siparişler" ekranı |
| Platform API bilgileri | Ayrı "Online platform bilgileri" ekranı |
| Menü yayını (fiyat, satışta mı) | Yalnız arka uç, ekran yok |
| Ürün eşleme | Uç nokta ve ekran yok; Trendyol Go bu yüzden kullanılamaz |
| Stok değişince platformda kapatma | Otomatik, durumu görünmez |
| Bağlantı sağlığı | Yalnız mutabakat vakası olarak |
| Restoranı platformda kapatma/yoğun | Yok |

## Semih'in cevapları

- Sekmeler: Siparişler, Menü, Sorunlar, Ayarlar; "Menü ve ayarlar yönetici şifresiyle giriş yapılırsa görünür olsun".
- Restoranı platformda kapatma/yoğun: "Evet, bu işe dahil et".
- Eski iki giriş: "Tek girişte birleşsin".
- Zaman: "Şimdi, Faz 4'ten önce".

## Platform servisleri (erişim 2026-09-27, uygulamanın kendi tarayıcısıyla)

- Yemeksepeti Partner API, Outlet Management: `GET`/`PUT /v2/chains/{chain_id}/vendors/{vendor_id}/status`;
  durumlar `OPEN`, `CLOSED_TODAY`, `CLOSED_UNTIL` (+ `closed_until`, UTC), `CHECKIN`; `closed_reason` örn.
  `TOO_BUSY_KITCHEN`, `TOO_BUSY_NO_DRIVERS`.
- Uber Eats Trendyol Go, Restaurant Integration: `PUT /integrator/store/meal/suppliers/{supplierid}/stores/{storeId}/status`
  `{"status":"OPEN"|"CLOSED"}`; `GET .../stores` → `workingStatus`. Süreli kapatma yok; süre dolunca açma isteğini
  ALKAROS gönderir.

## Görevler

`V12-OUI-004` (merkez ekran, tek giriş, bağlantı durumu), `V12-OUI-005` (Menü, ürün eşleme, yayın),
`V12-OUI-006` (Sorunlar), `V12-ONL-011` (restoranı platformda açma/kapama/yoğun). Sıra: OUI-004, sonra diğer üçü.

## Reddedilen alternatif

- Her konu için ayrı ekran (bugünkü dağınıklık): Semih tek ekran istedi.
