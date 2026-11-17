# V1-RMD-1151 bağımsız qa incelemeleri

## 1. qa-backend (sunucu ucu, sayaç, eşzamanlılık)
Kritik/Yüksek yok. Bulgular ve karşılıkları:
- Taslak (Draft) siparişe fiş veriliyordu (Orta): DÜZELTİLDİ, taslak `ORDER_NOT_OPEN` döner; hesap oluşturma da taslağı reddettiği için tutarlı; testi eklendi (Theory: Draft/Cancelled/Rejected/Completed).
- Birleşik hesapta sayaç siparişe özgü (Orta): bilinçli; docs/domain/bill-pre-receipt.md "Kurallar"a yazıldı.
- Hesap sahipliği ve ödenmiş denetimi kilitten önceydi (Orta): DÜZELTİLDİ, sipariş satırı kilidi alındıktan sonra hesap okunur, ödenmiş ve terminal sahipliği o noktada denetlenir; ikinci okuma kalktı.
- "nothing was paid meanwhile" yorumu fazla iddialıydı (Düşük): yorum yeniden yazıldı (kilit altında okunan an).
- Aynı anahtarla tekrarda fiş saati/satırları yeniden kurulur (Düşük): bilinçli; dokümana yazıldı.
- Siparişe bağlı birden çok açık hesap (Düşük): doğrulanamadı; kodda tek aktif hesap varsayımı (BillingSplitStore) ve en yeni hesap alınır.
- Hesapsız yolda sabit "TRY" (Düşük): DÜZELTİLDİ, `order.CurrencyCode`.
- `request?` ve `request!` gereksiz savunma (Düşük): DÜZELTİLDİ.

## 2. qa-frontend (PosTerminal)
- Yazdırma CSS'i masa QR etiketinin yazdırmasını bozuyordu (Yüksek): DÜZELTİLDİ. Fiş yalnız yazdırma süresince sayfada durur (`afterprint` ile silinir), gizleme kuralı yalnız `body.pre-receipt-printing` sınıfıyla çalışır; testi eklendi.
- Bekleyen hesaplarda düğme yetkiye bakmıyordu (Orta): DÜZELTİLDİ. Uç yetkisi `bills.split` yapıldı (iki ekran da bu yetkiyle "Öde"yi gösterir), Bekleyen hesaplarda düğme `bills.split` ile gösterilir; testi eklendi. Not: görev dosyası Goal'unda `payments.take` yazıyordu; iki ekranın yetki kapısıyla aynı olması için `bills.split` seçildi (sapma burada ve docs/domain/bill-pre-receipt.md'de kayıtlı).
- Zaman aşımında yeniden basışta numara boşluğu (Düşük): DÜZELTİLDİ. Sonucu belirsiz kalan basış aynı anahtarla yeniden denenir (sunucu aynı numarayı verir); kesin sonuçtan sonra anahtar bırakılır; 3 test eklendi.
- `orderId` ve `unitPrice` alanları çizimde kullanılmıyor (Düşük): sunucu sözleşmesini yansıtır, bırakıldı.
