# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

`GATE-V1-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altında 301 görev tanımlıdır: 296 `Done`, 5 onaylı `NotApplicable`, 0 `Planned`,
  0 `Blocked` ve 0 `InProgress` görev vardır (`find plan/v1 -name "V1-*.md" -not -path
  "*/README.md" | xargs grep -h "^- Status:" | sort | uniq -c` ile doğrulanmıştır).
  2026-09-05 32. dalga (Semih onayıyla — eski denetimin H4 bulgusu araştırılırken tüm
  `src/Clients/WaiterPwa/SessionQueue/**` modülünün (`WaiterOfflineQueueEngine` dahil) kendi
  test dosyasından başka hiçbir yerden hiç referans edilmediği bulundu; gerçek üretim
  istemcisi (`waiter-app.js`) ayrı ve zaten doğru bir çevrimdışı kuyruk mantığı içeriyordu —
  `V1-RMD-104`) `V1-GOV-084` sonrası kapıyı fiilen yeniden açtı; `V1-GOV-085` ile resmen
  kaydedildi; modül (4 kaynak dosyası + test projesi, 9 test) tamamen kaldırıldı, `V1-GOV-086`
  ile 32. dalga kesin olarak yeniden mühürlendi. Aynı sınıftaki bir diğer aday
  (`OrderEntryEngine.BeginSubmission()`) kasıtlı olarak dokunulmadan bırakıldı.
  2026-09-05 31. dalga (Semih onayıyla, "tümünü düzelt" — daha eski, ayrı bir denetim turunun
  hâlâ açık altı bulgusu: B1 [HIGH] bill adjustments (indirim) tamamen bağlanmamıştı, H1 [MED]
  `BillingSplitStore`'un finansal yolda geniş `catch (Exception)` kullanması, H2/H3
  `AuditSanitizer`'ın anahtar-tabanlı redaksiyon kusurları, B5 `SplitEngine`'in kısmi kalem
  bölmesinde kalan-dengeleme eksikliği, B6 `catalog.products`'ta hiç `row_version` olmaması —
  `V1-RMD-103`) `V1-GOV-082` sonrası kapıyı fiilen yeniden açtı; `V1-GOV-083` ile resmen
  kaydedildi. B1 için `bills.discount` grant-class izni (zaten var, hiç kullanılmamıştı)
  üzerinden `POST .../bills/{billId}/discount` ve `GET .../bills/{billId}/adjustments`
  eklendi — `V1-BIL-005`'in `bills.comp` için yaptığı ilk gerçek HTTP çağrısıyla aynı desen;
  indirimin `SplitEngine`/ödeme akışına tam entegrasyonu ayrı bir tasarım kararı olarak
  kapsam dışı bırakıldı. H1'in regresyon testi yazılırken `AddBillingSplitExperience()`'ın
  hiç `IOrderRepository` kaydetmediği (Catalog'un daha önce bulunan DI boşluğuyla aynı sınıf)
  ayrıca bulunup düzeltildi. `V1-GOV-084` ile 31. dalga kesin olarak yeniden mühürlendi.
  2026-09-05 30. dalga (Semih onayıyla, "Hepsi" — 29. dalganın bilerek kapsam dışı bıraktığı
  High/Medium/Low bulgular — `V1-RMD-102`) `V1-GOV-080` sonrası kapıyı fiilen yeniden açtı;
  `V1-GOV-081` ile resmen kaydedildi. 9 bulgu giderildi: `PostgresBillRepository
  .UpdateBillItemAsync`'e eksik item-seviyesi `row_version` kontrolü eklendi;
  `CatalogManagementEndpoints.AddCatalogManagement()`'a eksik filter bağımlılık kaydı
  eklendi; `IRoleManagementService` için `/api/v1/management/roles` altında 6 endpoint'lik
  yeni bir HTTP yüzeyi eklendi (önceden yalnız ham SQL ile yönetilebiliyordu); Cashier ve
  WaiterPwa üretim JS'ine çift-tıklama/yeniden-giriş koruması eklendi; `Order.CancelItem`
  artık sessizce attığı reason/actor/timestamp'i bir `OrderStatusHistoryEntry` olarak
  kaydediyor; `PostgresKitchenTicketRepository.SaveAsync`'in her save'de TÜM kalemlerin
  row_version'ını (yalnız dönüştürüleni değil) artıran sahte-concurrency kusuru
  `WHERE status IS DISTINCT FROM EXCLUDED.status` ile düzeltildi; `order.json`/
  `MigrationManifest.cs`'teki stale "031-049" faz aralığı gerçek "031-052"ye düzeltildi;
  `Done` görevlerin 73'ünün "Owned surface" bölümündeki hiç var olmamış `evidence/V1-XXX/**`
  iddiası mekanik olarak kaldırıldı; iki kullanılmayan TS import'u temizlendi. İki bulgu
  için (item-level row_version, kitchen ticket row_version) düzeltme geçici geri alınıp
  yeni regresyon testinin gerçekten kırıldığı doğrulandı, sonra geri getirildi. `V1-GOV-082`
  ile 30. dalga kesin olarak yeniden mühürlendi. 2026-09-05 29. dalga (Semih'in isteğiyle 4 bağımsız
  ajanla sıfırdan tam denetim taraması; her Critical bulgu bizzat doğrulandı — `V1-RMD-101`)
  `V1-GOV-078` sonrası kapıyı fiilen yeniden açtı; `V1-GOV-079` ile resmen kaydedildi. 3
  Critical defekt giderildi: submit rotası çakışması (PosTerminal'in asıl sipariş gönderme
  akışı her istekte 500 veriyordu), Cashier hızlı-satış istemcisinde sahte "İkram" (ekranda
  ₺0 gösterip müşteriyi tam fiyattan faturalayan bir para/güvenlik açığı — kaldırıldı), ve
  `SentItemVoidStore`'un Order/Kitchen'ı Bill kontrolünden önce kalıcı hale getirmesi
  (müşteriyi teslim edilmeyecek bir ürün için ödemeye zorluyordu, iz bırakmadan — sıra
  değiştirildi). `V1-GOV-080` ile 29. dalga kesin olarak yeniden mühürlendi. High/Medium/Low
  bulgular (Catalog'un eksik DI kaydı, çift-tıklama koruması, `IRoleManagementService`'in
  endpoint'siz kalması, `Order.CancelItem`'ın parametrelerini yok sayması, Kitchen'ın sahte
  concurrency çakışması, eksik `evidence/` dizinleri) ayrı bir remediasyon dalgasına
  bırakılmıştı — 30. dalga (`V1-RMD-102`, yukarıda) hepsini giderdi. 2026-09-04 (Semih onayıyla, bu sohbette)
  `V1-RMD-100` kapatıldı — `docs/domain/table-reservation-policy.md`'nin kendi 2026-09-03
  tarihli Amendment'ı ve "Who creates `Reserved`" satırı shipped davranışla (kasiyer
  rezerve eder, garson WaiterPwa'da edemez) örtüştüğü onaylanarak; kod/test/migration diff'i
  yok. V1'in son açık görevi kapandı; `V1-GOV-077` kapıyı yeniden açtı, `V1-GOV-078` kesin
  olarak yeniden mühürledi. 2026-09-04 27. dalga (Semih onayıyla `docs/domain/table-reservation-policy.md`'ye ikinci bir
  Amendment: rezervasyon her işletmede aynı işlemez — işletme başına açılabilir bir ayar
  (`reservations.dedicated_station_enabled`, `V1-SET-003`) arkasında, müşteri ekranı gibi kendi
  URL'i olan ayrı bir "Rezervasyon İstasyonu" ekranı (PosTerminal `/reservations`, `V1-CUI-006`)
  sunulabilir; kasiyerin kendi kat planındaki koşulsuz "Rezervasyon al" aksiyonu değişmeden sürer,
  hiçbir yeni izin kodu eklenmedi — `V1-TBL-008`) `V1-GOV-074` sonrası iki yeni görevle kapıyı
  fiilen yeniden açtı; `V1-GOV-075` ile resmen kaydedildi. `V1-CUI-006` sırasında gerçek, önceden
  var olan bir kusur bulunup giderildi: PosTerminal'in `workspace.tsx`'i `/`, `/tables`, `/billing`,
  `/kitchen` rota erişimini hâlâ migration 049'da (`V1-IAM-024`) kataloktan kaldırılmış
  `pos.cashier.mutate`'e göre kontrol ediyordu — hiçbir oturum bu izni bir daha hiç tutamayacağından
  Satış dışındaki HER ekran tüm kullanıcılar için sessizce erişilemezdi; granüler karşılıklarına
  (`orders.create`/`tables.status`, `bills.split`, `orders.send`) düzeltildi, regresyonu önleyen 6
  yeni test eklendi. `V1-GOV-076` ile 27. dalga kesin olarak yeniden mühürlendi:
  `pnpm --dir src/Clients/PosTerminal typecheck`/`test`/`build` sıfır çıkış kodu (109 → 118 test);
  `dotnet build -c Release`/`-c Debug` 0 uyarı/0 hata; ilgili tüm .NET regresyon testleri yeşil
  (bir istisna dışında — aynı önceden bilinen `psql`-eksik ortam boşluğu, G2, bu dalgadan bağımsız);
  `consistency_audit.py` temiz; `plan_audit_tool.py validate`/`validate-coverage`/`verify-manifest`
  sıfır hata. 2026-09-04 25. dalga differentiated-authorization (`V1-IAM-016..025`: `waiter` rolü, izin kodu
  granülerleştirmesi, policy/grant/delegation/offline-authority/behavioural-tightening motorları,
  her Experience endpoint'inin granüler koda bağlanması, `pos.cashier.mutate` takma adının
  kaldırılması, migration 044-052 — offline bütçe yeniden-ihraç FK çökmesi, delegation revoke
  actor'ı, davranışsal oran indeksi dahil — ve istemci tarafı kablolama: login'de offline bütçe
  ihracı, yeni reconnect endpoint'i, `workspace.tsx` rota testi — Semih onayıyla)
  `GATE-V1-EXIT`'i `V1-GOV-070` sonrası fiilen yeniden açtı; `V1-GOV-071` ile resmen kaydedildi ve
  `V1-IAM-025` ile sağlamlaştırma + kablolama tamamlandıktan sonra `V1-GOV-072` ile kesin olarak
  yeniden mühürlendi. Grant-class bir mutasyon endpoint'i (bills.void/comp/discount) gerektiren
  kalıntı kapsam — bağımsız denetimin önceden bilinen B1 bulgusuyla aynı kök — `V1-IAM-026` ile
  karara bağlandı: Semih onayıyla (2026-09-04) `docs/domain/void-complimentary-discount-policy.md`
  Amendment, gönderilmiş-ama-servis-edilmemiş bir kalemin `bills.void` grant'iyle (işletme başına
  ayarlanabilir bir mutfak-senkronizasyon anahtarı arkasında) iptal edilebileceğini kaydetti. Bu,
  `GATE-V1-EXIT`'i `V1-GOV-072` sonrası altı yeni Blocked görevle (`V1-SET-002`, `V1-KIT-005`,
  `V1-WTR-009`, `V1-ORD-005`, `V1-BIL-005`, `V1-IAM-027`) 26. dalga olarak yeniden açtı;
  `V1-GOV-073` ile resmen kaydedildi. Altı görev tamamlandı: `V1-SET-002` `kitchen.live_sync_enabled`
  anahtarını ekledi; `V1-KIT-005` mutfak bilet kalemi durumunu gerçek `OrderItem.KitchenState`'e
  senkronladı (anahtar açıkken); `V1-WTR-009` kalem hazır olduğunda bağlı garson cihazlarına
  SignalR bildirimi yayınladı; `V1-ORD-005` gönderilmemiş kalem için ücretsiz void uç noktasını
  bağladı ve aynı zamanda `OrderManagementEndpoints.cs`'in iki önceden var olan kusurunu giderdi
  (`DualScreenStore` hiç kayıtlı değildi, oturumsuz istekler 401 yerine 500 dönüyordu); `V1-BIL-005`
  comp uç noktasını `IAuthorizationGrantService.RequestAsync`'e bağladı — bu, tüm grant-akışı
  motorunun (policy/delegation/behavioural-tightening, `V1-IAM-019/020/021/023`) ilk gerçek HTTP
  çağrısıydı; `V1-IAM-027` `OrderItem.Cancel()`'ın reddini gevşetip gönderilmiş-ama-servis-edilmemiş
  bir kalemin `bills.void` grant'iyle iptalini, eşleşen mutfak bilet kalemi iptalini ve
  `BillLineType.Waste` satırına (III.7.2, hiç üretilmemişti) dönüşümü ekledi. Üç görevde de model
  §3'ün "own check" kuralı, hiçbir garson/sipariş servis-atama modeli olmadığından disclosure'lı
  olarak uygulanamadı (`SubjectServingUserId: null`) — gelecek iş olarak kaydedildi. `V1-GOV-074`
  ile 26. dalga kesin olarak yeniden mühürlendi: `dotnet build -c Release`/`-c Debug` 0 uyarı/0 hata;
  `dotnet test ALKAROS.slnx` 55 test projesinin 54'ü tam yeşil, tek istisna önceden bilinen ve bu
  dalgayla ilgisiz bir ortam boşluğu (`ALKAROS.Host.Tests`'in `psql` CLI'sinin bu makinede kurulu
  olmaması — G1); `consistency_audit.py` temiz; `plan_audit_tool.py validate`/`validate-coverage`/
  `verify-manifest` sıfır hata. 2026-09-03 24. dalga (Docker arayüz/backend ayrımı A1-full: `web`
  Caddy imajı statik istemci paketlerini sunar + TLS sonlandırır + `/api` `/hubs`'ı `api:5080`'e
  proxy'ler; `api` imajı `serve --api-only` ile yalnız JSON API + hub'ları düz HTTP çalıştırır;
  `Dockerfile` → `deploy/docker/Dockerfile` adlandırılmış aşamalarla; operatör araçları
  `compose.ops.yaml`, dev overlay `compose.dev.yaml`; `V1-RMD-096` Host self-signed `8444` yedeği
  bu ayrımla kaldırıldı; WaiterPwa personel girişi + `crypto.randomUUID` düz-HTTP çökme düzeltmesi;
  Semih onayıyla, `V1-RMD-098`) ile `GATE-V1-EXIT` yeniden açılıp `V1-GOV-070` ile kesin olarak
  yeniden mühürlenmiştir. 2026-09-01 23. dalga (Host-terminated HTTPS: Host artık
  `https://0.0.0.0:5443` üzerinde kendinden imzalı bir sertifikayla — SAN
  `ALKAROS_PROXY_HOST` — TLS sonlandırır; garson telefonları Caddy önde olmasa da güvenli
  bağlam / çevrimdışı kuyruk elde eder; düz `5080` portu güvenilir proxy için kalır ve
  `HTTPS_REQUIRED` ile fail-closed'dır; Semih onayıyla V1'e çekildi, `V1-RMD-096`) ile
  `GATE-V1-EXIT` kapısı yeniden açılıp `V1-GOV-069` ile kesin olarak yeniden
  mühürlenmiştir. 22. dalga (`V1-RMD-095`, `V1-GOV-067`) PostgreSQL WAL arşivleme /
  point-in-time recovery ve rakiplere göre kalibre edilmiş RPO/RTO hedefleri tamamlanmıştır
  (para/mali/denetim RPO ~5 dk; `V0-BKP-001`/`V0-BKP-002` `## Onay` bloklu `Done`, devir
  listesi 11 → 9). 21. dalga (`V1-RMD-094`,
  `V1-GOV-065`) KVKK saklama anonimleştirme fiili tamamlanmıştır; `audit.audit_events`
  AUD-01 append-only olduğu için `V15-KVK-002`'ye devredildi. 20. dalga (`V1-RMD-093`, `V1-GOV-063`) tamamlanmıştır; 1M sipariş satırında
  20 terminalde sipariş gönderimi p95 45.9 ms / p99 73.9 ms, sıfır deadlock.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` sıralı foundation kapısı geçmeden
  başka application görevi başlamaz.
- Kimlik, yetki, masa, sipariş, mutfak ve bill foundation testleri geçer.
- Duplicate submit ve concurrency senaryoları kanıtlanır.
- Payment UI ve gerçek fiscal akış kapalıdır.
- Audit, print queue ve yerel backup temel akışları geri kazanılabilir durumdadır.

## Modüller

`alerts`, `billing`, `cash-design`, `cashier-ui`, `catalog`, `foundation`, `governance`,
`identity-authorization`, `kitchen-printing`, `operations`, `orders`,
`reconciliation`, `remediation`, `reporting`, `security-foundation`, `settings`,
`table-management`, `waiter-pwa`.

Doğrulanan plan hacmi: 18 modül/dizin, 282 tek-sahip görev.
2026-09-01 12. dalga F bölümü modül domain incelemesi (`V1-RMD-084..085`, `V1-GOV-047`) V1-GOV-046 ile açıldı ve V1-GOV-047 ile kesin olarak mühürlendi.
2026-09-01 13. dalga PostgreSQL yedekleme/geri yükleme mekanizması (`V1-RMD-086`, `V1-GOV-049`) V1-GOV-048 ile açıldı ve V1-GOV-049 ile kesin olarak mühürlendi.
2026-09-01 14. dalga V1 go-live yük testi temel ölçümü (`V1-RMD-087`, `V1-GOV-051`) V1-GOV-050 ile açıldı ve V1-GOV-051 ile kesin olarak mühürlendi.
2026-09-01 15. dalga dağıtım altyapısı performans ayarı (`V1-RMD-088`, `V1-GOV-053`) V1-GOV-052 ile açıldı ve V1-GOV-053 ile kesin olarak mühürlendi.
2026-09-01 16. dalga orders ölçek indeks migration'ı (`V1-RMD-089`, `V1-GOV-055`) V1-GOV-054 ile açıldı ve V1-GOV-055 ile kesin olarak mühürlendi.
2026-09-01 17. dalga oturtmada sürüm toleransı (`V1-RMD-090`, `V1-GOV-057`) V1-GOV-056 ile açıldı ve V1-GOV-057 ile kesin olarak mühürlendi.
2026-09-01 18. dalga operasyonel veri housekeeping (`V1-RMD-091`, `V1-GOV-059`) V1-GOV-058 ile açıldı ve V1-GOV-059 ile kesin olarak mühürlendi.
2026-09-01 19. dalga cihaz/tarayıcı test planı ve vanilla istemci a11y smoke (`V1-RMD-092`, `V1-GOV-061`) V1-GOV-060 ile açıldı ve V1-GOV-061 ile kesin olarak mühürlendi.
2026-09-01 20. dalga yazma kritik yolu yük testi (`V1-RMD-093`, `V1-GOV-063`) V1-GOV-062 ile açıldı ve V1-GOV-063 ile kesin olarak mühürlendi.
2026-09-01 21. dalga KVKK saklama anonimleştirme fiili (`V1-RMD-094`, `V1-GOV-065`) V1-GOV-064 ile açıldı ve V1-GOV-065 ile kesin olarak mühürlendi.
2026-09-01 22. dalga WAL arşivleme / point-in-time recovery ve rakiplere göre kalibre RPO/RTO (`V1-RMD-095`, `V1-GOV-067`) V1-GOV-066 ile açıldı ve V1-GOV-067 ile kesin olarak mühürlendi.
2026-09-01 23. dalga Host-terminated HTTPS kendinden imzalı yedek sertifika (`V1-RMD-096`, `V1-GOV-069`) V1-GOV-068 ile açıldı ve V1-GOV-069 ile kesin olarak mühürlendi.
2026-09-03 24. dalga Docker arayüz/backend ayrımı A1-full (`V1-RMD-098`, `V1-GOV-070`) V1-RMD-098 ile açıldı ve V1-GOV-070 ile kesin olarak mühürlendi.
2026-09-04 25. dalga differentiated authorization (`V1-IAM-016..025`, `V1-GOV-072`) V1-IAM-016..024 ile açıldı, `V1-GOV-071` ile kaydedildi, `V1-IAM-025` sağlamlaştırma + kablolamayı tamamladı ve V1-GOV-072 ile kesin olarak mühürlendi.
2026-09-04 26. dalga grant-class bill adjustment (void/comp) + mutfak-sipariş senkronizasyonu + garson bildirimi (`V1-IAM-026` karar, `V1-SET-002`/`V1-KIT-005`/`V1-WTR-009`/`V1-ORD-005`/`V1-BIL-005`/`V1-IAM-027` uygulama görevleri, `V1-GOV-074` kapanış) `V1-GOV-072` sonrası `V1-GOV-073` ile açıldı — kapı henüz açık.
