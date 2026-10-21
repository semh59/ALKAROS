# qa-backend — V1-RMD-597 ham rapor özeti

94/231 kalem kontrol edildi (Identity çekirdeği ~50 dosya + 7 Host dosyası tam; göçler 005..184 okundu; testler grep ile). Kanıt düzeyi Kod okuması. Koşu kayıtları yeşil (59, 223, 20, 9, 10, 12 + vitest 23).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| B1 | Yüksek | AuthorizationPolicyEvaluator.cs:26-28; BillingSplitApplication.cs:155-165; BillingSplitContracts.cs:104; OrderManagementEndpoints.cs:782 | İndirim yüzdesi TL limitiyle karşılaştırılır; void tutarı hep 0 → temsilin LimitAmount'u void'i sınırlamaz. Limit 100 olan politika/temsilde %90 indirim "100 ≤ 100" geçer. |
| B2 | Orta | OfflineGrantReconciler.cs:66-72; AuthorizationGrantService.cs:73,183-190; OfflineGrantReconciler.cs:152-159 | 2026-09-07'de çevrimiçi yolda kapatılan anahtar tekrar kullanımı düzeltmesi çevrimdışı yola taşınmamış: izin/tutar/talep eden kontrolsüz eski grant dönüyor (başka kullanıcınınki, GrantId sızar); yarışı kazanan kayıt da eşleşme kontrolsüz. |
| B3 | Orta | PostgresUserStore.cs:230-245; SecurityAdministrationEndpoints /users/lookup | Yönetici PIN kilidini açamaz ve göremez; kabul ölçütü 5 PIN için karşılanmaz (geçici yol: 15 dk veya parolayla girip SetPin). Parolayla giriş PIN sayaçlarına dokunmaz. |
| B4 | Orta | offline-queue.js:374; api.js:29; OfflineReconciliationEndpoints.cs:284-286; OfflineGrantReconciler.cs:51-53 | 400/429/500/503'te çevrimdışı kuyruk silinir; Validate döngü içinde, işlem yok → yarım parti kaydedilir. |
| B5 | Orta | PostgresOfflineReplayLedger.cs:28-31; OfflineReconciliationEndpoints.cs:282; OfflineAuthorityBudget.IsExpiredAt | Saati ileri giden tablette son dakikaların eylemi 400 ile reddedilir (B4 ile silinir); IssuedAt ≤ at alt sınırı yok, süre aşımı cihaz saatine göre. |
| B6 | Orta | OfflineGrantReconciler.cs:113-116; PostgresOfflineAuthorityBudgetRepository.cs:29-35; 050 | Sayım+karar+kayıt kilitsiz (çevrimiçide kilit var); her giriş yeni bütçe, eskisi 4 saat geçerli; sayım budget_id başına → tavan delinir (sonuç Pending). |
| B7 | Orta | AuthorizationGrantService.cs:132-133,147-152; PersonalCompBudgetEscalationResolver.cs:20-23; DelegationEscalationResolver.cs:25; OrderManagementCompHttpTests.cs:128-148 | Açık davranışsal sıkılaştırma yalnız AutoApprove'u Escalate yapar; sonra çözücüler çalışır ve ≤50 TL ikram (günlük 150) ve temsil yöneticiye gitmeden Granted olur; test 110 TL ile çalıştığı için yakalamaz. |
| B8 | Orta | AuthorizationDecisionStore.cs:58-78; AuthorizationDecisionEndpoints.cs:25-31,84; 045 tetikleyicisi | Onaylayanın grant.PermissionCode iznine sahipliği bakılmaz; kendi talebini onaylama koruması yalnız servis katmanında, DB tetikleyicisinde approver<>requester yok. |
| B9 | Orta | RoleManagementService.cs:84-109; Program.cs:38-44,171-176 | Son identity.roles.manage sahibi kaldırılabilir; provision-manager yalnız 4 kodu geri koyar (identity.* ve security.manage dönmez); kurtarma yalnız SQL. |
| B10 | Orta (doküman) | ApplicationPermissions.cs:8-10 | İzin kodları dört kaynakta (katalog 26, PermissionCodes 4, uç sabiti/Program.cs 10, göç tohumları); katalog-tohum tutarsızlığı yok; yorum "tek kaynak" yanlış. |
| B11 | Orta | authorization_policies INSERT araması database/src: 0 | Politika tablosunu yazan üretim yolu yok; politika boşsa her talep yöneticiye gider ve OfflineAuthorityBudgetPolicy.LinesFor boş; çevrimdışı void/ikram üretimde fiilen çalışmaz. |
| B12 | Orta | DualScreenApplication.Endpoints.cs:210-212; SessionTokenIssuer.cs:13-15 | Her başarılı PIN açılışı oturumu taze 8 saate uzatır; mutlak tavan yok. |
| B13 | Düşük | PersonalCompBudgetEscalationResolver.cs:56; OrderManagementEndpoints.cs | Günlük ikram bütçesi UTC gece yarısında sıfırlanır (TR 03:00); iş gününe bağlı değil. |
| B14 | Düşük | 045; PostgresAuthorizationGrantRepository.cs:164-185 | Bekleyen grant süresi dolmaz, temizlenmez; ListPendingAsync 5000'i aşınca fırlatır. |
| B15 | Düşük | Program.cs; RoleManagementService.cs:24 | Parola tabanı tutarsız (12 vs 8). |
| B16 | Düşük | RoleManagementService.cs:111-131; Endpoints.cs:70 | CreateUser adı kırpmaz, giriş kırpar; büyük/küçük harf duyarlı (Türkçe İ/i); rol/izin kodu uzunluğu servis katmanında doğrulanmaz. |
| B17 | Düşük | Endpoints.cs:245 | char.IsDigit: Unicode rakamlı PIN kurulabilir, tuş takımı giremez. |
| B18 | Düşük | DeviceSessionService.cs:5; AuthenticationService.cs:92 | Varsayılan ömür 30 gün yanıltıcı; LoginSuccess.Session ölü üretim. |
| B19 | Düşük | ReconnectAsync/GetProcessedOperationIdsAsync araması: modül dışı 0 | Yeniden bağlanma + session_operations çağıransız (ölü). |
| B20 | Düşük | PostgresUserStore.cs:153; AuthorizationGrantService.cs:85-92 | PIN başarısında DB now(), başarısızlıkta uygulama saati; boş konu çevrimiçide atlanır, çevrimdışında reddedilir. |
| B21 | Düşük (Çıkarım) | PostgresAuthorizationGrantRepository.cs:196-209 | Bağlantı tutan danışma kilidi, eşzamanlılık havuzu aşılırsa kilitlenme olasılığı. |

Not: PIN kilidi yalnız istemcide uygulanır, 5 yanlış PIN'den sonra sunucu oturumu geçerli kalır (DualScreenStore.cs:58-82; güvenlik ajanına). Doküman sapması: "5 yanlış PIN hesabı kilitler" kodla uyuşmuyor, PIN kilidi hesabı kilitlemez (AuthenticationService.cs:97-106).
Temiz: PIN/parola sayaçları atomik, giriş zamanlaması, kendi talebini onaylama (servis), temsil sınırları, çevrimdışı kimlik, oturum SQL kontrolleri, danışma kilidi, göç tohumları/RoleGrants uyumu, hata eşleme.
Doğrulanamayan: 14 istemci dosyası ve çoğu test/down göç okunmadı; etiket anahtarı farkı; "41" izin sayısı komutla üretilmedi; SKILL/gaps register taraması yapılmadı (bilinen işareti yok); captive dependency riski.
