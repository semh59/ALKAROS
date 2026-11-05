# V1-RMD-825 sonuç

Kapatılanlar: hesap bulunamadı ekranı sade cümle + "Bekleyen hesaplara dön" (T-11/K15); hata bildirimi Kapat'a basılana dek kalır, aynı cümle tekrarlanmaz, en çok 3 hata görünür, bölge kaydırılır, Kapat düğmesinin adı "Bildirimi kapat" (F0390); gönderilmiş ama yanıtı bilinmeyen tur yenilemede kaybolmaz, yalnız sepet aynı satır ve miktarlardaysa ve aynı kasiyerse yeniden gönderilir, vardiya kapanışında silinir (F0354); açılışta sepet güncel fiyata çekilir, artık satılmayan ürün çıkarılıp bildirilir (F0401).
F0324 (KASA-1 serbest bırakma `finally`'de değil): bilinçli tasarım, `cashier-app.test.js` "keeps KASA-1 attached and resends the same round..." ve "does not release KASA-1 when the submit call is lost..." ile kilitli; değişiklik gerekmedi.

## Doğrulama
- StaticApps 315/317, kalan 2 kırmızı G5 alanı (`error-dictionary-lock`, `waiter-app`).
- Playwright E2E 01-02, 04-05, 13, 20, 22, 27 yeşil (18/18, önceki turda 01-06, 13, 20, 22, 23, 26, 27 28/28).
- Mutasyon 11 mutant, hepsi öldürüldü (mutation-summary.txt).
- İncelemeler: qa-frontend (sepet uyuşmazlığı, kasiyer sızıntısı, bayat yük, toast yığılması: giderildi), qa-ux-tasarim (bağlantı biçimi, yığılma, aria: giderildi). Kabul: modifier farkı satırda saklı değerden alınır (sunucu fiyatı yeniden hesaplar); bekletilmiş fişler yeniden çağrılırken yenilenmez.
