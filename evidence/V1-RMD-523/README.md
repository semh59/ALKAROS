# V1-RMD-523 kanıtı

`IStockMovementReversalService.ReturnConsumedShareAsync`: bir sipariş kaleminin tüketim hareketlerinden, iade edilen adede karşılık gelen payı `Return` hareketi (giriş, kaynak Order + kalem kimliği) olarak yazar ve bakiyeyi çağıranın bağlantı ve işleminde artırır.

Gerçek Postgres testleri (22/22, bunlardan 6'sı yeni):
- Her malzemeden orantılı pay döner, bakiye artar (iki malzeme).
- Birikimli pay hesabı: 10 birim / 3 adet için 3,3333 + 3,3334 + 3,3333 = tam 10; sapma yok.
- Tüketilenden fazla dönmez; tamamı döndükten sonra tekrar çağrı boş döner.
- Bütünüyle geri alınmış (Reversal) tüketime dokunulmaz.
- Geçersiz miktar/boş gerekçe reddedilir; çağıranın işlemi geri alınınca hareket ve bakiye de geri döner.

Doğrulanmayanlar: gerçek siparişin tüketim hareketleri ile uçtan uca akış (V1-RMD-524'te). Kabul edilen sınır: aynı kalemin eşzamanlı iadelerini çağıran serileştirir (hesap kilidi).
