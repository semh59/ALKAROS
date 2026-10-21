# qa-dokuman-bagimlilik — ham rapor özeti (SHA eb93ec69; kod okuması; 74/74 envanter, ~30 test/istemci dosyası yalnız grep)

- DB-D1 Düşük (Doküman sapması): settings-ownership.md:21 "settings registry" — kodda registry yok, sahip her ayar sınıfındaki ModuleOwner sabiti.
- DB-D2 Düşük (Doküman sapması): settings-ownership.md:26 restart kuralı — RequiresRestart yalnız saklanıyor, hiçbir ayar true kaydedilmiyor, tüketen kod yok.
- DB-D3 Düşük (Doküman sapması): settings-ownership.md:24,27 "history silinmez" — 026'da setting_id ON DELETE CASCADE, changed_by SET NULL.
- DB-D4 Düşük: 6 test projesinde kullanılmayan doğrudan Npgsql PackageReference (TestHelpers zaten taşıyor).
- DB-D5 Düşük: plan/v1/settings SET-002..006 için evidence dizini yok (tarihsel olabilir; doğrulanamadı).
- DB-D6 Düşük: GarsonFeatureToggles.cs ve QrOrderExpirySetting.cs için tests/Modules/Settings altında doğrudan test yok (dolaylı kapsam doğrulanamadı).
- DB-D7 Düşük (bilinen V1-RMD-596): geçişli paket lisansları makine-okur doğrulanmadı.
Temiz: scope enum belgeyle uyumlu, paket kilidi tutarlı, 9 plan dosyası Done, atıflar çözümleniyor.
