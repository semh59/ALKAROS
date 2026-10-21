# qa-backend — ham rapor özeti (SHA eb93ec69; kod okuması; 74/74)

Semih senaryosu geçmiyor (BE-B1, BE-B2).
- BE-B1 Yüksek: SuggestedWaiterResolver.cs:112 — waiter.max_active_tables yalnız "önerilen garson" SQL'inde süzgeç; OrderManagementEndpoints.cs:289-304 elle masa açarken ve kendi adına açarken sınıra bakmıyor. Başka tüketici yok. 4. masa uyarısız açılır.
- BE-B2 Yüksek: WaiterMaxActiveTablesSetting.cs:57, SettingsManagementEndpoints.cs:53 — ayar ilk okunana kadar kayıtsız (listede yok, PUT 404); düzenleme ekranı da yok.
- BE-B3 Orta: PostgresSettingsRepository.cs:179-188 — kayıt SELECT sonra INSERT; yarışta ham 23505.
- BE-B4 Orta: PostgresSettingsRepository.cs:296-304,436-445 — pasif ayara PUT başarılı, ayar pasif kalır; yeniden etkinleştirme yok.
- BE-B5 Orta: SettingValidator.cs:90-113 — aralık yok; negatif sınır hiçbir garsonu uygun bırakmaz; QR süresi ≤0 bekleyen tüm QR siparişlerini reddeder; Duration "5"=5 gün (çalıştırılmadı); Int32 taşması sessizce varsayılana (SettingsService.cs:62-70).
- BE-B6 Orta: business.name/accent_theme doğrulaması yok.
- BE-B7 Orta: GarsonFeatureToggles ve QrOrderExpirySetting için hiçbir testte başvuru yok (tests/ grep 0).
- BE-B8 Orta: 026 setting_history CASCADE, append-only yok, changed_by SET NULL.
- BE-B9 Orta: geçmişte kimlik parçası (SettingsHistorySection.tsx:19).
- BE-B10 Orta: logo değişimi geçmiş/denetime yazılmıyor (BusinessIdentityLogoEndpoints.cs:113).
- BE-B11 Düşük: sır yasağı yalnız anahtar adı ve yalnız api_key/apikey yazımı; api-key, x-api-key, pwd, passwd, passphrase geçer (SettingValidator.cs:7-23).
- BE-B12 Düşük: BusinessIdentitySettings.tsx:133-134 rowVersion kayıttan hemen önce okunuyor.
- BE-B13 Düşük: geçmiş sıralamasında ikinci anahtar yok (PostgresSettingsRepository.cs:131).
- BE-B14 Düşük: logo içerik türü büyük/küçük harfe duyarlı, "image/png; charset" reddedilir (BusinessIdentityLogoEndpoints.cs:95).
- BE-B15 Düşük: ISettingsService dört yerde farklı ömürle kayıtlı.
Doküman sapması: RequiresRestart saklanıyor, değiştirme akışında uyarı yok.
Doğru varsayılanlar: waiter.max_active_tables=0, kitchen.dense_mode_threshold=9, kitchen.live_sync_enabled=false, qr süre=5 dk, reservations.dedicated_station_enabled=false, business.name="", accent=lacivert, garson özellik anahtarları=true.
Temiz: iyimser kilit çift kontrol, tek işlem, parametreli SQL, 5000 koruması, logo tür+imza+5 MB, settings.manage kapısı, Türkçe hata eşleme.
Fırsat: tek ayar kataloğu (anahtar, tür, aralık, varsayılan, etiket); ayar düzenleme ekranı; garson sınırı aşımında Türkçe uyarı + yönetici geçişi.
