# V1-RMD-841 sonuç

Kapatılan: `void-sent` ucu, `bills.void` yetkisi olmayan kullanıcı için önce yetki talebini açıyor, kalem doğrulamasını (bayat satır sürümü, mutfağa gönderilmemiş veya servis edilmiş kalem, kapanmış hesap) ancak `VoidAsync` içinde yapıyordu; geçersiz istek yönetici onay kuyruğunda hayalet talep bırakıyordu. `SentItemVoidStore` doğrulamayı tek özel yönteme (`LoadVoidableAsync`) çıkardı; `VoidAsync` aynı yöntemi kullanıyor (davranış aynı). Uç, yetkisiz dalda aynı anahtarla kayıtlı bir yetki yoksa yetki talebinden önce `EnsureVoidableAsync` çağırıyor; kayıtlı yetkisi olan tekrar gönderim eskisi gibi yetkiden ve saklanan sonuçtan cevaplanıyor. Hata kodları değişmedi.

## Doğrulama
- Kırmızı (`red.log`): dört senaryo da düştü (gönderilmemiş, servis edilmiş, bayat sürüm, kapanmış hesap).
- Yeşil (`green.log`): VoidSent HTTP projesi 35/35 (onaylanıp uygulanmış iptalin aynı anahtarla tekrar gönderimi testi dahil).
- Mutasyon (`mutations.log`): ön kontrol kaldırıldı (4 test düştü), servis edilmiş kalem denetimi kapatıldı (1), kapanmış hesap denetimi kapatıldı (2), ön kontrol tekrar gönderimde de çalışır hâle getirildi (1); dördü `cmp` ile geri doğrulandı. Kırık derlenen ilk mutant (`m1`) geçerli biçimiyle yeniden koşuldu.
- Atlanan test yok.

## İnceleme
qa-backend ilk turda şunu buldu (Yüksek): ön doğrulama tekrar gönderimde de çalışınca uygulanmış iptal aynı anahtarla yeniden gönderildiğinde 200 yerine 409 dönüyordu. Düzeltildi (anahtara kayıtlı yetki varsa ön doğrulama atlanır) ve testle sabitlendi.
İkinci inceleme (qa-test-kalitesi) bulgu bulmadı; düşük notlardan hata kodu doğrulaması senaryo başına teoriye eklendi (35/35 yeşil). Kabul edilen: onaylı tekrar gönderim testi satır sürümünü ve stok sayısını ayrıca doğrulamıyor (yetkili yol için var); ön kontrol artık sahiplik denetiminden (403) önce çalıştığı için yetkisiz garson başkasının kaleminin durumunu 409 kodundan çıkarabilir (yetki kontrolü grant servisinin içinde, ayrılamıyor); ön kontrol ile grant arasında atomiklik yok, onayda `VoidAsync` aynı hatayı verir.
Kardeş kusur: ikram ucundaki (V1-RMD-836) ön kontrol de aynı anahtarla uygulanmış iptalin tekrar gönderiminde, arada sipariş kapandıysa 409 döndürebilir; ayrı görevle ele alınacak.
