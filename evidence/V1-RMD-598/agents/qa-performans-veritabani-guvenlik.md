# Ham rapor özetleri (SHA eb93ec69; hepsi kod okuması, çalıştırılmadı)

## qa-performans-dayaniklilik (74/74, 24 dosya ayrıntılı)
- PD-B1 Yüksek: QrOrderingEndpoints.cs:134 herkese açık /branding yalnız HasLogo için logonun tüm byte'larını (5 MB'a kadar) okur (BusinessLogoStore.cs:66-71).
- PD-B2 Orta: QrOrderingEndpoints.cs:147-151 /logo önce içeriği okur, sonra ETag/304 karşılaştırır.
- PD-B3 Orta: SettingsService.cs:81-110, GarsonFeatureToggles.cs:155-162 ayar okumalarında önbellek yok; IsEnabledAsync 2 sorgu.
- PD-B4 Orta: GarsonFeatureToggles.cs:170-179 GetAllAsync "tek gidiş-dönüş" diyor ama özellik başına 2 sorgu (N+1).
- PD-B5 Orta: PostgresSettingsRepository.cs:178-188 RegisterSettingAsync SELECT sonra INSERT; yarışta ham PostgresException (23505) → ilk açılışta 500.
- PD-B6 Orta: PostgresSettingsRepository.cs:120-158 geçmiş sınırsız, 5000+ satırda InvalidOperationException, sayfalama yok.
- PD-B7 Orta: SettingRequests/SettingValidator değer ve reason uzunluk sınırı yok.
- PD-B8 Orta: BusinessIdentityLogoEndpoints.cs:100-113 yükleme MemoryStream + ToArray çift kopya.
- PD-B9 Düşük: logo PUT'u yönetici filtresinden önce 6 MB form gövdesi okutur, hız sınırı yok.
- PD-B10 Düşük: komutlarda CommandTimeout/yeniden deneme yok.
Temiz: güncelleme/pasifleştirme FOR UPDATE + row_version atomik; indeksler; hosted service döngüsü.

## qa-veritabani (58/74)
- DB-B1 Orta: RegisterSettingAsync yarışı (PD-B5 ile aynı kök).
- DB-B2 Orta: PostgresSettingsRepository.cs:269-314 UpdateSettingAsync active denetlemiyor, anahtara özel aralık/uzunluk yok; negatif değer waiter.max_active_tables'a yazılabilir.
- DB-B3 Orta: /branding ve /logo 5 MB okuma (PD-B1/B2 ile aynı kök).
- DB-B4 Orta: geçmiş sayfalama/saklama yok (PD-B6 ile aynı kök).
- DB-B5 Orta: 026 setting_history ON DELETE CASCADE, salt-ekleme koruması yok; belge "never physically deleted" diyor (Doküman sapması).
- DB-B6 Düşük: idx_settings_scope, idx_settings_active, idx_setting_history_changed_at kullanılmıyor.
- DB-B7 Düşük: 136 CREATE TABLE IF NOT EXISTS yok, content_type/boyut CHECK yok, row_version INTEGER vs BIGINT, updated_at DEFAULT yok.
- DB-B8 Düşük: logo yükleme/silme geçmişe/denetime yazılmıyor.
- DB-B9 Düşük: 136 down için test yok.
- DB-B10 Düşük: ORDER BY changed_at DESC ikincil sıralama yok.
Fırsat: 7 ayar sınıfında tekrar eden get-or-register kalıbı ortak yardımcıya.

## qa-guvenlik-yetki (74/74, testler taranarak)
- GY-B1 Orta: settings.manage için ApplicationPermissions sabiti yok; iki uç düz string kullanıyor.
- GY-B2 Orta: logo yükleme/silme aktör kaydı yok (DB-B8 ile aynı kök).
- GY-B3 Düşük: pasif ayar güncellenebiliyor (DB-B2 ile aynı kök).
- GY-B4 Düşük: sır yasağı yalnız anahtar adında ve yalnız kayıtta; değer içeriği taranmıyor, güncellemede ValidateKey yok.
- GY-B5 Düşük: Reason/NewValue uzunluk sınırı yok (PD-B7 ile aynı kök).
- GY-B6 Düşük: logo doğrulaması yalnız sihirli bayt; yeniden kodlama yok.
Doğrulanamayan: alkaros.manager çerezi SameSite/HttpOnly ve logo PUT/DELETE CSRF (DisableAntiforgery) durumu; CSP/nosniff'in logo yanıtına uygulandığı çalıştırılarak görülmedi.
Temiz: yetki filtresi, parametreli SQL, tür beyaz listesi, KVKK.
