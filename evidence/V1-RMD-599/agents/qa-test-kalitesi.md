# V1-RMD-599 Host (platform) - Test gücü ve kapsam denetimi (qa-test-kalitesi)
HEAD: d1dc86b6. Kanıt düzeyleri: Ç=Çalıştırıldı, K=Kod okuması, Çık=Çıkarım.

## 1. Kapsam envanteri ve kontrol sayısı
- Envanter: evidence/V1-RMD-599/baseline/scope-files.txt, `wc -l` = 53 (48 src/Host + 5 göç SQL). 53/53 kalem kontrol edildi (ad ve yol bazlı grep: tests/Host, tests/Architecture).
- Kayıtlı koşular (evidence/V1-RMD-599/runs/exit-codes.txt): ApiConventions 5/5, ModuleBoundaries 10/10, Host.Errors 180/180, MigrationComposition 196/196 (17 dk 36 sn), Host.Observability exit=1 (kesik log, Başarısız satırı yok) = DOĞRULANAMADI. Atlanan: hepsinde 0.
- Skip taraması: grep -rnE "Skip\s*=|\[Fact\(Skip" tests/Host tests/Architecture = 0 sonuç. Sabit bekleme (Task.Delay) 25 yer.

## 2. Mutasyon özeti
Repo dışı worktree TEMP/alk-mut-host, tek koşu, her turda yedek/geri al/cmp; worktree silindi, ana ağaç değişmedi. Çıktılar: evidence/V1-RMD-599/mutation/ (summary.txt, *.diff, *.log). 19 geçerli mutant: 12 öldü, 7 hayatta; A03 geçersiz (derleme hatası, öldürülmüş sayılmadı).
| Mutant | Değişiklik | Sonuç |
|---|---|---|
| E01 | Default: PostgresException 503 -> 500 | ÖLDÜ |
| E02 | Catalog InvalidOperationException 409 -> 500 | ÖLDÜ |
| E03 | RoleManagement InvalidOperationException 409 -> 400 | ÖLDÜ |
| E04 | ApiErrorCatalog.cs:196 NumericValueOutOfRange kolu silindi | HAYATTA |
| E05 | ApiErrorCatalog.cs:348 NumericValueOutOfRange kolu silindi | HAYATTA |
| E06 | Default: CashSessionException (taban) 400 -> 500 | HAYATTA |
| E07 | Resolve en dıştan başlıyor | ÖLDÜ |
| E08 | Resolve: area.Catches koşulu kaldırıldı | HAYATTA (kısmen eşdeğer) |
| E09 | WriteAsync günlük eşiği >=500 -> >500 | HAYATTA (Errors projesinde) |
| E10 | ApiErrorScope.Enter areas.Add kapatıldı | HAYATTA (Errors projesinde) |
| E11 | ApiErrorArea.Catches Any -> All | ÖLDÜ (133 başarısız) |
| B01 | Host.Experience.Orders içinde Production bağımlılığı | ÖLDÜ |
| B02 | Host.Experience.Catalog içinde Production bağımlılığı | HAYATTA |
| B03 | Host.Experience.WebPush içinde Production bağımlılığı | HAYATTA |
| B04 | BillingModule.DependsOn + Kitchen | ÖLDÜ |
| B05 | BillingModule.DependsOn - Orders | ÖLDÜ |
| B06 | Host içinde ham INSERT INTO audit.audit_events | ÖLDÜ |
| A01 | revoke ucunda RequireCashierAsync kaldırıldı (ApiConventions) | ÖLDÜ |
| A02 | approve ucunda RequireCashierAsync kaldırıldı | ÖLDÜ |
| A03 | session/current null kontrolü | GEÇERSİZ (CS8602) |
E09/E10 yalnız tests/Host/Errors ile denendi; Observability/Experience testlerinin onları öldürüp öldürmediği DOĞRULANAMADI (büyük proje çalıştırılmadı).

## 3. Bulgu tablosu
| No | Önem | Kanıt | Sorun | Etki | Önerilen düzeltme |
|---|---|---|---|---|---|
| T-01 | Yüksek (Kod okuması) | src/Host/DualScreen/DualScreenApplication.Endpoints.cs:492,501,513,525. Arama: grep -rniE pairing/session-current/display-sessions tests --include=*.cs -c => yalnız DualScreenHostTests.cs:5 (satır 253 pairing-requests yalnız hız sınırı probu) ve Identity PermissionSplitDatabaseTests.cs:1. CreatePairingRequestAsync, ApprovePairingAsync, CompletePairingAsync, RevokeDisplaySessionsAsync için tests altında 0 .cs eşleşmesi; DualScreenStoreTests içinde Pairing 0 | Müşteri ekranı eşleştirme akışının (create/approve/complete: kod, süre dolumu, yanlış veya yeniden kullanılmış kod, gizli anahtar, çerez, token hash) sunucu testi yok; yalnız istemci JS/E2E testleri var | Kimlik doğrulama akışı gerilemesi yakalanmaz | PG destekli uç testi: mutlu yol, yanlış/süresi dolmuş/yeniden kullanılmış kod, başka terminal, çerez bayrakları |
| T-02 | Yüksek (Kod okuması + Çalıştırıldı) | Endpoints.cs:535-545 display-sessions/revoke. DualScreenHostTests.cs:443 testi revoke işlemini uçtan değil SQL UPDATE ile yapar. A01 mutantı yalnız oturumsuz çağrıyı yakalar | Revoke gerçekten oturumları iptal ediyor mu, yanıttaki revoked sayısı, başka terminali etkilememe sınanmıyor | Revoke bozulursa sızıntı fark edilmez | Revoke sonrası ekran çerezi ile snapshot 401, sayı doğrulaması |
| T-03 | Orta (Kod okuması) | Endpoints.cs:271-289 /auth/session/current. grep -rn session/current tests => yalnız PermissionSplitDatabaseTests.cs (uç testi değil). Mutasyon A03 derlenmedi | capabilities, userId, terminalId ve çerezsiz 401 sınanmıyor (GET /auth/session için test var, current için yok) | İstemci yetkileri bu uçtan okur; sessiz bozulma | Uç testi: çerezsiz 401, geçerli çerezle capabilities |
| T-04 | Orta (Çalıştırıldı) | Mutant E04 (ApiErrorCatalog.cs:196), E05 (:348) hayatta; 5 alanda NumericValueOutOfRange kolu var (satır 122,196,283,348,563); ApiErrorCatalogTests.cs içinde 22003 yok, yalnız Pg 23514 | 22003 -> 400 VALIDATION_FAILED kolu hiçbir alanda sınanmıyor | Taşan sayı yanlış koda düşse fark edilmez | Her alan için Pg 22003 satırı |
| T-05 | Orta (Çalıştırıldı) | Mutant E06 hayatta; Default içinde CashSessionException ve CashTenderException taban kolları (400), testte yalnız alt sınıflar (ApiErrorCatalogTests.cs:210-224). Ayrıca Default yalnız PostgresException için 503 verir; soket kaynaklı NpgsqlException 500 INTERNAL_ERROR olur, alanlar ise 503 verir | Taban sınıf kolları ve Default içindeki NpgsqlException davranışı sınanmıyor | Yanlış durum kodu fark edilmez | Taban sınıf örnekleri ve NpgsqlException için satır (davranış kararı ayrı) |
| T-06 | Orta (Çalıştırıldı) | E09 ve E10 hayatta. ApiErrorCatalogTests.cs yalnız Resolve çağırır; ApiExceptionHandler.WriteAsync (zarf JSON, traceId, 5xx günlüğü), ApiErrorScope.Enter/Entered, ApiErrorHandling.EnsureFor için Errors projesinde test yok. Zarf yalnız Observability TraceCorrelation ve ServerEndToEnd testlerinde dolaylı olabilir (çalıştırılmadı) | Zarf biçimi ve 5xx günlüğü bu projede sınanmıyor | Bozulma bu projede kırmızı vermez | DefaultHttpContext ile WriteAsync testi (status, code, traceId, 5xx günlüğü); Enter/Entered birim testi |
| T-07 | Orta (Çalıştırıldı) | ModuleBoundaryTests.cs ApprovedHostOrchestrationEdges 11 ad alanı. grep -rhoE namespace ALKAROS.Host src/Host => Experience altında 30 dan fazla ad alanı daha (Authorization, Catalog, Menu, Tables, Settings, Inventory, Production, Purchasing, WebPush, HelpRequests, WaiterNotifications, Reporting, Reconciliation, OnlineOrdering, QrOrdering, NfcOrdering, Roles ...), ayrıca Composition ve Outbox. B02 (Catalog) ve B03 (WebPush) hayatta | Host ad alanlarının çoğu modül-kenar kuralından muaf | Modül bağımlılığı eklense mimari test geçer | Listede olmayan her Host ad alanı için varsayılan kural, veya ad alanı listesinin src/Host ile eşlendiğini doğrulayan test |
| T-08 | Orta (Kod okuması) | ProductionExperienceCompositionTests.cs:332-336 (3 girişli ApprovedHostDecoratedModuleServices), 355-372 ve 380-388. Mutasyonla denenmedi (büyük proje) | (a) Sözlük yalnız tek kayıtlı servis dalında kullanılır; çok kayıtlı dalda dekoratör denetlenmez (b) Sözlükte ölü giriş fark edilmez (c) Dekoratörün modülün kendi uygulamasını sardığı doğrulanmaz (d) ImplementationInstance olan kayıtlar atlanır | Yeni sessiz Host geçersiz kılması ve bayat allowlist | Her anahtarın modül servis listesinde bulunduğunu doğrulayan assert; iç örnek kontrolü |
| T-09 | Orta (Kod okuması) | grep -rlw tests/Host tests/Architecture: CustomerDisplayHub 0, SignalRCashierQueueAnnouncer 0, WaiterNotificationDispatch 0, DualScreenExceptions 0 eşleşme; hubs/customer-display için test yok | Müşteri ekranı hub yetkisi/grubu ve bildirim yönlendirme kararı (hedef bağlıysa grup, değilse yayın, push bağımsız) sınanmıyor; WaiterPresenceTracker ve HubGroup testleri var, dispatch kararı yok | Yanlış alıcıya bildirim fark edilmez | HelpRequestHubTests kalıbıyla hub yetki testi; Dispatch karar tablosu birim testi |
| T-10 | Düşük (Kod okuması) | Sabit bekleme: AnalysisRunnerTests.cs:119 (5000 ms), InsightRunnerTests.cs:47,73, KitchenOperationsHttpTests.cs:505, YemeksepetiStatusSyncTests.cs:251, QrConfirmationReservationHttpTests.cs:137,206 (toplam 25 yer) | Zamana bağlı, flaky riski | Aralıklı kırmızı, boşa süre | Koşul beklemeli yardımcı |
| T-11 | Düşük (Kod okuması) | ModuleBoundaryTests.cs: yasak küme ModuleAssemblies.Except(onaylı); ALKAROS.Secrets ve ALKAROS.QrRelay ModuleAssemblies içinde yok | Bu iki derlemeye bağımlılık hiçbir Host ad alanında yasaklanamaz | Kenar denetimi boş | Listeye ekle |
| T-12 | Düşük (Çalıştırıldı) | E08 hayatta (ApiExceptionHandler.Resolve area.Catches koşulu) | Koşul olmadan da testler geçer (alanlar null döner) | Ölü kontrol ya da eksik test | Kontrolü sil ya da Catches olmayan tipe yanıt veren alanla test ekle |

## 4. Ajan iddiaları
- HelpRequests ve WebPush için sunucu testi hiç yok iddiası: ÇÜRÜTÜLDÜ. HelpRequestHttpTests.cs (7 test: başarı, bekleme süresi, iki eşzamanlı istek tek kazanan, geçersiz tür, olmayan masa, oturumsuz 401), HelpRequestHubTests.cs (4), WebPushHttpTests.cs (6), WebPushCryptoTests, WebPushSenderTests, PushSubscriptionStoreTests mevcut.
- PostgresException->503: Default içinde ve NpgsqlException olarak her alanda sınanıyor (E01 öldü). InvalidOperationException->409: Catalog ve RoleManagement içinde sınanıyor (E02, E03 öldü). Boşluklar T-04, T-05.
- Eşleştirme, revoke, session/current: DOĞRULANDI test yok (T-01..T-03). Mimari testin 11 ad alanı: DOĞRULANDI (T-07).
- Park edilmiş V1-RMD-474, 519, 572, 588: bu dilimde ilişkilendirilen bulgu yok.

## 5. Temiz bulunan alanlar
- ApiErrorCatalog: yaklaşık 180 eşleme durum+kod+Türkçe mesajla birebir sınanıyor, zayıf assert yok; iç-önce çözüm ve düşme testli (E01-E03, E07, E11 öldü).
- ModuleBoundaries: DependsOn onaylı kenar, gerçek derleme bağımlılığı, ham audit insert, Orders ad alanı kuralı mutantları öldü (B01, B04, B05, B06).
- ApiConventions: oturumsuz uç denetimi gerçek PG ile çalışıp yetki kaldırma mutantlarını öldürüyor (A01, A02); allowlist yalnız küçülür.
- HelpRequests eşzamanlılık testi gerçek PostgreSQL ile.
- Skip içeren test yok; Atlanan=0.
- Göç dosyaları (5 SQL) MigrationComposition içinde yürütülüyor (196/196); DDL ayrıntısı bu raporun odağı dışı.

## 6. Doğrulanamayanlar
- Host.Observability koşusu (exit=1, kesik log): yeniden başlatılmadı, sonuç belirsiz (altyapı veya bellek olasılığı).
- E09, E10, T-08: büyük projeler çalıştırılmadı (kural); diğer testlerin öldürüp öldürmediği bilinmiyor.
- A03: geçerli mutant üretilemedi.
- Satır bazlı kapsama yüzdesi: kapsama aracı çalıştırılmadı; 53 kalemin kapsamı ad/yol grep ile belirlendi (Çıkarım).
