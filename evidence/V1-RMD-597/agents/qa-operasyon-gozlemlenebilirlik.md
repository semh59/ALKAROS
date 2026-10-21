# qa-operasyon-gozlemlenebilirlik (dar kapsamlı) — V1-RMD-597 ham rapor özeti

22/231 kalem kontrol edildi (satır satır); kalanı yalnız arama. HEAD f4afb5e6. Migrationlar, policy/grant/delegation dosyaları ve 61 test okunmadı. Kanıt düzeyi Kod okuması.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Orta | AuthenticationService.cs:74-79 | 5. hatalı denemede hesap kilitlenir ama sonuç InvalidCredentials; kilit anı denetim/alarm üretmez, LockoutTriggered olayı yalnız kilitliyken yapılan sonraki denemede yazılır (SuspiciousLoginAuditingAuthenticationService.cs:72-73). |
| 2 | Orta | SuspiciousLoginAuditingAuthenticationService.cs:72-73 | Kilit sürerken (15 dk) her deneme ayrı denetim olayı yazar; audit_events şişer. |
| 3 | Orta | DualScreenApplication.Endpoints.cs:202-229 | PIN hatası/PIN kilidi (423 PIN_LOCKED)/UnlockFailure için log, denetim veya brute-force sayacı yok; yalnız /auth/login LoginFailures.Record çağırır (:73). PIN en az 4 rakam (:245). |
| 4 | Orta | PostgresUserStore.cs:225-241, SecurityAdministrationEndpoints.cs:92 | ForceUnlock yalnız parola sayaçlarını sıfırlar, pin_failed_attempts/pin_locked_until kalır; users/lookup IsLocked yalnız LockedUntil'den; operatör PIN kilidini göremez/açamaz (geçici yol: şifreyle giriş veya 15 dk). |
| 5 | Orta | data-housekeeping.md:15, Program.cs:297 | identity.denial_events için saklama/temizlik ve okuma ucu yok; kullanıcıya RESTRICT FK (074) KVKK silmeyi zorlaştırır. |
| 6 | Orta | docs/recovery/backup-restore-runbook.md:94-97 | Geri yükleme sonrası Identity durumu yazılı değil: yedekten dönen device_sessions iptal edilmiş oturumları, pasifleştirilen kullanıcıyı ve kilitleri geri getirebilir. |
| 7 | Orta | DualScreenApplication.Endpoints.cs:69-75, SuspiciousLoginAuditingAuthenticationService.cs:62-80 | Denetim sink yazımı hata verirse login 500 olur (kilit varsa 423 yerine); PostgresDenialEventSink hatası AuthorizationDeniedException yerine DB hatası atar. |
| 8 | Orta | AuditEventStoreSuspiciousLoginSink.cs:42-45 | Ham girilen kullanıcı adı denetim kaydına yazılır (şifre yazılmış olabilir); correlationId istekle ilgisiz rastgele GUID. |
| 9 | Düşük | Endpoints.cs:73, SecurityDetectors.cs:49-50,64 | Brute-force alarmı süreç geneli tek sayaç 10/60 sn; hesap başına yavaş saldırı eşiğe ulaşmaz. |
| 10 | Düşük | SessionTokenIssuer.cs:12, AuthenticationService.cs:92-93 | IssuedSessionToken record ToString ham belirteci basar; login ucu bu belirteci kullanmaz (ayrı belirteç üretir, :81) — üretilen belirteç boşa. |
| 11 | Düşük | AuthenticationService.cs:119-120 | PinNotSet 409 telemetrisiz; Identity modülünde hiç ILogger yok. |

Temiz: belirteç yalnız SHA-256 özetiyle saklanır; WaiterPwa localStorage yalnız kilit bayrağı; bilinmeyen kullanıcıda sahte PBKDF2; yetki reddi kalıcı yazılır, 20/60 sn alarmı var; operatör kurtarma uçları (revoke-sessions, force-unlock, deactivate, reactivate) yetki filtreli ve denetimli; süresi dolan oturum temizliği tanımlı; runbook master key uyarısı var.
Doğrulanamayan: security.* alarmının Telegram/ekrana ulaşması; restore provası kanıtı; kalan migrationlar.
