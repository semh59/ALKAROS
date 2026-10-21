# qa-mevzuat-uyum (dar kapsamlı) — V1-RMD-597 ham rapor özeti

42/231 dilim dosyası + 7 repo kaynağı. Yalnız Kod okuması; hukuki yorum yapılmadı. 29 izin tohumu göçü ve tests/Modules/Identity okunmadı.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| M1 | Yüksek (karar) | RoleManagementService.cs:72-101, PostgresRoleRepository.cs:114,154 | Rol-izin ve kullanıcı-rol atama/geri alma, rol/izin/kullanıcı oluşturma hiçbir denetim olayı yazmaz; kayıt DELETE ile fiziksel silinir. "Kim kime hangi yetkiyi verdi/aldı" cevaplanamaz. |
| M2 | Yüksek | AuthorizationGrantService.cs:175, DelegationEscalationResolver.cs:28, 045 şeması | Delegasyonla onaylanan hibede delegasyon/delegatör hibe satırına yazılmaz (ApproverUserId null; delegation_id kolonu yok); doküman authorization-model.md:162 "delegator recorded" der. |
| M3 | Orta | 046-authorization-delegations.up.sql | Delegasyon tablosunda hibe tablosundaki gibi geçiş/silme koruması yok; limit/süre/alıcı değiştirilebilir, satır silinebilir. |
| M4 | Orta | PostgresUserStore.cs:230-240, AccountRecoveryService.cs:58-63 | ForceUnlock yalnız parola kilidini sıfırlar; PIN sayaçları (pin_failed_attempts, pin_locked_until) kalır; kabul senaryosu 5'in PIN kısmı karşılanmaz. |
| M5 | Orta | Program.cs:732, kvkk-retention-runbook.md:17 | Personel anonimleştirmesi pin_hash, last_login_at ve sayaçları temizlemez. pin_hash'in kişisel veri sayılması karar. |
| M6 | Orta (karar) | kvkk-data-inventory.md:17, 005, 094 | Envanter display_name, email, phone, pin_hash, last_login_at, device_sessions alanlarını içermez; email/phone sütunlarını yazan uygulama kodu yok. |
| M7 | Orta (karar) | Program.cs:255,297, SuspiciousLoginAuditingAuthenticationService.cs:61 | Oturum kaydı bitiş+7 günde silinir; başarılı giriş denetime yazılmaz (yalnız 3+ başarısızlık sonrası), last_login_at üzerine yazılır; erişim kaydı süresi/kapsamı karar. |
| M8 | Orta | DualScreenApplication.Endpoints.cs:194,239 | PIN kilitlenmesi, koyma/kaldırma denetim olayı üretmez (parola kilidi üretir). |
| M9 | Orta (karar) | OrderManagementEndpoints.cs:776-782,676, AuthorizationGrantService.cs:127 | Void hibesi tutar olarak 0 kaydeder; limit/politika/delegasyon limiti void'de her zaman geçer; reporting.authorization_grant_daily toplayamaz. |
| M10 | Orta (karar) | PersonalCompBudgetEscalationResolver.cs, void-complimentary-discount-policy.md:34 | Garson yönetici onayı olmadan 50 TL/kalem, 150 TL/gün ikram verebilir; iki karar belgesinde yok; sıfırlama UTC günü (TR 03:00); karar kaydı repoda yok. |
| M11 | Orta | OfflineGrantReconciler.cs:106, OfflineAuthorizedAction.cs Validate | OfflineAuthorizedAt cihazdan gelir, IssuedAt ≤ at ≤ şimdi sınırı yok; 4 saatlik süre istemci beyanına dayanır. |
| M12 | Düşük | AuthorizationGrantService.cs:88 vs OfflineGrantReconciler.cs:97-102 | SubjectId'siz istek çevrimiçi own-check'i geçer, çevrimdışı reddedilir; bugün sömürülebilir çağıran yok. |
| M13 | Düşük | AuthorizationDecisionEndpoints.cs:28 | Onaylayan yalnız reports.view + yönetim oturumu ister; hedef izne sahiplik/tutar sınırı denetlenmez. |
| M14 | Orta (karar) | AuditEventStoreSuspiciousLoginSink.cs:41, kvkk-retention-runbook.md:55-56 | Denetim olayı ham kullanıcı adını yazar (append-only, 10 yıl); anonimleştirme etkisiz kalır; runbook belgelemiş (bilinen), kabul kararı kaydı yok. |
| M15 | Düşük | UpsertAsync src: 0, authorization-model.md:211 | authorization_policies tablosunu yazan üretim kodu/tohum yok; Evaluate(null) hep Escalate; "yönetici arayüzünden limit düzenleme" ve çevrimdışı bütçe satırları fiilen yok. |

Doküman sapması: void-complimentary-discount-policy.md:60 "Manager authority" ile ApplicationPermissions.cs:127 supervisor doğrudan yetkili; kvkk-data-inventory "Delete" ile kod "anonymize"; 094 yorumu "30 gün" ile kod 8 saat.
Temiz: kendi talebini onaylama/kilidi kaldırma reddi, onay yarışı atomik, hibe tablosu silinemez, FK RESTRICT, idempotency tam eşleşme, parola/PIN ayrı sayaç 5 deneme 15 dk, PIN koymak için parola şart, uzlaştırmada aktör/rol sunucudan, yönetim oturumu aktif+iptal denetimi, rol varsayılanları belgeyle uyumlu, ForceUnlock ve oturum iptali denetim yazar, her yetki reddi denial_events'e yazılır, iade isteyen≠onaylayan.
Doğrulanamayan: hibe gerektirmeyen yetkili void/ikram/indirimin denetim satırı (Identity dışı); çevrimdışı reddedilen eylemin cihazda geri alınışı; yasal erişim kaydı gereği; audit-disposal kararları (runbook'ta açık).
Ürün sahibi soruları: M1 (izin değişikliği izi), M7 (erişim kaydı süresi), M5/M6 (pin_hash, email/phone), M10 (kişisel ikram bütçesi 50/150 TL onayı), M9 (void tutarı 0 bilinçli mi).
