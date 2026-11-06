# V1-RMD-843 sonuç

Kapatılan (F0897, birinci yarı): hiçbir `billId` ucu hesabı terminalle eşleştirmiyordu. Artık `billing.bills.terminal_id` (migration 219, boş olabilir) hesabın sahibi terminali tutuyor. Sahibi olmayan terminal `BILL_OTHER_TERMINAL` (403, Türkçe metin) alıyor; `cash.session.override` sahibi (yönetici, süpervizör) geçiyor, hiçbir hesabı sahiplenmiyor.

Sahiplik kuralı: yazan istek (POST/PUT) sahipsiz hesabı tek atomik `UPDATE ... WHERE terminal_id IS NULL` ile kendi terminaline alır (uç yetki denetiminden önce, çünkü dışlamanın atomik olması gerekiyor); okuma (GET) hiçbir zaman sahiplenmez, yalnız başkasının hesabını reddeder. `from-order` açılışta hesabı sahiplenir ve var olan başka terminal hesabını döndürmez. Eski satırlar ve yöneticinin açtığı hesaplar ilk yazan kasaya geçer.

Kapsanan Billing uçları: `from-order`, iptal, indirim, bahşiş, düzeltme geri alma/okuma, iade isteği, iade listesi, iade tamamlama, bölme tasarımı 7 uç. İade listesi hesapla çağrılınca hesap denetlenir; hesapsız çağrıda (Cashier ödeme ekranı böyle çağırıyor) yetkisiz kullanıcıya yalnız terminalin işleyebileceği hesapların istekleri döner (tamamlama ile aynı ölçüt). Süzme sayfalamadan sonra uygulanıyor; bu kısıt eskiden de vardı (istemci 200 satırdan sonra süzüyordu), kötüleşmedi. İade onay/ret uçları yönetici işi (`payments.refund.approve`, varsayılan rollerde override ile aynı kümede) olduğundan süzülmedi; `refund-allowance` hesap taşımıyor.

Ödeme alma, makbuz, cari hesap, nakit tahsilat ve ekranda gösterme uçları V1-RMD-844'te. Eski testlerde güncelleme gerekmedi.

## Doğrulama
- Kırmızı (`red.log`): tüm denetimler kapalıyken yeni testlerin çoğu düşüyor (17/20 o günkü sayıyla); `red-migration.log`: boş migration ile sütun testi düşüyor.
- Yeşil: Billing HTTP projesi 233/233 (`green-billing.log`, yeni 23 test dahil), migration/manifest 58/58 (`green-migration.log`; ileri, geri, yeniden uygulama dahil), mimari sınır 31/31, API sözleşmeleri 17/17, hata sözlüğü 225/225, BillFoundation projesi 50/50 (`green-billfoundation.log`), gerçek host (`DualScreenApplication.Build`, `ValidateScopes` açık) üzerinde PaymentTender örneği 9/9 (`green-host-boot.log`; `IRoleRepository` kaydı sorunsuz çözülüyor). Depo testleri (atomik sahiplenme, 8 eşzamanlı istek) ayrı dosyada değil `tests/Host/Experience/Billing/BillTerminalAccessHttpTests.cs` içinde.
- Mutasyon (`mutations.log`): üç grup filtresi tek tek kaldırıldı, `from-order`, iade tamamlama, iade listesi hesap denetimi, iade listesi süzgecinin ters çevrilmesi, "başka terminal her zaman geçer", yetkili geçişi, sahiplik çalma, yetkili kullanıcı sahiplenir, okuma sahiplenir; hepsi testleri düşürdü ve `cmp` ile geri doğrulandı. (Yeni, izlenmeyen dosyalardaki mutasyonlarda `git diff` boş çıktığı için görülen "NO MUTATION" uyarıları silindi; düşen test sayıları mutasyonun uygulandığını gösteriyor.)
- Kapsam aracı exit 0 (`scope.log`).

## Koordinatöre karar notları
- Sahiplik terminal bazlı (kasa oturumu bazlı değil): açık hesap vardiya değişiminde de yaşar. Başka terminal açık hesabı alamaz; yalnız yönetici/süpervizör devralabilir.
- "Yönetici" = `cash.session.override` sahipleri (süpervizör dahil). Yeni izin kodu eklenmedi.
- Başka terminalin hesabı için 404 değil 403 döner: kasiyer kuyrukta gördüğü hesabın neden açılmadığını anlasın (olmayan hesap 404, GUID tahminiyle varlık sızıntısı düşük risk).
- Yetkisi olmasa da, sahipsiz hesaba yazmayı deneyen kasa hesabı sahiplenebilir (atomik dışlama için kabul edildi); sahipliği devretme/bırakma ucu yok, devralma yöneticiyle.
- İşletme geneli kuyruk ve gün sonu uyarısı, `orders/active` içindeki ödeme ilerlemesi süzülmedi.

## İnceleme
- qa-guvenlik-yetki: Yüksek (yetkili kullanıcı okumayla sahiplenebiliyordu) ve Orta (okuma sahiplenmesi) düzeltildi; mutasyonlar m11 ve m12.
- qa-backend: iade listesinde LIMIT sonrası süzme ve liste/tamamlama ölçüt farkı. Ölçüt tamamlama ile aynı yapıldı; LIMIT sonrası süzme kabul edildi (yukarıda).
- Doğrulama turu: Cashier ödeme ekranı (`split-payment.js:1229`) iade listesini hesapsız çağırıyor; ilk düzeltmem (hesapsız listeyi kasiyere kapatmak) bu ekranı kırardı, geri alındı. Son halin ikinci doğrulaması aşağıda.
