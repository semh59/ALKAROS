# qa-frontend — ham rapor özeti (SHA eb93ec69; kod okuması; 31/74, istemci rolü dışı dosyalar okunmadı)

- FE-B1 Orta: management/panel.tsx:49-50 — usePanel anahtar değişince "ready" kalıyor, iptal bayrağı yok; A'nın geçmişi B başlığı altında kalabilir.
- FE-B2 Orta: management-settings-history/models.ts:100 — "Setting deactivated" ham İngilizce ekrana çıkıyor (sunucu PostgresSettingsRepository.cs:477).
- FE-B3 Orta: models.ts:92-96 — Duration/WholeNumber biçimlenmiyor ("00:05:00", "9"), birim yok.
- FE-B4 Orta: models.ts:98 — formatWhen saat dilimi sabitlenmemiş (örnek: authorization-decisions/models.ts:92).
- FE-B5 Düşük: SettingsHistorySection.tsx:20 — Değiştiren sütunu kimlik parçası (UX-B1 ile aynı).
- FE-B6 Orta: BusinessIdentitySettings.tsx:132-150 — kayıttan hemen önce okunan rowVersion; eşzamanlı çakışma hiç görünmez (UX-B7).
- FE-B7 Orta: BusinessIdentitySettings.tsx:84-86,264-290 — yükleme hatasında kilitli, tekrar dene yok (UX-B4).
- FE-B8 Düşük: BusinessIdentitySettings.tsx:98-100 — ağ hatası "anonim" sayılıp giriş formu çıkıyor.
- FE-B9 Orta: BusinessIdentitySettings.tsx:347 — Kaldır onaysız (UX-B6).
- FE-B10 Düşük: styles.css:370 — .danger-text 44 px (<48).
- FE-B11 Düşük: BusinessIdentitySettings.tsx:354 — tek paylaşımlı hata sayfa altında; hata/başarı karışık.
- FE-B12 Orta: BusinessIdentitySettings.tsx:132-139,188,206 — 401'de onSessionExpired bağlı değil, giriş formu açılmıyor.
- FE-B13 Düşük: api.ts:379-388 — fetchBusinessLogo hatada null döner ("logo yok" gibi), zaman aşımı yok.
- FE-B14 Orta: BusinessIdentitySettings.test.tsx:255-262 — axe yalnız giriş formunda; hazır durum, SellerProfileCard, geçmiş ekranı taranmıyor; 401/403/409/yükleme hatası testleri yok.
- FE-B15 Orta: SellerProfileCard.tsx:20-26,70 — yükleme hatasında form boş ve düzenlenebilir; Kaydet mevcut fatura bilgisinin üstüne yazabilir.
- FE-B16 Düşük: SellerProfileCard.tsx:49-57 — sunucu 400 fields yok sayılıyor; aria-invalid/pattern yok.
- FE-B17 Düşük: sellerProfileApi.ts:36-38 — X-Idempotency-Key sunucuda kullanılmıyor (InvoiceSettingsEndpoints.cs:73).
- FE-B18 Orta: src/Clients/** — garson masa sınırı hiçbir istemcide gösterilmiyor; WaiterLoadV1'de sınır yok; Cashier yalnız "(N masa)" (UX-B3 ile aynı kök).
- FE-B19 Orta: yalnız ad ve renk düzenlenebiliyor; garson sınırı, mutfak eşiği, QR süresi, garson anahtarları, rezervasyon istasyonu, mutfak canlı eşitleme için ekran yok (UX-B2 ile aynı kök); test salt okunurluğu sabitliyor (SettingsHistorySection.test.tsx:86).
- FE-B20 Düşük: SettingsHistorySection.test.tsx:75-83 — etiket testi anahtar listesini elle yazıyor.
- FE-B21 Düşük: SellerProfileCard.tsx:62-64; BusinessIdentitySettings.tsx:283,313 — eski "kaydedildi" iletisi temizlenmiyor.
Temiz: ham HTTP kodu/İngilizce enum yok, nesne URL'leri serbest, çift tıklama busy ile engelli, yetki ön denetimi sunucuyla aynı.
Fırsat: ayarların tek ekranda toplanması; değişiklik sonrası "Geri al"; garson seçicide "sınır dolu" rozeti.
