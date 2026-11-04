# V1-RMD-811 - Sonuç

Müşteri ekranı eşleştirmesi sunucu süresine göre işler, hız sınırında geri sayımla bekler ve ekranda hiçbir ham sunucu metni göstermez.

## Kök nedenler (kodda doğrulandı)

- Kod yenileme zamanı ve dakika etiketi istemci saatinden (`expiresAt - Date.now()`) hesaplanıyordu; saati ileri kayık ekran her kodu hemen yeniliyordu.
- 429 yanıtında ekran sunucunun cümlesini basıyor, kendiliğinden yeniden denemiyor, `Retry-After` okunmuyordu; durum sorgusu ve "onaylandı mı" sorgusu beklemeden vuruyordu.
- Hata ekranı ham `error.message` basıyordu.

## Değişiklik

- Sunucu: eşleştirme yanıtı `ExpiresInSeconds` taşır (sunucu sabitinden, 120).
- `api.ts`: `ApiError.retryAfterSeconds` (`Retry-After`, 1..300 sn; 0, negatif, sayı olmayan ve boş değer yok sayılır).
- `CustomerDisplay.tsx`: kod ömrü yanıttaki saniyeden (alan yoksa veya geçersizse 120); dakika etiketi aynı değerden; sabit Türkçe cümleler (ağ yok, sunucu hatası, hız sınırı, genel); 429'da "N saniye sonra kendiliğinden yeniden denenecek" geri sayımı (başlık yoksa 5 sn); beklemede durum yoklaması ve onay sorgusu durur; hata ekranı 1-2-4-8-15 sn geri çekilmeyle kendini yeniler; "Tekrar dene" başarısız olan isteği (kod ya da ekran verisi) yeniden dener; değiştirilmiş koda geç gelen hata yeni kod başlatmaz.

## Doğrulama (gerçek Postgres, port 55432; çıktı dizini `artifacts/g6`)

- Kırmızı: `vitest-red.log` (yeni testler uygulamadan önce başarısız).
- Yeşil: PosTerminal vitest 88 dosya / 891 test, tsc 0, lint 0 hata; Host `DualScreenStoreTests` 31/31 (`green2/`).
- Mutasyon (hepsi `cmp` ile geri alındı, `mut/*.log`, `mut/w2.result`): istemci saatine dönüş, başlığı yok sayma, ham mesaj, geri sayım metni, dakika etiketi, başlıksız 429, bekleme yok sayan yoklama, `/complete` bekleme, "Tekrar dene" hedefi, varsayılan ömür, üst sınır, `active` bayrağı (v1-v13, b1); ikinci tur w1-w8: `>= 0`, varsayılan 0 sn, `floor`, sayaç sıfırlanmaması, geri sayımın her hatada görünmesi, geçersiz ömrü kabul, geri çekilme çarpanı: hepsi test kırdı; üçüncü tur x1-x5: hub yeniden bağlanma, çevrimiçi olma, hub bildirimi ve hazır ekran başlıksız 429 beklemesini yok sayma, geri çekilme üst sınırı: hepsi test kırdı. w7 (başarı dalında `setPairing(null)`) sağ kaldı; çünkü satır gereksizdi, kaldırıldı.
- Atlanan test yok. Sınır: sahte zamanlayıcı `Date`'i de sahteler; saat kayması, yanıttaki `expiresAt`'in 8 dakika geriye alınmasıyla simüle edilir (gerçek saat kayması tarayıcıda denenmedi).

## İncelemeler

- qa-frontend: 429 sırasında yoklama durmuyordu, ömür alanı yoksa NaN zamanlayıcı döngüsü, bayat tamamlama yanıtı, "Tekrar dene" hedefi, `Retry-After` üst sınırı (hepsi düzeltildi ve testlendi).
- qa-test-kalitesi: başarılı eşleştirme testi, `Retry-After` sınır değerleri, başlıksız 429, ömür sınırları, geri çekilme/sıfırlanma, geri sayımın yalnız 429'a özgü olması, `status 0` ve snapshot hedefli "Tekrar dene", JSON alan adı sözleşmesi (hepsi eklendi); `api.ts` içinde yanlış yerde duran yorum taşındı.
- Doğrulama turu (qa-bulgu-dogrulayici): 9 maddeden 7 kapandı; kalan ikisi giderildi (hazır ekranda başlıksız 429 testi, geri çekilme testinin adı ve 4 sn adımı; JSON alan adı testi ASP.NET Web varsayılanlarıyla yapılır, Host'ta özel adlandırma ayarı yoktur). Yeni bulgu: hub yeniden bağlanması ve çevrimiçi olma bekleme süresini yok sayıyordu; tek yardımcıya (`refreshUnlessPaused`) bağlandı ve test eklendi, mutantlarla kanıtlandı.
