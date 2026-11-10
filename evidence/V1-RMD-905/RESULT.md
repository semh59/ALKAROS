# V1-RMD-905 sonucu

Hesap açıkken yapılan kalem ikramı hesap kalemine yansımıyordu; düzeltildi. Misafir ikram edilen kalemi ödemeye devam ediyordu ve hesap toplam ikram sınırı (902/903) o ikramı görmüyordu.

## Kanıt
- Kırmızı (red-1.log): ikramdan sonra billing.bill_items satırı Sale kaldı.
- Düzeltme (hesap kalemi aynı id ve satır sürümüyle yerinde güncellenir; sil-ekle yapılsaydı bölünmüş hesabın tahsislerine ve düzeltmelerine bağlı FK ya hata verir ya da kayıtları silerdi (şema sürümüne göre)): ItemExceptionHandler.ApplyComplimentaryAsync siparişi kaydeden işlemin içinde bir geri çağrı çalıştırır; uç nokta açık hesabın kalemini BillItem.FromOrderItem ile Complimentary satırla değiştirir (gerçek değer discount_amount, gross/net/tax 0, ödenecek tutar 0). Sipariş ve hesap tek işlemde yazılır.
- Yeşil: OrderCompOnOpenBillHttpTests 4 vaka; Orders.Comp.Tests 34/34 (green-final.log); ItemExceptions.Tests 22/22 (itemexc.log).
- Mutantlar (mutations.txt): 6 öldü.

- Tur 2 bulgusu: hesapta sabit tutarlı indirim varken kalem ikramı hesabı ödenemez yapardı (indirim > yeni taban, her okumada 400). Artık ikramdan önce AdjustmentCalculator ile denenir; sığmazsa 409 DISCOUNT_EXCEEDS_PAYABLE, sipariş ve hesap değişmez (test + mutant 5/6).

## Açık
- Açık hesaba kısmi ödeme (tahsis) yapılabiliyor ve ödeme yolu hesap durumunu değiştirmiyor; böyle bir hesapta ikram sonrası ödenecek tutar ödenenin altına inerse hesap fazla ödenmiş görünür; bu görev kapsamında ele alınmadı (ürün kararı: fark iadesi mi, kapanış mı).
- Kalem ikramı hesap düzeyi kapıdan geçmez, geri alınamaz (903'ten devreden).
