# V1-RMD-737 - Test boşlukları (603 B005, B006, B037)

**Sonuç:** Denetimde sağ kalan mutantların ilgili olanları öldürüldü; yalnız test yazıldı, üretim kodu değişmedi. 24/24 mutasyon KILLED (`mutations.log`). Modül testleri Adjustments 21, BillFoundation 49, SplitDesign 32, PaymentClosure 24; Host Billing 106/106; StaticApps 137/137.

## Eklenen testler
- İndirim/ek ücret toplamları: AdjustedDiscountTotal (satır indirimi dahil), AdjustedTaxTotal, vergi sıfırın altına inemez, indirim = ödenecek tutar kabul, %100 oran kabul (M07, M08, M10–M13, H19, H20).
- Yuvarlama ve fiyat: yarım kuruş sıfırdan uzağa (M01), sıfır birim fiyat kabul / negatif ret (M05).
- Özel bölme eksik toplam reddi (M24, H21, B005).
- Kapanış: mutabakat bekleyen ödeme engel (M18); `BillClosureService` engelleyici varken kapanmaz, çakışmada 3 deneme (H22, H23).
- İade: isteyen kendi isteğini reddedemez (H05), bütçe 0 ve azami değer kabul / aşım ret (H06, H07), kalan tutarı olmayan ödeme istenemez (H03), açık kardeş ödeme varken hesap iptal olmaz (H09; Pending/Unknown/ReconciliationRequired/PartiallyRefunded).
- İptal: havadaki ödeme iptali engeller (H12, H13), tam iade edilmiş ödeme engellemez, kısmi iade engeller (H14).
- İndirim/bahşiş: indirim sonrası tutar tahsilata eşit olabilir (H15), kapanmış hesaba bahşiş reddi (H16), bahşiş özelliği kapalıyken ret (H17).
- Cashier: 5xx sonrası iade isteği aynı işlem anahtarıyla tekrarlanır (727'den kalan boşluk).

## Kapanan / yapılmayan
- M20, M21, M26 (vergi payı yuvarlama): 730'da vergi paylaşımı `BillMath.Apportion`'a taşındığı için o satırlar artık yok; mutantlar geçersiz.
- M17: kodda düzeltilmiş (`allocatedTotal >= adjustedPayable`).
- H08 (kalem iadesi yarım kuruş): ondalık bölme sonuçları kesin yarım kuruşa getirilemediği için öldürülemedi.
- H18 (efektif vergi oranı yuvarlama): kodu değişmiş, ayrı test yazılmadı.
- B037 listesinden eşzamanlılık testleri (iptal+tahsilat, çift onay, bahşiş+kapanış) ve cash-session/customer-accounts axe testi yapılmadı; gerçek eşzamanlılık düzeneği ayrı iş.
