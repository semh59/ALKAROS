# V1-RMD-878 - Kapanmış hesabın fişini yeniden yazdırma (F0052)

## Sonuç
Kasa ekranında ödenmiş (kapanmış) hesapta "Fişi yeniden yazdır" eylemi var. Her yeniden yazdırma denetim izine yazılır (kim, ne zaman, hangi hesap ve terminal: `bill.receipt.reprinted`) ve fişin üstünde "KOPYA" yazar. Yeni yazıcı mimarisi yok: mevcut bilgi fişi okuması ve tarayıcı yazdırma akışı kullanıldı; tek yeni parça `POST .../billing/bills/{id}/receipt/reprint`.
- Ödeme anındaki ilk yazdırma eskisi gibi ("Fişi yazdır", KOPYA yok, kayıtsız); ondan sonraki her yazdırma ve kapanmış hesabın sonradan açılması "Fişi yeniden yazdır" + KOPYA + kayıt.
- Yetki: mevcut `payments.take` (fişi okumakla aynı); yeni izin/migration yok. Yalnız Paid hesap (aksi 409 "Yalnız ödenmiş hesabın fişi yeniden yazdırılabilir."). Kayıt, veri verilmeden önce yazılır.

## Karar boşlukları (benim belirlediğim)
1. Yeniden yazdırma sayısı sınırı ve yönetici onayı yok; her çağrı bir kayıt.
2. KOPYA işareti istemcide; sunucu "orijinal basıldı" durumu tutmaz (migration gerektirirdi).
3. Tekrar anahtarı yok: her çağrı bilerek ayrı kayıt (idempotency_allowlist.json'a gerekçeyle girdi).
4. Sahipsiz hesap yeniden yazdırmayla terminale sahiplendirilmez (qa bulgusu, `BillRouteDoesNotClaim`).

## Kanıt
- Gerçek Postgres: PaymentTender projesi 103/103 (green); sonrasında BillReceipt+TerminalAccess 27/27 (green2); ApiConventions 17/17 (green3). Kırmızı koşu kaynak düzenlemesiyle yarıştığı için gerçek kırmızı değil; kanıt mutasyonlardır.
- StaticApps vitest 409/409 (split-payment-receipt.test.js 13 test).
- Mutasyon (mutations.txt): J1-J7 ekran/CSS, M1b-M7 sunucu; hepsi öldü (M1 geçersiz derleme hatasıydı, M1b ile değiştirildi).

## Açık risk / ürün kararı
- `GET .../receipt` kayıtsız kalır (iade ekranı kalem verisi için kullanıyor); payments.take sahibi bunu doğrudan çağırıp kayıtsız ve işaretsiz çıktı alabilir. Kesin çözüm sunucuda "orijinal basıldı" durumu ister; Semih: istenir mi?
- Ödemeden sonra sayfa yenilenirse ilk fiş hakkı kaybolur; ilk basım KOPYA ve kayıtlı olur (güvenli yön).
- Kayıtta kopya sıra numarası ve gerekçe yok; hız sınırı genel terminal-write (120/dk).
