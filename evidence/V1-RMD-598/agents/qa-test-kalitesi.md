# qa-test-kalitesi — ham rapor özeti (SHA eb93ec69; mutasyon git worktree kopyasında, worktree silindi)

Mutasyon: 19 mutant (M1-M20, M12/M20 masa sınırı). Ajanın sayıları tutarsız (özette "11/19 yaşadı", tabloda 6 öldü/13 yaşadı); ana oturum bunu yeniden saymaz, doğrulayıcı bulgular tek tek sınanır. Tablo: doğrulayıcı 6 mutant (3 öldü), depo/servis 5 (1 öldü), masa sınırı 2 (2 öldü), logo deposu 1 (0), logo/yönetim uçları 5 (0).

- TK-B1 Orta: logo imzası — PNG son bayt, WebP "P", JPEG 3. bayt silinince test geçti (M14-M16).
- TK-B2 Orta: logo boyut sınırı `>` yerine `>=` geçti (M17); yalnız 5 MB+1 deneniyor (BusinessIdentityLogoEndpointsTests.cs:115).
- TK-B3 Orta: PUT'ta actorId yerine null geçti (M18); HTTP testi ChangedBy'ı denetlemiyor (SettingsManagementHttpTests.cs:97).
- TK-B4 Orta: moduleOwner süzgeci ters çevrilince geçti (M7); GetByModuleOwner ve GET `/` moduleOwner süzgeci hiçbir testte yok.
- TK-B5 Düşük: GetAllActive sıralaması denetlenmiyor (M11).
- TK-B6 Orta: bozuk kayıtlı değerde varsayılana düşen catch devre dışı kalınca geçti (M10).
- TK-B7 Düşük: Duration ISO-8601 yolu (M4), apikey yasağı (M5), PreciseNumber NumberStyles.Any (M6) testsiz.
- TK-B8 Orta (etkisi sınırlı): FOR UPDATE kaldırılınca geçti (M9); eşzamanlı güncelleme testi yok.
- TK-B9 Düşük: logo row_version + 1 → + 0 geçti (M13).
- TK-B10 Orta: GarsonFeatureToggles testsiz. TK-B11 Düşük: QrOrderExpirySetting testsiz.
- TK-B12 Düşük: pasif ayara güncelleme davranışı kararsız/testsiz. TK-B13 Düşük: SettingsService TimeSpan dalı ölü kod.
- TK-B14 Orta: masa sınırında negatif değer reddi yok.
Skip'li test yok. İstemci testleri mutasyonla ölçülmedi.
