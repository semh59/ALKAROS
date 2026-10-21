# qa-api-uclar — V1-RMD-597 ham rapor özeti

24/231 dosya, 25/25 uç tek tek incelendi (karar 8, rol 8, kullanıcı 2, çevrimdışı uzlaştırma 1, oturum rotaları 6). Test yeniden koşulmadı. Kanıt düzeyi Kod okuması (aksi belirtilmedikçe).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Yüksek | AuthenticationService.cs:156, PostgresUserStore.SetPinAsync:78-93, Endpoints.cs:231-233 | POST /auth/pin yanlış parolayı saymaz ve kilide bakmaz; geçerli oturumla art arda yanlış currentPassword 401 döner, hesap kilitlenmez (5 deneme kilidi atlanır). |
| 2 | Orta | PostgresOfflineAuthorityBudgetRepository.cs:39,29-35; Endpoints.cs:326-340; OfflineReconciliationEndpoints.cs:77 | Her giriş yeni tam çevrimdışı bütçe verir; eskiler çıkıştan sonra süresine kadar geçerli, oturuma bağlı değil; N giriş = N kat yetki payı. |
| 3 | Orta | offline-queue.js:371,374; OfflineReconciliationEndpoints.cs:284-286 | İstemci 401/ağ dışındaki her yanıtta (503/500 dahil) kuyruğu siler. |
| 4 | Orta | OfflineReconciliationEndpoints.cs:269; api.js:56 | Uç düz {code,message,status,traceId} yazar, istemci data.error.* okur; UNKNOWN_BUDGET/IDENTITY_MISMATCH Türkçe mesajları ekrana çıkmaz. |
| 5 | Orta | PostgresAuthorizationGrantRepository.cs:28, OfflineGrantReconciler.cs:66, 045:25 | İdempotency anahtarı tablo genelinde tekil; tekrar yanıtı kullanıcı/bütçe/yük karşılaştırmadan başka kullanıcının onay kaydını döndürür; aynı anahtar+farklı yük çakışma vermez. |
| 6 | Orta | OfflineReconciliationEndpoints.cs:109, OfflineGrantReconciler.cs:51-83, DualScreenApplication.cs:739 | Actions sayısı sınırsız, N+1 sorgu; ortadaki eylemde Validate 400 atarsa öncekiler yazılmış (atomik değil); hız sınırı ve alan uzunluk sınırı yok. |
| 7 | Orta | PostgresAuthorizationGrantRepository.cs:181, PostgresAuthorizationDelegationRepository.cs:111, ApiErrorCatalog.cs:54-66,592 | 5000'den fazla bekleyen onay/aktif devirde liste uçları InvalidOperationException atar → 500; sayfalama yok. |
| 8 | Orta | PostgresAuthorizationGrantRepository.cs:108, PostgresRoleRepository.cs:105,145, ApiErrorCatalog.cs:473-475 | Var olmayan grantId için onay/ret 409 ALREADY_RESOLVED (404 olmalı); rol/kullanıcı yönetiminde yok-olan kayıt 409 "zaten var veya yok" tek koduna düşer; InvalidOperationException alt sınıfı 409. |
| 9 | Orta | RoleManagementService.cs:114-129, Endpoints.cs:70, PostgresUserStore.cs:31, 005:20 | Kullanıcı oluşturma adı kırpmaz ve büyük/küçük harf duyarlı; giriş Trim() ile arar → " ali" hesabı giriş yapamaz, "Ali" ve "ali" ayrı hesap. |
| 10 | Orta | Endpoints.cs:83,157-159,189-193,234-238,326-329; DualScreenStore.cs:49 | Giriş istemcinin gönderdiği GUID'i terminal yapar ve tabloya satır ekler; session/unlock/pin/logout'ta terminalId sorguda (diğer uçlarda yolda). |
| 11 | Orta | Endpoints.cs:277-285, DualScreenApplication.cs:829 | Hız sınırı partisyonu yalnız rota değerini okur; sorguda taşıyan 4 uçta bölüm "missing" → aynı IP'deki tüm terminaller tek kovayı paylaşır. |
| 12 | Orta | Grep sıfır sonuç komutları | Test boşlukları: unlock, pin, session/current için sıfır HTTP testi; Roles'te sıfır 400/404 testi; Offline'da sıfır 400; Authorization'da bilinmeyen grantId testi yok; Semih senaryosu (5 yanlış PIN kilitler, yönetici açar) HTTP düzeyinde testsiz. |
| 13 | Düşük | Endpoints.cs:200,219-227,244,246,254; DualScreenApplication.cs:848 | unlock/pin elle {error:{code,message}} kurar (status/traceId yok); *V1 kayıt tipi yok; session/current'ta roleName yok. |
| 14 | Düşük | DualScreenApplication.cs:737-739 | 19 yönetim ucunda hız sınırı yok (POST /users PBKDF2 hesaplatır). |
| 15 | Düşük | AuthorizationDecisionEndpoints.cs:99, RoleManagementEndpoints.cs:73, RoleManagementService.cs:91 | Revoke 404 boş gövde; rol/kullanıcı oluşturma 200 (delegasyon 201); rota roles/roles; bilinmeyen yetki iptali sessiz 204. |
| 16 | Düşük | RoleManagementService.cs:84-109, 054:20-22 | identity.roles.manage için son kullanıcı koruması yok; yönetici kendi yetkisini kaldırıp yönetimi kilitleyebilir; sahip olmadığı yetkiyi verme kuralı yok. |
| 17 | Düşük | client.ts:65,73; api.ts:38 | POST delegations ve POST roles/permissions için istemci çağrısı yok (ölü yüzey). |
| 18 | Düşük | AuthenticationService.cs:65,69-70; Endpoints.cs:80-100,210-213 | Kilitli hesap zamanlaması ayırt edilir; giriş çok adımlı yazma ve işlemsel değil; her doğru PIN 8 saatlik yeni ömür verir; üst uzunluk sınırı yok. |
| 19 | Düşük | OfflineReconciliationEndpoints.cs:214 | İstekte SubjectServingUserId kabul edilir ama yok sayılır. |

Doküman sapması: 17 değiştirici ucun hiçbirinde idempotency anahtarı yok (bilinen: idempotency_allowlist.json; V1-IAM-024/025, V1-RMD-102); offline allowlist gerekçesi bayat (IdempotencyKey eylem başına var); standart cursor/limit sayfalama yok.
Temiz: yönetim uçlarında oturum+yetki, kendi talebini onaylama koruması, delegasyon sınırları, çevrimdışı sahiplik, giriş sızıntısı/sayım, çerez bayrakları, güvenli hata gövdesi, parola hash sızıntısı yok.
Doğrulanamayan: bozuk gövde eşlemesi (ThrowOnBadRequest), DualScreenHostTests koşu kaydı, ApiConventions koşulmadı, DB alan sınırları, bulgu 1/2 uçtan uca yeniden üretilmedi.
