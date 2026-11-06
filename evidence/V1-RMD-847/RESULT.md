# V1-RMD-847 sonuç

Kasa ekranında ("Bekleyen hesaplar") aynı masanın ödenmemiş, hesabı açılmamış siparişleri onay kutusuyla seçilir; iki veya daha fazla seçilince "Hesapları birleştir" V1-RMD-846'nın ucunu çağırır ve açılan birleşik hesap mevcut ödeme/bölme sayfasında açılır (`collectionHref`).

## Davranış
- Düğme yalnız bir masada iki veya daha fazla birleştirilebilir sipariş varken görünür; seçim ikiden azken pasiftir. Hesabı açık veya parası alınmış siparişte onay kutusu yoktur. Seçim başladıktan sonra başka masanın kutuları pasiftir ve nedeni yazılır ("Yalnız Masa X siparişleri seçilebilir").
- Çift tıklama tek istek gönderir. Aynı seçim hata sonrası yeniden denenirse aynı anahtar gider (kayıp yanıtta sunucu aynı hesabı döndürür); seçim değişirse veya sunucu isteği reddederse (4xx) yeni anahtar kullanılır.
- Her sunucu hata kodu (9 birleştirme kodu dahil) `billingApi.ts` sözlüğünden Türkçe cümleye gider; ham kod veya sunucu metni gösterilmez. Birleştirme hatası kendi uyarısında görünür, "liste eski olabilir" bandını tetiklemez.
- Onay kutusu 48 px etiket içinde; yenilemede listeden düşen sipariş seçimden çıkar.

## Doğrulama
- Kırmızı `red.log`: 7 test düşüyor. Yeşil `green.log` ve tam koşu `full.log`: 103 dosya, 1103 test geçti; `typecheck.log` tsc exit 0.
- Mutasyon `mutations.log` (cp + cmp ile geri alındı): masa kilidi, "en az iki" kuralı, hesabı-açık kuralı, anahtar yeniden kullanımı, çift gönderim korumasının kaldırılması, hata metni, ham kod sızıntısı, red sonrası anahtar sıfırlama, yenilemede seçim süzme, masa ipucu: hepsinde test düşüyor.
- İngilizce sızıntı taraması `leak-scan.log`: yalnız CSS sınıf adı eşleşmeleri (kullanıcıya görünmez).
- İnceleme: qa-frontend ve qa-ux-tasarim bulguları (dokunma hedefi, anahtar takılması, ipucu, yanlış "eski liste" bandı, yorum yeri) düzeltildi; ardından qa-bulgu-dogrulayici turu.

## Açık notlar
- Yetkisiz kullanıcı için eylem gizlenemiyor: kuyruk uç noktası yetki bilgisi taşımıyor. Sunucu `FORBIDDEN` döner ve Türkçe gösterilir.
- Kuyruk satırında sipariş kaynağı yok; QR/çevrimiçi sipariş seçilirse sunucu reddeder (`MERGE_ORDER_SOURCE`, Türkçe gösterilir).
- Elle deneme (iki sipariş, birleştir, öde, iki sipariş tamamlanır) bu ortamda tarayıcıyla yapılmadı; sunucu tarafı uçtan uca testi V1-RMD-846'dadır.
