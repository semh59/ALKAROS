# V1-RMD-524 kanıtı

Eklenen: `POST .../billing/bills/{billId}/refund-requests` gövdesi isteğe bağlı `items` (billItemId, quantity, restoreStock) alır; yanıt ve liste `items` döner. Yeni tablo `payments.refund_request_items` (migration 186).

Gerçek Host + Postgres testleriyle doğrulananlar (Billing test projesi tamamı yeşil):
- Seçilen adetler kadar kısmi iade; ödeme PartiallyRefunded, ters kayıt PartialRefund, kasadan çıkış yazılır, hesap iptal olmaz.
- Tutar sunucuda hesaplanır: kalem brüt × adet / kalem adedi × hesap indirim oranı (250 TL hesapta 25 TL indirimle 100 TL'lik kalem 90 TL).
- Açık/onaylı istekler adetleri ayırır (ödemeler arası); reddedilen istek adetleri serbest bırakır; kalanı aşan istek 409 REFUND_ITEM_QUANTITY.
- İade ödemenin kalan tutarını aşarsa 409 REFUND_AMOUNT_EXCEEDS_PAYMENT; yinelenen, sıfır, negatif, 3 ondalıktan ince adet ve başka hesaba ait kalem 400.
- Kısmi iadeden sonra kalan tutarın tamamı kalemsiz iade olarak istenebilir.
- Bütçe içinde kalem iadesini kasiyer kendisi tamamlar ve bütçeden düşer (V1-RMD-520).
- Stoğa dön seçilen kalemler iade tamamlanırken (nakit onayı ve kartlı fiş girişi) V1-RMD-523 ile aynı işlemde döner; art arda iadelerde her pay bir kez döner; seçilmeyenler dönmez.
- Mimari testler (ModuleBoundaries 9, ApiConventions 5), manifest 16, tutarlılık denetimi temiz.

Varsayım (PO onayı bekler): hesap seviyesi indirim iade tutarına orantılı yansır; bahşiş ve hizmet bedeli iade edilmez.
Doğrulanmayanlar: gerçek tarayıcı/yazıcı yok; kasa oturumu kapanışında kısmi iade nakdi yalnız nakit çıkış kaydı olarak doğrulandı.
