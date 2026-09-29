# V14-INV-002 - kabul kanıtı

Görev dosyasındaki kabul ölçütlerinin karşılandığını gösteren özet; komut çıktıları bu klasördedir.

- Oluşturulan toplamlar, kaynak işlemler ve vergi gruplarıyla mutabakata varır; nesil hesap bakiyesine ikinci bir borç
  eklemez.
- Gerçekleşen (gerçek PostgreSQL 18, tüm migration'lar; `evidence/V14-INV-002/tests.log`): 21/21. Fatura yalnız
  kaynak kümenin `Charge` hareketlerinden oluşur; her hareket ödediği adisyonun KDV oranlarına (kalemler, indirim ve
  ücret düzeltmeleri; bahşiş hariç) kuruşuna kadar oransal bölünür, parçalar hareket tutarına eşittir. Satır başına
  bir KDV oranı, KDV dahil brüt; net + KDV = brüt ve fatura toplamı hesaba yazılan tutarların toplamıdır. Cari
  defterde satır sayısı ve toplam değişmez. Yeniden deneme aynı faturayı döner, başka profil reddedilir, eşzamanlı
  altı çağrı tek fatura üretir. Alıcı adı, VKN/TCKN, vergi dairesi, adres ve e-posta şifreli anlık görüntüdür;
  sonraki müşteri düzenlemeleri faturayı değiştirmez. Vergi kimliği olmayan ya da anonimleştirilmiş müşteri, iptal
  edilmiş ya da yalnız ödeme içeren küme ve adisyonu bulunamayan hareket reddedilir, hiçbir kayıt yazılmaz. Satırlar ve
  içerik veritabanı tetikleyicisiyle değiştirilemez ve silinemez.
- Oransal bölme devre dışı bırakılınca üç test kırmızı (`evidence/V14-INV-002/red-without-split.log`); migration 168
  ileri/geri/ileri temiz (`evidence/V14-INV-002/migration-up-down.log`). Modül sınırı, bileşim/manifest (164/164),
  kaynak seçimi ve API kuralı paketleri yeşil.
- Kapsam notu: satıcı bilgisi, fatura numarası ve GİB'e gönderim V14-QNB-002'de; e-Fatura/e-Arşiv seçimi girdi
  olarak alınır (kayıtlı kullanıcı sorgusu V14-QNB-001). HTTP ucu ve arayüz V14-UI-002'de.
