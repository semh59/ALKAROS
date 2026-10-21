# qa-sinir-mimari — ham rapor özeti (SHA eb93ec69; kod okuması; 74/74 envanter, ayrıntılı okuma kısmi)

- SM-B1 Orta: SettingsManagementEndpoints.cs:82, SettingValidator.cs:64 — PUT yalnız veri tipini doğruluyor; QR zaman aşımı "00:00:00"/negatif kaydedilebilir, QrOrderExpiryHostedService.cs:112 bekleyen tüm QR siparişlerini reddeder; waiter.max_active_tables ve kitchen.dense_mode_threshold için de aralık yok. (DB-B2 ile aynı kök.)
- SM-B2 Orta: PostgresSettingsRepository.cs:255-318, 381-436 — pasif ayara PUT yazılıyor, çifte pasifleştirme ek geçmiş satırı yazıyor; GetValueAsync pasifi yok sayar. Testte yok.
- SM-B3 Orta: SettingsModule.cs:15 (Transient) ve Host'ta ≥7 yerde (Scoped/Singleton) aynı ISettingsService/Repository/Validator tekrar kaydı; TryAdd olduğundan ömür kayıt sırasına bağlı, captive bağımlılık riski. Etkin ömür çalıştırılarak doğrulanmadı.
- SM-B4 Orta: BusinessLogoStore.cs:41-69 — logo kaydı/silmesi geçmiş/denetim yazmıyor, changed_by yok (DB-B8, GY-B2 ile aynı kök).
- SM-B5 Düşük: ModuleOwner serbest metin ("waiter","qr_ordering","tables"...), modül kimlikleriyle tutarsız, hiçbir kayıtla doğrulanmıyor (Doküman sapması).
- SM-B6 Düşük: requires_restart saklanıyor/dönüyor ama hiçbir yerde uygulanmıyor (Doküman sapması).
- SM-B7 Düşük: 026 setting_history ON DELETE CASCADE + identity.users FK (DB-B5 ile aynı kök).
- SM-B8 Düşük: SettingsService.cs:100-103 tip uyumsuz kayıtlı değer sessizce varsayılana dönüyor, günlük yok.
- SM-B9 Düşük: invoice-settings istemci dosyaları Settings'e ait değil (dilim sınırı belirsiz).
Temiz: modül kaydı ve sınır testi, bağımlılık yönü, tablo erişimi tek dosyada, FOR UPDATE + row_version, yetki filtresi, olay/outbox yok.
Fırsat: tek DI kaydı + ömür testi; (anahtar, sahip, min/max) kayıt tabanlı katalog.
