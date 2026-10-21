# qa-api-uclar — ham rapor özeti (SHA eb93ec69; kod okuması; 74/74 envanter, ~35 dosya ayrıntılı)

- API-B1 Orta: WaiterMaxActiveTablesSetting.cs:15, SuggestedWaiterResolver.cs:112 — waiter.max_active_tables yalnız "önerilen garson" sorgusunu süzüyor; masa açma akışında sınır denetimi ve Türkçe uyarı YOK. Semih senaryosu karşılanmıyor (grep: GetLimitAsync tek tüketici Resolver).
- API-B2 Orta: SettingValidator.cs:74-83,122-131 — aralık yok; WholeNumber negatif; Duration "5" = 5 gün, "-00:01:00" negatif; negatif QR süresi bekleyen siparişleri hemen iptal eder (QrOrderExpiryHostedService.cs:72).
- API-B3 Orta: business.accent_theme palet anahtarı yazımda doğrulanmıyor ("kaydedildi ama uygulanmadı"); business.name uzunluk sınırı yok.
- API-B4 Orta: geçmişte değiştiren kimlik parçası, API yalnız ChangedBy GUID dönüyor (SettingsManagementContracts.cs:27).
- API-B5 Düşük: PostgresSettingsRepository.cs:466,477,222 — "[Deactivated]" / "Setting deactivated" İngilizce geçmişe yazılıyor, UI çevirmiyor.
- API-B6 Orta: logo yükleme/silme geçmiş/denetim yazmıyor (diğerleriyle aynı kök).
- API-B7 Düşük: logo doğrulaması yalnız imza+boyut; çözümleme/en-boy sınırı yok.
- API-B8 Düşük: settings.manage ApplicationPermissions'ta sabit değil, iki uçta tekrar.
- API-B9 Düşük: Reason/NewValue/Key uzunluk sınırı yok; etkin olmayan ayara yazma engellenmiyor; yeniden etkinleştirme ucu yok.
- API-B10 Düşük: sır yasağı yalnız anahtar adında; HTTP kayıt ucu olmadığından bugün tetiklenmiyor.
- API-B11 Düşük: setting_history CASCADE, append-only zorlaması yok.
- API-B12 Düşük: Host testlerinde boşluk: DELETE 409/401/403, moduleOwner süzmesi, logo DELETE 401/403, geçersiz NewValue biçimleri.
Varsayılanlar kodla doğrulandı: waiter.max_active_tables=0 (sınırsız), kitchen.dense_mode_threshold=9, kitchen.live_sync_enabled=false, qr süre=5 dk, reservations.dedicated_station_enabled=false, business.name="", accent=palet varsayılanı; garson özellik anahtarları varsayılan açık.
Temiz: yetki filtresi, Türkçe hata zarfı, row_version+FOR UPDATE, parametreli SQL, logo tür/boyut/SVG yok, genel uçlar hız sınırlı.
Doğrulanamayan: çerez SameSite/CSRF, business.name'in QR sayfasında kaçışlanması, ApiConventions kaydı.
