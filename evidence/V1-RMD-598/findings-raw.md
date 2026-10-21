# V1-RMD-598 — birleştirilmiş bulgular (SHA eb93ec69)

Ham: 8 ajandan ~110 satır (UX 17, DY 10, DB 10, GY 6, SM 9, FE 21, DOK 7, API 12, BE 15; test kalitesi bekleniyor). Aşağıda kök nedene göre tekil liste. Çürütme için doğrulayıcıya bu liste verilir.

| Kök | Önem (ham) | İçerik | Ajan kimlikleri |
| --- | --- | --- | --- |
| R1 | Yüksek | `waiter.max_active_tables` yalnız önerilen-garson süzgeci (SuggestedWaiterResolver.cs:112); OrderManagementEndpoints.cs:289-304 masa açarken sınıra bakmıyor; Türkçe uyarı yok; hiçbir istemcide sınır görünmüyor | BE-B1, API-B1, UX-B3, FE-B18 |
| R2 | Yüksek | Ayar düzenleme ekranı yok (yalnız ad/renk/logo); sınır ayarı ilk okunana dek kayıtsız (listede yok, PUT 404) | BE-B2, UX-B2, FE-B19 |
| R3 | Yüksek (UX) / Orta | Geçmişte "Değiştiren" kimlik parçası; API yalnız GUID döner | UX-B1, FE-B5, API-B4, BE-B9 |
| R4 | Orta | Değer doğrulaması yalnız tür: aralık yok (negatif sınır, QR süresi ≤0 bekleyen siparişleri reddeder, Duration "5"=5 gün), accent_theme palet ve business.name uzunluk yok, Reason/NewValue uzunluk sınırı yok | SM-B1, DB-B2, API-B2, API-B3, API-B9, BE-B5, BE-B6, PD-B7, GY-B5 |
| R5 | Orta | Pasif ayara PUT başarılı, ayar pasif kalır; yeniden etkinleştirme yok; çifte pasifleştirme ek geçmiş satırı | SM-B2, DB-B2, GY-B3, BE-B4 |
| R6 | Orta | RegisterSettingAsync SELECT sonra INSERT; yarışta ham 23505, ilk açılışta 500 | PD-B5, DB-B1, BE-B3 |
| R7 | Yüksek (DY) / Orta | Herkese açık /branding yalnız HasLogo için 5 MB'a kadar logo baytı okur; /logo 304 öncesi tam okur | PD-B1, PD-B2, DB-B3 |
| R8 | Orta | Logo yükleme/silme geçmiş/denetim yazmıyor, aktör yok | DB-B8, GY-B2, SM-B4, API-B6, BE-B10 |
| R9 | Orta | setting_history ON DELETE CASCADE, append-only yok; geçmiş sayfalama/saklama yok (5000+ satırda 500); ikincil sıralama yok | DB-B5, SM-B7, API-B11, BE-B8, PD-B6, DB-B4, DB-B10, BE-B13 |
| R10 | Orta | Ayar okumalarında önbellek yok; IsEnabledAsync 2 sorgu; GetAllAsync N+1 | PD-B3, PD-B4 |
| R11 | Orta | ISettingsService/Repository/Validator Host'ta ≥7 yerde farklı ömürle kayıtlı | SM-B3, BE-B15 |
| R12a | Orta | İstemci: rowVersion kayıttan hemen önce okunuyor (iyimser kilit işlevsiz) | UX-B7, FE-B6, BE-B12 |
| R12b | Orta | İstemci: yükleme hatasında kilitli, tekrar dene yok; 401'de yeniden giriş yok; ağ hatası "anonim" | UX-B4, FE-B7, FE-B8, FE-B12, UX-B17 |
| R12c | Orta | İstemci: Kaldır onaysız | UX-B6, FE-B9 |
| R12d | Orta | İstemci: SellerProfileCard yükleme hatasında boş düzenlenebilir form (fatura bilgisi ezilebilir) | FE-B15 |
| R12e | Orta | İstemci: usePanel anahtar değişince eski geçmiş B başlığı altında kalabilir | FE-B1 |
| R12f | Orta/Düşük | İstemci: "Setting deactivated" İngilizce; Duration/WholeNumber birimsiz; saat dilimi sabit değil; "PosTerminal" gerekçe metni | FE-B2, FE-B3, FE-B4, UX-B8, UX-B9, UX-B13, UX-B14, API-B5 |
| R12g | Düşük | İstemci: hata sayfa altında/kart dışında; eski "kaydedildi"; 44 px; tablo kaydırma; ölü CSS; --ds-* ; VKN doğrulama/aria; ölü idempotency başlığı; logo fetch hatada null | UX-B5, UX-B10, UX-B11, UX-B15, UX-B16, FE-B10, FE-B11, FE-B13, FE-B16, FE-B17, FE-B21 |
| R13 | Düşük | settings.manage ApplicationPermissions'ta sabit değil | GY-B1, API-B8 |
| R14 | Orta/Düşük | Test boşlukları: GarsonFeatureToggles ve QrOrderExpirySetting testsiz; DELETE 409/401/403; logo DELETE 401/403; geçersiz NewValue; 136 down testi; axe yalnız giriş formu; 401/403/409 istemci testleri; elle yazılmış etiket testi | BE-B7, DB-D6, API-B12, DB-B9, FE-B14, FE-B20 |
| R15 | Düşük | Sır yasağı zayıf: yalnız anahtar adı, api_key/apikey yazımı; değer taranmıyor | BE-B11, GY-B4, API-B10 |
| R16 | Düşük | 136 sertleştirme (IF NOT EXISTS, CHECK, row_version tipi), kullanılmayan 3 indeks, CommandTimeout yok, logo çift kopya, logo PUT filtre öncesi 6 MB, logo tür büyük/küçük harf, poliglot | DB-B6, DB-B7, PD-B8, PD-B9, PD-B10, GY-B6, API-B7, BE-B14, SM-B8 |
| R17 | Düşük | Doküman sapmaları: registry yok (ModuleOwner serbest metin), RequiresRestart etkisiz, "history silinmez" şema ile zorlanmıyor, SET-002..006 evidence yok, kullanılmayan Npgsql PackageReference (6 test projesi), invoice-settings dilim dışı | SM-B5, SM-B6, SM-B9, DOK-1..7 |
| Doğrulanamayan | — | alkaros.manager çerezi SameSite/HttpOnly ve logo PUT/DELETE CSRF (DisableAntiforgery); CSP/nosniff logo yanıtında; business.name kaçışlaması QR sayfasında | GY, API |
