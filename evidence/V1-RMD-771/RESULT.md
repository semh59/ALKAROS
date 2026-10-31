# V1-RMD-771 sonuç

Kusur: `AvailabilityNotDelivered` vaka anahtarı `desired_version` içeriyordu; yayıncı her turda sürümü adet aynı olsa da ilerlettiği için süren bir fark her turda yeni vaka açıyor, eskisini otomatik çözüyordu. Düzeltme: anahtar `desired_at` (yalnız adet değişince ilerler) mikrosaniyesini kullanır.

- Kırmızı: red/test.log — yayıncıyla 4 tur sonunda 1 yerine 4 vaka.
- Yeşil: green/ (Reconciliation.OnlineOrders 41/41), green-avail/ (AvailabilityPublishing 14/14), host-recon/ (Host Reconciliation 28/28).
- Mutasyon: anahtarı `desired_version`'a döndürmek 4 testi kırdı (yeni test dahil); kaynak `cmp` ile geri alındı, final-recon/ 41/41.
- Eski üç testin sabit `:1`/`:2` anahtarları `desired_at` tabanlı yardımcıya çevrildi (anahtar tanımı değişti).
- Sınır: yayıncı kodu değişmedi; üretimdeki tam tur (stok bakiyesi okuması) bu testte yoktur, testte yayıncının gerçek `RecordObservationAsync` ifadesi kullanıldı (RefreshAsync'in her ürün için çağırdığı aynı ifade).
- Geçiş: canlı veri yok (V12-RMD-009 kanıtı: V0-YSP-001 Blocked). Eski biçimli açık vaka olsaydı kaynakta görünmeyip bir kez çözülür ve yeni anahtarla bir kez yeniden açılırdı; zararsız, ayrı geçiş yazılmadı.
- Bağımlılık: anahtar, yayıncının adet aynıyken `desired_at`'i korumasına dayanır; bu davranışı yeni test (4 tur → 1 vaka) korur. Yayıncı bu görevin dışında olduğundan kodu değişmedi.
- İnceleme 1 (kod) ve 2 (test): kusur çözümü doğru; düşük notlar (boş satır eksikti: düzeltildi; helper anahtar biçimini kaynaktan kopyalar: davranış testi yeterli bulundu).
