# V1-RMD-825 - Hızlı kasa düşük kalemleri: kalıcı hata bildirimi, yenilemede kaybolmayan gönderim, bayat fiyat, hesap bulunamadı ekranı

- Task ID: V1-RMD-825
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G7c kalemlerinden vanilla kasa (`Cashier/wwwroot`) parçası (`3-tasarim.md` T-11; `2-arayuz-kodu.md` K15; `evidence/V1-RMD-739` F0390, F0354, F0324, F0401). PO kararı (2026-11-04): en küçük hata kritik sayılır, açık bırakma yok. Kodda doğrulanan durum:

1. T-11/K15 `split-payment.js` `renderMissingBill`: "Bu sayfa geçerli bir hesap kimliği (billId) gerektirir." teknik terim gösterir, ne yapılacağını söylemez, geri düğmesi yok. Düzeltme: "Hesap açılamadı. Bekleyen hesaplardan tekrar seçin." ve "Bekleyen hesaplara dön" bağlantısı (`/pending-checks`, 48 px).
2. F0390 `cashier-app.js` `showToast`: hata bildirimi 6 sn'de kendiliğinden kapanır; "Sipariş iletilmiş olabilir" gibi kritik cümle kaçırılabilir. Düzeltme: hata bildirimi "Kapat" düğmesine basılana dek kalır (başarı 5 sn'de kapanmayı sürdürür).
3. F0354: sayfa yenilenirse `pendingDispatch` (gönderilmiş ama sonucu bilinmeyen tur) kaybolur, yeni kimlikle ikinci sipariş açılabilir. Düzeltme: `localStorage`'da saklanır (`alkaros_cashier_pending_dispatch`), açılışta geri yüklenir, kesin ret ve başarıda silinir; vardiya kapanışında etkin fişle birlikte silinir (cash-session.js aynı anahtar listesine eklenmez: yalnız bu sayfa okuyup siler; vardiya sınırında bayat gönderim kalmaması için `cash-session.js` anahtarı da temizler).
4. F0401: etkin sepet `localStorage`'dan eski fiyatla geri yüklenir, ekranda yanlış toplam görünür (sunucu fiyatı yeniden hesaplar). Düzeltme: katalog gelince her satırın fiyatı güncel ürün fiyatı artı seçili seçenek farklarıyla yeniden hesaplanır; katalogda artık bulunmayan ürün satırı sepetten çıkarılır ve kasiyere bildirilir.
5. F0324 (KASA-1 serbest bırakma `finally`'de değil): kod bilerek yeniden denemeye çevrilmiştir (aynı yük her basışta yeniden gönderilir, serbest bırakma yalnız mutfak aldıktan veya kesin retten sonra); `cashier-app.test.js` "keeps KASA-1 attached and resends the same round..." ve "does not release KASA-1 when the submit call is lost..." testleriyle kilitli. Değişiklik gerekmez; kanıt RESULT.md'dedir.

## Owned surface

- `evidence/V1-RMD-825/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-RMD-707) — kalıcı hata bildirimi, bekleyen gönderim, fiyat yenileme.
  - src/Clients/Cashier/wwwroot/cashier-app.css (V1-CUI-008) — bildirim kapat düğmesi.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js (V1-RMD-241) — vardiya kapanışında bekleyen gönderim anahtarını siler.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — hesap bulunamadı ekranı.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — bağlantı biçiminde düğme.
  - tests/Clients/StaticApps/cashier-low-items.test.js (V1-RMD-109) — yeni dosya: bu görevin testleri.
  - tests/Clients/StaticApps/cashier-app.test.js (V1-RMD-253) — bildirim metni okuyucusu kapat düğmesini saymaz.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.
3. Mevcut `cashier-app.test.js`, `cashier-client-resilience.test.js` ve kasa özellikleri yeşil kalır.

## Out of scope

- Sunucu, veritabanı, geçiş; React kabuğu (V1-RMD-826).

## Dependencies

- V1-RMD-824

## Acceptance evidence

1. StaticApps tam paket (G5'in kırık testi hariç) ve ilgili Playwright özellikleri yeşil; kırmızı sonra yeşil: hesap bulunamadı metni ve dönüş bağlantısı, hata bildirimi kapanmaz ve Kapat ile kapanır, başarı bildirimi kapanır, bekleyen gönderim yenilemeden sonra aynı kimlikle yeniden gönderilir ve başarıda silinir, eski fiyatlı sepet güncellenir, katalogdan kalkan ürün çıkarılır.
2. Mutasyon: her yeni davranış bozulunca ilgili test kırılır; `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, düzeltme sonrası doğrulama turu.
5. Semih'in elle deneyeceği senaryo: hızlı kasada sipariş gönderirken bağlantıyı kes; kırmızı bildirim sen kapatana dek durur. Sayfayı yenile, gönder; ikinci sipariş açılmaz. Hesap kimliksiz tahsilat adresi açılınca sade cümle ve "Bekleyen hesaplara dön" görünür.

## Handoff

- None
