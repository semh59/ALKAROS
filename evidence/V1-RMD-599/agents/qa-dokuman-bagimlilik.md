# V1-RMD-599 Host (platform katmani) - qa-dokuman-bagimlilik raporu
HEAD: d1dc86b6. Salt okunur; build/test yeniden kosulmadi, evidence/V1-RMD-599/runs kayitlari kullanildi. Kanit duzeyi aksi belirtilmedikce "Kod okumasi". Kritik/Yuksek bulgu yok.

## 1. Kapsam envanteri ve kontrol sayisi
Komut: `wc -l evidence/V1-RMD-599/baseline/scope-files.txt` -> 53 (5 migration SQL + 48 src/Host dosyasi).
- 53/53 kalem kontrol edildi (borc kalibi grep, docs atif varligi, SQL yazma semasi esleme, test varligi, dokuman iddia eslemesi). Genis okuma: Program.cs, DualScreenOptions/Tls/Token, Store.Display, Endpoints ilgili bolumler, ApiExceptionHandler, MigrationManifest/Discoverer, WebPushExperience/Crypto. Kalanlar hedefli grep ile sinandi.
- Dokumanlar: architecture/{dual-screen-pos-topology, notification-delivery-matrix, api-contract-standard, settings-ownership}.md, data/{migration-rehearsal-profile, migration-dependency-graph}.md, compliance/kvkk-retention-runbook.md, operations/{data-housekeeping, production-go-live-checklist}.md, CONSISTENCY_AUDIT.md, design/foundations.md (WebPush), deploy/docker/{README.md,Caddyfile}.
- Done gorevler: 6 (V1-RMD-005, V1-RMD-006, V1-WTR-011, V1-WTR-013, V1-WTR-014, V1-CDP-001).
- Bagimlilik: Host.csproj (1 PackageReference, 33 ProjectReference), Directory.Packages.props (10 PackageVersion), src/Host/packages.lock.json (10 paket), NuGet.config; npm: PosTerminal node_modules (363 package.json), tests/E2E/Cashier ve WaiterPwa (16 sar).

## 2. Bulgu tablosu
| No | Onem | Kanit | Sorun | Etki | Onerilen duzeltme |
|---|---|---|---|---|---|
| D-01 | Orta | docs/architecture/dual-screen-pos-topology.md:210 "Mevcut planda customer-display server projection ... uygulama gorevleri yoktur"; kod src/Host/DualScreen/**; plan V1-RMD-006 Done | Doküman sapmasi: karar kaydi uygulanmis ozelligi "yok / kod yazilamaz" diye anlatiyor (:199 "V1-RMD-004 Mock kalir" da). SKILL de yok. | Okuyan ozelligin olmadigini sanir. | Kararin sonuna "V1-RMD-006 ile uygulandi" notu / tarihsel ibaresi. |
| D-02 | Orta | dual-screen-pos-topology.md:87 customer-display:read; `grep -rn "customer-display:read" src --include=*.cs` -> 0 sonuc | Doküman sapmasi: dokumandaki yetki kodu kodda yok; yetki cerez oturumu (DualScreenApplication.cs:93 alkaros.customer-display) + rota izin listesiyle saglaniyor. | V20-SEC-001 girdisi yanlis yetki adini arar. | Dokumani koda uydur. |
| D-03 | Dusuk | topology.md:63 "yalniz displayId, orderId, revision ve degisiklik turu"; DualScreenApplication.Endpoints.cs:600 `new { orderId, revision, kind = "OrderChanged" },`; DualScreenApplication.Payments.cs:87 yalniz kind | Doküman sapmasi: SignalR yukunde displayId yok, bazilari yalniz kind. | Istemci kontrati yanlis okunur. | Dokumani guncelle. |
| D-04 | Dusuk | topology.md:86 "HttpOnly, Secure, SameSite=Strict"; Endpoints.cs:612 `Secure = context.Request.IsHttps,` | Doküman sapmasi: Secure kosullu (dev loopback HTTP). Uretimde HTTPS zorunlu (DualScreenApplication.cs:467). | Dusuk. | Dokumana kosul notu. |
| D-05 | Dusuk | api-contract-standard.md:60 docs/engineering/error-code-catalogue.md; `ls docs/engineering` -> dosya yok | Olmayan dosyaya atif. Fiili katalog src/Host/Composition/Errors/ApiErrorCatalog.cs. | Hata kodu dokuman kaynagi yok. | Atifi koda cevir veya katalog uret. |
| D-06 | Dusuk | api-contract-standard.md:98 CONCURRENT_MODIFICATION; ApiErrorCatalog.cs:127 "CONCURRENCY_CONFLICT"; `grep -rl CONCURRENT_MODIFICATION src --include=*.cs` -> 0 | Doküman sapmasi (api-endpoint SKILL kod adini yaziyor). Ayrica istemci testleri sunucunun uretmedigi kodu taklit ediyor: src/Clients/PosTerminal/src/features/billing/billingApi.test.ts, kitchenApi.test.ts, KitchenOperationsWorkspace.test.tsx. | Gercek 409 dali istemcide sinanmamis olabilir (istemci esleme dilim disi, dogrulanamadi). | Dokuman + test fixture hizala. |
| D-07 | Dusuk | api-contract-standard.md:18 "C# / .NET 10 LTS, EF Core 10"; Directory.Build.props net8.0; `grep -rIl EntityFramework src` -> 0 | Doküman sapmasi: .NET 10/EF Core yok (FluentValidation sapmasi bilinen: api-endpoint SKILL; .NET/EF SKILL de listeli degil). | Yanlis beklenti. | SKILL sapma listesine ekle. |
| D-08 | Dusuk | kvkk-retention-runbook.md:101 ozet satiri apply=false ile bitiyor; Program.cs:444 `+ $"blocked={blocked} failed={failed}");` | Doküman sapmasi: blocked= ve failed= alanlari dokumanda yok; failed>0 ise exception (Program.cs:445). | Log ayristirma eksik. | Runbook guncelle. |
| D-09 | Dusuk | Program.cs:18 "Exit codes: 0 = success, 1 = migration execution failed, 2 = startup or"; HostComposition.cs:17 `CompletedWithWarnings = 3,` | Kod yorumu eski (3 eksik). | Yaniltici yorum. | Yorumu guncelle. |
| D-10 | Dusuk | Program.cs:694 "Verbs: serve | provision-manager ..." (watchdog yok; audit-disposal --operator yok, oysa Program.cs:469 case "--operator" var); `grep -rl "OutageWatchdog" docs` -> 0 | Kullanim metni watchdog fiilini ve --operator bayragini icermiyor; watchdog icin docs altinda belge yok (yalniz plan/V1-RMD-585, compose.yaml:203). | Operator ozelligi bulamaz. | PrintUsage + kisa operasyon notu. |
| D-11 | Dusuk | database/MigrationComposition/order.json:12 "max": "139"; MigrationManifest.cs:40 `PhaseBMax = "197";` | order.json phaseBRange.max 139 (kod alani okumuyor, sabit kullaniyor). alkaros-database-migration SKILL "birlikte guncellenir" diyor; tekrarlayan kusur (docs/engineering/garson-audit-2026-09-10.md:111). | Olu ve yaniltici meta veri. | 197 yap + esitlik testi veya alani sil. |
| D-12 | Dusuk | docs/data/migration-dependency-graph.md:19 "004 stores, 005 users, 006 roles", :158 "Phase B (migrations 031-040)"; order.json da 004 yok, 006=catalog, 008=roles; `grep -rl "CREATE TABLE.*stores" database/migrations` -> 0 | Doküman sapmasi: pozisyonlar ve Phase B araligi eski. | Graf gercek sirayi yansitmiyor. | Tarihsel notu / guncelle. |
| D-13 | Dusuk | migration-rehearsal-profile.md:43 "replaces two trigger functions"; 195-authorization-guards.up.sql:60 `CREATE TRIGGER trg_authorization_delegations_transition` | Kucuk doküman sapmasi: delegations icin yeni tetikleyici yaratiliyor. 173/194/197 maddeleri kodla uyumlu; profil guncel sayilir. | Dusuk. | Cumleyi duzelt. |
| D-14 | Dusuk | settings-ownership.md:66 "Making history rows immutable in the database is a separate migration task."; 197-settings-history-immutable.up.sql:30 `CREATE TRIGGER trg_setting_history_immutable` | Doküman sapmasi: is migration 197 ile bitmis. Anahtar tablosu (9 garson anahtari dahil) kodla uyumlu. | Eski cumle. | Guncelle. |
| D-15 | Dusuk | notification-delivery-matrix.md:15-22; `grep -rn -i "quiet|escalat" src/Host/Experience/WaiterNotifications src/Host/Experience/WebPush src/Host/Experience/HelpRequests` -> 0; V15-NOT-001 Status: Planned | Matris Done karar kaydi ama Host uygulamiyor (gelecek gorev). Sapma degil, mevcut davranis gibi okunabilir. | Yanlis beklenti. | "Uygulama: V15-NOT-001" notu. |
| D-16 | Dusuk | src/Host/ALKAROS.Host.csproj:11 tek PackageReference (DI); `using Npgsql;` Program.cs:10 ve 20+ dosya; src/Host/packages.lock.json Npgsql "type": "CentralTransitive" | Host dogrudan Npgsql kullaniyor, dogrudan referansi yok (gecisli). DI PackageReference FrameworkReference ile gereksiz olabilir (dogrulanamadi, restore denenmedi). | Gecisli bagimlilik kaybolursa derleme kirilir. | Npgsql acikca ekle; DI ihtiyacini dogrula. |
| D-17 | Dusuk | third-party-license-policy.md:22 (bilinen: V1-RMD-596) | Bilinen: makine okunur lisans envanteri yok. Host lock 10/10 paket nuspec ten dogrulandi: Microsoft.* MIT, Npgsql 9.0.2 PostgreSQL. | Yalniz bilinen aciklik. | V1-RMD-596. |
| D-18 | Dusuk | src/Host/Experience/WaiterNotifications/SignalRCashierQueueAnnouncer.cs:12; `grep -rl "SignalRCashierQueueAnnouncer" tests --include=*.cs` -> 0 | Dogrudan test yok (arayuz ICashierQueueAnnouncer yalniz tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs te). | Best-effort yol sinanmamis. | Birim test. |

### Done gorev denetimi
| Gorev | Owned surface yollari | Kabul testi | evidence/ | Sonuc |
|---|---|---|---|---|
| V1-RMD-005 | var | n/a (karar) | evidence/V1-RMD-005/validation.md | Temiz (D-01 haric) |
| V1-RMD-006 | var | tests/Host/MigrationComposition/DualScreen/* | evidence/V1-RMD-006/browser-e2e.md, validation.md | Temiz |
| V1-WTR-011 | src/Host/Experience/WebPush/** var | tests/Host/Experience/WebPush (9 dosya) | yok; tarihsel (Done 2026-09-11, TASK_STANDARD 2026-09-28 kurali oncesi) | Temiz/tarihsel |
| V1-WTR-013 | var | handoff/PushSubscription testleri var | yok; tarihsel | Temiz/tarihsel |
| V1-WTR-014 | var | tests/Host/Experience/HelpRequests | yok; tarihsel | Temiz/tarihsel |
| V1-CDP-001 | var; Screensaver.cs ve test V1-CDP-004 e devredilmis | tests/Host/MigrationComposition/DualScreen/DualScreenScreensaverTests.cs | yok; tarihsel | Temiz/tarihsel |

## 3. Temiz bulunan alanlar
- Borc kalibi: grep -rn -E "TODO|NotImplementedException|catch\s*\{\s*\}|#pragma warning disable|SuppressMessage" src/Host --include=*.cs -> 0; Skip = tests/Host -> 0.
- Host kaynaklarindaki tum docs/... atiflari mevcut dosyalara gidiyor (0 eksik).
- Sema sahipligi: CONSISTENCY_AUDIT.md Host cross-schema satiri koda uyuyor (WebPush/HelpRequests -> notifications, DualScreen -> customer_display + belgeli table_mgmt istisnasi).
- CLI/operasyon: data-housekeeping.md, kvkk-retention-runbook.md bayraklari, deploy/docker/README.md bayraklari (DualScreenOptions.cs:174-225, Dockerfile:123-129), compose.ops.yaml komutu, HTTPS_REQUIRED (DualScreenApplication.cs:472), 8444 geri cekilmesi (README, go-live :66,:202) kodla uyumlu.
- DualScreen karari dogrulananlar: pairing 2 dk (DualScreenStore.cs:18), 5 hatali deneme (DualScreenStore.Display.cs:82), HttpOnly+SameSite=Strict, /api no-store (DualScreenApplication.cs:669), CSP, display origin izolasyonu.
- WebPush: "ek paket olmadan" (foundations.md) -> WebPushCrypto.cs:10-17 yalniz .NET primitifleri.
- Bagimlilik: merkezi surum, RestoreLockedMode=true, Host lock props ile uyumlu; runs/exit-codes.txt te Host.Errors, Host.MigrationComposition, Architecture.* exit=0 (dolayli kanit). Plansiz/kullanilmayan paket Host ta yok. FluentAssertions 6.12.0 pin politikayla uyumlu.
- Lisans: npm (PosTerminal 363, E2E 16+16): yalniz MIT/Apache-2.0/ISC/BSD/MIT-0/Unlicense/MPL-2.0; GPL/AGPL/SSPL/lisanssiz 0. MPL-2.0 yalniz axe-core 4.10.3 ve lightningcss 1.33.0 (politika ile birebir). Politika dosyasi var (docs/engineering/third-party-license-policy.md).
- order.json son girdi 197 = diskteki son migration; ID boslugu yalniz 004.

## 4. Dogrulanamayanlar
- Ag gerektiren surum guncelligi taramasi: doğrulanamadi (ag). Zafiyet taramasi qa-guvenlik-yetki de.
- Diger projelerin NuGet lock lisanslari bu dilimde degil.
- pnpm node_modules taramasi yalniz gezilebilen package.json lar; pnpm-lock.yaml ile capraz kontrol yapilmadi.
- D-16 DI referansinin gereksizligi: restore denenmedi.
- runs/test-tests-Host-Observability.log exit=1: neden incelenmedi (altyapi/filtre olabilir), doğrulanamadi.
- plan_audit validate calistirilmadi.
