# V1-RMD-876 sonuç

Nakit alınan para alanı yalnız Cashier vanilla split-payment ekranında var (PosTerminal React'te yok). Alanın altına 5/10/20/50/100/200 ₺ düğmeleri ve "Tam tutar" eklendi. Banknot düğmesi alanı o tutara AYARLAR (eklemez); Tam tutar ödenen tutarı (amountDraft, varsayılan kalan) yazar. Önizleme (Para üstü), Ödemeyi Ekle düğmesi ve idempotency anahtarı güncellenir; işlem sürerken düğmeler etkisizdir. Gerçek button öğeleri (klavye), .sp-btn (48px hedef), grup etiketi Türkçe.
Kırmızı: red-1.log (5 başarısız). Yeşil: green-1.log; StaticApps tamamı: static-full.log.
Mutasyon: 11 mutant, hepsi öldü (mutations.txt, cp+cmp geri alındı).
Sınır: 200 TL üstü hesapta banknot düğmeleri "tutardan az" gösterir, Tam tutar çalışır; telemetri kuralı React'e özgü, vanilla ekranda yok.
