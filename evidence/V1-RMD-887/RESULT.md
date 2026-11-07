# V1-RMD-887 sonucu

Bilgi fişinin "orijinal basıldı" durumu artık sunucuda tutulur. İlk basım orijinaldir; sonraki her basım kopyadır ve kayıtlıdır; eski `GET .../receipt` ucu da bu duruma bağlandı.

## Davranış (ürün kararı, Semih 2026-11-07)
- Yeni `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/receipt/print` (ödenmiş hesap, `payments.take`): tek işlemde ilk basımı atomik talep eder. Kazanan çağrı orijinaldir (`isCopy=false`, denetim `bill.receipt.printed`); diğer her çağrı kopyadır (`isCopy=true`, denetim `bill.receipt.reprinted`). Kim, hangi terminal, hangi hesap numarası denetim kaydındadır. Aynı anda gelen birden çok basımdan yalnız biri orijinal olur (6 paralel istek testi: 1 orijinal, 5 kopya).
- Durum `billing.bills.receipt_first_printed_at/_by` sütunlarındadır (migration 223, ileri/geri testli); sayfa yenilense ya da başka terminalden açılsa da korunur.
- Eski `POST .../receipt/reprint` kaldırıldı (print ile yer değiştirdi). `GET .../receipt` salt okunur kalır (iade ekranı kalem verisini buradan okur), ama yanıtı `originalPrintedAt` ve `isCopy` taşır: orijinal basıldıktan sonra bu yanıttan fiş çizen her istemci KOPYA yazar.
- Kasiyer sayfası her basımda print ucuna gider ve KOPYA'yı sunucudan gelen `isCopy` ile çizer; bellekteki durum ve "Fişi yeniden yazdır" etiketi kalktı (düğme her zaman "Fişi yazdır").
- Hata metni: ödenmemiş hesap için "Yalnız ödenmiş hesabın fişi yazdırılabilir." (409 BILL_NOT_PAID); ödenmemiş hesapta hiçbir durum/denetim yazılmaz.

## Kapsam dışı ve açık riskler
- Mali fiş / e-Arşiv değil; belge mali olmayan bilgi fişidir. Tarayıcı yazdırma penceresi açıldığında orijinalin gerçekten kâğıda çıktığı doğrulanamaz: pencere iptal edilirse orijinal hakkı sunucuda harcanmış sayılır, sonraki basımlar KOPYA olur. Yönetici ile orijinali sıfırlama yolu yoktur (ürün kararı gerekir).
- Önceden ödenmiş eski hesaplarda durum boştur: ilk basım orijinal sayılır, fiziksel olarak daha önce basılmış olsa bile.
- Ödenmemiş hesabın fişi bu uçla basılmaz (eski GET ile hesap özeti hâlâ okunabilir); bu görevde değişmedi.
- Kırmızı koşu: print ucu yokken 3 yeni test başarısız (red-1.log).

## Kanıt
- PaymentTender projesi 112/112 (final-paymenttender.log; son kilit/test değişikliğinden sonra fiş testleri 33/33, final-receipt.log), ApiConventions 17/17 (final-arch.log), migration manifest ve 223 şema testleri 22/22 (final-migration.log), kasiyer sayfası split-payment 154/154 (final-js.log).
- Mutasyon (mutations.txt): 9 mutant, hepsi öldü (satır kilidi, ödenmiş kontrolü, kopya dalı, olay adları, kopya bayrağı, GET durumu, işlem commit, yanıtta orijinal zamanı, istemcinin sunucu bayrağını yok sayması). Her biri cp+cmp ile geri alındı ve yeniden derlendi.

## İnceleme sonrası notlar
- Fiş verisi orijinal talep edilmeden ÖNCE okunur (okuma hatası orijinali harcamaz). Talep ile yanıt arasında bağlantı kopar ya da istemci 15 sn zaman aşımına uğrarsa orijinal harcanmış kalır; tekrar deneme KOPYA basar ve geri alma yolu yoktur (ürün kararı: yönetici sıfırlaması ya da "yazdırıldı" onaylı iki adımlı akış). İlk çağrının proxy ile yeniden denenmesi de aynı sonucu verir; idempotency_allowlist gerekçesine yazıldı.
- Ödenmiş-hesap kontrolü, talep ve denetim kaydı aynı işlemde, hesap satırı kilidi (FOR NO KEY UPDATE) altında yapılır; eksik ya da ödenmemiş hesapta hiçbir şey yazılmaz (TheClaimChecksThePaidStatusUnderTheRowLock...). Uç ayrıca en başta ön kontrol yapar.
- GET .../receipt, orijinal basılmadan önce `isCopy=false` döner; bugün GET'ten fiş çizen istemci yok (iade ekranı yalnız kalem verisi okur).
- Denetim yazımı hata verirse talep geri alınır: veritabanı tetikleyicisiyle denetim yazımı reddedilerek test edildi (AFailedAuditWriteTakesTheOriginalClaimWithIt...): baskı 500 döner, hesap işaretlenmez, sonraki basım yine orijinaldir.
- Düğmenin yanına "ilk basım orijinaldir" uyarısı eklemek ürün/arayüz kararıdır, yapılmadı.
