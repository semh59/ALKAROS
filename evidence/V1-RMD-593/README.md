# V1-RMD-593 - Yazıcı formu alan bazlı mesajlar

`printerInputError` (models.ts) ad, istasyon, IP biçimi, özel ağ aralığı ve port aralığı için ayrı Türkçe mesaj döndürür; form sunucuya gitmeden bu mesajı gösterir. Sunucu kuralları değişmedi (sunucu cevabı hâlâ `printerErrorText` ile çevrilir).

- Testler: printerInputError.test.ts (parametrize) + 2 ekran testi; kitchen-operations 100/100, tsc temiz, oxlint uyarı sayısı sınır içinde (test-runs.log).
- Mutasyon: 12 mutant, ilk turda 2 hayatta kaldı (beş sayılı IP, boşluklu istasyon); testler sıkılaştırıldı, yeniden koşuda öldürüldü (mutations.log). Dosyalar mutasyon sonrası orijinalle aynı.
- Doğrulanamadı: yeni mesajlar Docker/tarayıcıda ayrıca denenmedi (ekran testi jsdom).
