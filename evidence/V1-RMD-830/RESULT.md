# V1-RMD-830 sonuç

Kapatılan (K18 arayüz): tahsilat sayfasındaki "Uygulanan indirim, bahşiş ve ücretler" listesinde indirim ve bahşiş satırlarında "Geri al" düğmesi (hizmet bedeli, kuver, özel ücret satırında yok). Basınca `POST .../adjustments/{id}/undo` gider, liste, toplam ve ödeme tutarı yenilenir. Hata Türkçe ve `role="alert"` ile gösterilir, odak hata kutusuna gider: 404 "zaten kaldırılmış; liste yenilendi", durum çakışması "Bu hesap durumunda indirim ya da bahşiş geri alınamaz.", toplanan tutarın altı (sözlükteki cümle doğrulandı), sunucu ve ağ hatası. Geri alma yapıldıktan sonra yalnız yenilemenin düşmesi "Geri alındı, ancak liste yenilenemedi" der. Düğmeler gönderim, indirim, bahşiş ya da başka geri alma sürerken pasiftir ve kalem adı/tutarı ile aria-label taşır.

## Doğrulama
- StaticApps 31 dosya, tüm paket yeşil (`error-dictionary-lock` dahil); yeni dosya `split-payment-undo-adjustment.test.js` 13 test.
- Mutasyon 13 mutant öldürüldü; tek yaşayan: 404 yolunda tutarın yeniden çizilmesi, sahte sunucuda aynı değer olduğundan fark edilmez (zararsız).

## İncelemeler (qa-frontend, qa-test-kalitesi)
Giderilen: yenileme hatasının "bağlantı kurulamadı" olarak yanlış gösterilmesi, odak ve ekran okuyucu etiketi, meşgulken pasiflik, ham mesaj sızıntı testi, 404 yenileme ve kaçış testleri. Kabul edilen: `undoAdjustment` içinde ayrı meşgul koruması yok (düğme senkron çizimle pasifleşir, test kilitler).
