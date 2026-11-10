# V1-RMD-906 sonucu

Kısmi ödemesi alınmış açık hesapta, ödenecek tutarı tahsil edilenin altına indirecek kalem ikramı reddediliyor (Semih kararı): 409 COMP_BELOW_COLLECTED, "Bu hesapta ödeme alınmış; ikram, alınan tutarın altına iniyor. Önce ödemeyi iade edin, sonra ikram edin." Sipariş kalemi ve hesap kalemi değişmiyor.

## Değişiklik
- OrderManagementEndpoints.CompBillLineAsync: ikram işleminin içinde, indirim yolundaki aynı ödeme-tahsis kilidi altında tahsil edilen (payment_allocations) eksi iade edilen (payment_reversals) hesaplanır; ikram sonrası ödenecek tutar bunun altındaysa BillDiscountBelowCollectedException fırlatılır, hata sözlüğü Türkçe 409'a çevirir.
- Sipariş yazılmadan önce hesapların bill-settlement kilitleri, işlem içinde hesap satırı (FOR UPDATE) sonra ödeme-tahsis kilidi (tender, iade, iptal ve indirim yollarıyla uyumlu sıra); Pending, Unknown, ReconciliationRequired ödemesi olan hesapta ikram 409 COMP_PAYMENT_IN_FLIGHT ile reddedilir.
- Test projesine ödeme göçleri eklendi (120, 123, 182).

## Kanıt
- Kırmızı (red-1.log): ödemeli hesapta ikram 200 dönüyordu.
- Yeşil: OrderCompOnPartlyPaidBillHttpTests 4 vaka (iade sonrası, kısmi iade, yolda ödeme dahil), Orders.Comp.Tests 38/38 (green-final.log): tahsilatı aşan ikram reddedilir ve hiçbir şey değişmez; iade edildikten sonra aynı ikram uygulanır.
- Mutantlar (mutations.txt): 5 öldü; kilit kaldırma mutantı tek iş parçacıklı testte görünmez (kabul).

## Açık
- İndirim yolu (BillingSplitStore.ReadCollectedAsync) iadeyi düşmüyor: bir ödeme iade edildikten sonra da indirim engelli kalır. İkram yolu iadeyi düşüyor; iki yol artık farklı davranıyor. Ayrı karar/görev.
- Kart iadesi elle tamamlanana (payment_reversals satırı oluşana) kadar ikram engelli kalır.
