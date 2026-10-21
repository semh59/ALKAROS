# qa-guvenlik-yetki — V1-RMD-597 ham rapor özeti

94/231 (≈80 satır satır). Kanıt düzeyi Kod okuması; hiçbiri çalıştırılarak yeniden üretilmedi. Sır bulunmadı. Yetki envanteri: 26+4+10 = 40 kod, DB ve istemci etiketiyle birebir.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| G-01 | Yüksek | AuthenticationService.cs:156; DualScreenApplication.cs:377-378 | /auth/pin yanlış mevcut parolayı saymaz/kilitlemez; çalıntı oturum çerezi parola tahminine dönüşür (limit IP başına 240/dk). |
| G-02 | Yüksek | kiosk-lock.js:134-135; waiter-app.js:857-871; Endpoints.cs:219-221 | PIN kilidi yalnız istemcide; 423 oturumu iptal etmez, sunucuda kilit durumu yok; localStorage silinerek veya çerezle doğrudan API'ye atlanır. |
| G-03 | Yüksek | PostgresUserStore.cs:230-245; SecurityAdministrationEndpoints.cs:92 | Kabul 5 (5 yanlış PIN kilitler, yönetici açar) PIN için karşılanmaz. |
| G-04 | Orta | Endpoints.cs:210-213; SessionTokenIssuer.cs:13-15 | Her PIN açılışı oturumu yeni 8 saate uzatır (V1-IAM-031 ile çelişir). |
| G-05 | Yüksek | AuthorizationDecisionEndpoints.cs:30,160; AuthorizationDecisionStore.cs:58-78; Endpoints.cs:93-95 | Onay yetkisi reports.view; onaylayanın grant izni/tutar kademesi denetlenmez; rapor izinli özel rol onaycı olur. |
| G-06 | Orta | AuthorizationDecisionStore.cs:66-68; 045 | Öz-onay engeli yalnız Host'ta; DB tetikleyicisi yok. |
| G-07 | Orta | AuthorizationGrantService.cs:132-133,147-152 | Sıkılaştırma sonrası kişisel bütçe/temsil çözücüleri yine otomatik verir; PersonalCompBudget testsiz. |
| G-08 | Orta | DelegationEscalationResolver.cs:24-27; FindCoveringAsync | Temsil: kullanımda temsil edenin aktifliği denetlenmez, tavan kümülatif değil, hibede delegation_id yok. |
| G-09 | Orta | OfflineReconciliationEndpoints.cs:96-106; OfflineGrantReconciler.cs:~128; 047:65 | Çevrimdışı süre cihaz beyanıyla; IssuedAt alt sınırı yok; tutar sunucuda yeniden hesaplanmaz. |
| G-10 | Orta (gizli) | OfflineAuthorityBudgetService.cs:30-48; Endpoints.cs:127-128; 050 | Her girişte yeni bütçe; eskiler iptal/çıkış/süre sonrası da uzlaştırılır; bugün politika boş olduğundan etkisiz. |
| G-11 | Orta | UpsertAsync çağıranı yok; 044/099 INSERT 0 | authorization_policies hiç doldurulmuyor; otomatik onay ve çevrimdışı bütçe ölü. |
| G-12 | Orta | RoleManagementService.cs:72-109; RolesPanels.tsx | Rol yönetiminde sahip olmadığını verme/son sahip koruması yok; verme onaysız; audit yok. |
| G-13 | Yüksek | grep audit Roles/Identity/Authorization → 0; PostgresDenialEventSink | Rol/izin/kullanıcı mutasyonları denetim kaydı yazmaz; denial_events okunmaz, saklama yok, salt-ekleme değil. |
| G-14 | Orta | PermissionCodes.cs:16 | identity.device_sessions.manage hiçbir ucu korumaz. |
| G-15 | Orta | client.ts | POST /delegations için ekran yok. |
| G-16 | Orta | Endpoints.cs:245; Program.cs:729-735 | Zayıf/4 haneli PIN; KVKK anonimleştirme pin_hash'i temizlemez. |
| G-17 | Orta | Endpoints.cs:599; DualScreenApplication.cs:328-334 | TrustedProxies boşsa çerez Secure'suz; HSTS yok (dağıtıma bağlı). |
| G-18 | Orta | HelpRequestHub.cs:55-66; LowStockAlertHub.cs:28-40 | Hub bağlantısı sonrası yeniden doğrulama/izin kontrolü yok. |
| G-19 | Orta | waiter-app.js:140-148 | Çıkışta localStorage kuyruk/taslak/çevrimdışı eylemler silinmez (sonraki kullanıcıya sızar). |
| G-20 | Düşük | PostgresRoleRepository.cs:250-256; 005 | Kullanıcı adı kırpılmaz, büyük/küçük harf duyarlı. |
| G-21 | Düşük | StoredUser.cs; LoginResult.cs:11-14 | record ToString PasswordHash/PinHash/Token sızdırabilir (şimdilik gizli); ölü SessionTokenIssuer. |
| G-22 | Düşük | Program.cs:297-299 | denial_events, bütçe, delegasyon saklama süresi yok. |
| G-23 | Düşük (doküman) | login-timing-contract.md; AuthenticationService.cs:85-90 | Rehash "uygulama yok" notu eski; kilitli hesap daha hızlı yanıtlar. |

Bilinen: remaining-gaps-register V1-RMD-295 satırı artık geçerli değil. Doküman sapması: authorization-model.md:211 "manager UI to edit limits" yok.
Temiz: PBKDF2 600k+FixedTimeEquals, tek hata mesajı, atomik sayaçlar, token 32 bayt+SHA-256, oturum SQL denetimleri, çerez bayrakları, CORS yok, SQL parametreli, hata iletileri, 26 kodun kullanımı.
Doğrulanamayan: paket zafiyeti, Billing iade öz-onay, AccountRecovery/SessionRotation, dağıtım TLS, dinamik yeniden üretim, 137 kalem açılmadı.
