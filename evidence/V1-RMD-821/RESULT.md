# V1-RMD-821 sonuç

Kapatılan bulgular: satır 27 / T-18 (ürüne ilk dokunuşta sipariş; kartlar pasif değil; "Yeni sipariş aç", "Sipariş aç", "Sonraki siparişi aç" kalktı), satır 28 / T-21 / T-22 (bekleyen hesap etiketi masa/Hızlı satış + saat, geri gönderme kartın içinde), F0067 (bekleyen hesaplarda 401 giriş ekranına döner), T-26 bekleyen hesaplar kısmı (geri gönderme sabit cümle), K14 (saat dilimi: format.ts, bekleyen hesaplar, hesap bölme, workspace son güncelleme, eşleştirme bitiş tarihi, cashier-app bekletme saati, cari hesap tarihleri), satır 29 / K10 (tahsil et meşgul gösterimi, boş limit kaydedilmez, limit formu `settings.manage` iznine bağlı, sunucu metni yalnız Türkçe alan/gerekçe kodlarında).

Sunucu metni basma doğrulaması (PO 7): cari hesap uçlarının kodları kodda tek tek incelendi (CUSTOMER_NOT_FOUND, CONCURRENT_UPDATE, CLOSED_CASH_SESSION, TENDER_IDEMPOTENCY_KEY_REUSED, BILL_NOT_FOUND, TENDER_UNSETTLED_PAYMENT_EXISTS sabit cümle; VALIDATION_FAILED, CREDIT_DENIED, RECEIPT_EXCEEDS_BALANCE sunucunun Türkçe, eyleme dönük metniyle); bilinmeyen kod duruma göre genel cümleye düşer.

## Doğrulama
- StaticApps: 281/283; kalan 2 kırmızı bu görevin dışında: `error-dictionary-lock` (G5 çalışma ağacındaki `REFUND_QUANTITY_NOT_WHOLE` sunucu metni İngilizce) ve `waiter-app` zaman aşımı (G5 WaiterPwa alanı, yük altında aralıklı).
- PosTerminal vitest: 953/953, tsc 0, oxlint 0 hata (vitest-final.log, tsc.log, lint.log).
- Playwright E2E (gerçek Host + Postgres 55432): 01, 02, 04, 13, 14, 20, 21, 22, 23, 26, 27, 30, 31 yeşil (36/36); PosTerminal `vite build` yerelde çalışıyor (dist E2E için yeniden derlendi).
- Mutasyon: 15 mutant, 15 öldürüldü, her dosya cmp ile geri alındı (mutation-summary.txt). İlk turda iki mutant yaşadı (çift dokunuş koruması: testte dokunuşlar ayrı çerçevedeydi; workspace saat dilimi: testi yoktu); testler düzeltildi/eklendi, ikinci turda öldü.
- Saat dilimi testleri makine İstanbul saatindeyken boşa geçmesin diye `TZ=UTC` ile koşuldu.
- Kırmızı: first-touch (6 kırmızı) ve customer-accounts-flows (8 kırmızı) düzeltmeden önce kırmızı görüldü; diğerleri mutantlarla kanıtlandı.
- `BillSplitWorkspace.test.tsx` makineye bağlı bir saat beklentisi taşıyordu (UTC'de kırılırdı); anlık zamana bağlandı (plan: kapsam eki).

## Notlar
- `cashier-app.js` bekletme saati testi `customer-accounts-flows.test.js` içinde (aynı "İstanbul saati" kuralı; ayrı dosya planda yoktu).
- Boş sipariş: ürün eklenemezse açılan boş sipariş etkin sipariş olarak görünür kalır, sonraki dokunuş onu kullanır; sunucu tarafı temizlik G5'te.

## Bağımsız incelemeler (qa-frontend, qa-ux-tasarim)
Giderilen: tahsil yolunda 401 oturum kaybına düşmüyordu (test + mutant öldü); 409'da çift yenileme; zaman dilimi testleri artık süreç saat dilimini UTC'ye sabitler (İstanbul makinede boşa geçmez); 401 işleyicisi her testten sonra temizlenir; geri gönder düğmesinin erişilebilir adı görünür metni içerir; cari hesapta limit kaydederken 401 girişe döner; genel hata cümlesi ne yapılacağını söyler; yetkisiz oturumda limit formu yerine açıklama satırı.
Kabul edilen: geri gönder onaysızdır (garson yeniden gönderebilir, işlem geri alınabilir); gönderilmiş siparişten sonra yeni satış ipucu bildirimdedir; başlatılan sipariş + ekleme + yenileme üçü birden düşerse sunucuda boş sipariş kalabilir (temizlik V1-RMD-802/G5); vade alanı doğrulamasını sunucu yapar (Türkçe VALIDATION_FAILED metni).
