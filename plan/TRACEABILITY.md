# Denetim İzlenebilirliği

Bu kayıt, PDF düzeltmelerini ve plan incelemesinde kanıtlanan açıkları kesin görev
kimliklerine bağlar. PDF'nin bütün içerik birimleri `PDF_COVERAGE.md` içinde,
değişebilir dış kaynaklar `OFFICIAL_SOURCE_REGISTER.md` içinde izlenir.

## PDF Part IV düzeltmeleri

| Finding | Kanıtlanan sorun | Karar veya contract sahibi | Uygulama/doğrulama sahipleri | Durum |
| --- | --- | --- | --- | --- |
| `C1` | Migration sırası forward/cyclic foreign key riski taşıyor. | `V0-DAT-001` | `V20-MIG-001`, `V20-MIG-002` | Done |
| `C2` | Order pre-reservation durumları eksik. | `V0-DAT-002` | `V11-RSV-001`, `V14-QRO-001` | Done |
| `C3` | `account_transactions.amount` işaret kuralı tanımsız. | `V0-DOM-007` | `V13-ACC-001` | Done |
| `C4` | `payment_allocations.idempotency_key` kapsamı ve çapraz-bill bütünlüğü eksik. | `V0-DOM-004` | `V1-FND-002`, `V12-ALC-001` | Done |
| `C5` | Table status, QR `PendingConfirmation` durumunu güvenilir biçimde yansıtmıyor. | `V0-DOM-005` | `V14-QRO-002` | Done |
| `C6` | Meal-card parent/child settlement status güncellemesi atomik değil. | `V0-DAT-004` | `V12-MCD-002` | Done |
| `C7` | Polymorphic reference değer kataloğu ve kısıtları eksik. | `V0-DAT-002` | `V20-GAT-001` | Done |
| `C8` | `I.46` başlangıç lifecycle listesi 14 diyor; doğrulanmış sayı 13. | `V0-DOC-001` | `V20-GAT-001` | Done |
| `C9` | `recipe_ingredients.waste_factor` işlem sırası açık değil. | `V0-DOM-010` | `V11-PRD-002` | Done |

## Plan denetiminde eklenen açıklar

| Finding | Kanıtlanan sorun | Karar veya validation sahibi | Uygulama/doğrulama sahipleri | Durum |
| --- | --- | --- | --- | --- |
| `C10` | Fee/tip davranışı PDF'de tanımlı değil. | `V0-CMP-004` | `V1-BIL-003` | Done |
| `C11` | Purchase receipt variance ve fazla teslim politikası yok. | `V0-DOM-009` | `V11-PUR-001` | Done |
| `C12` | Stok valuation ve historical recipe cost kaynağı yok. | `V0-DOM-010` | `V11-RCP-002`, `V11-PRD-002` | Done |
| `C13` | Printer route precedence tanımlı değil. | `V0-DOM-011` | `V1-KIT-002` | Done |
| `C14` | Notification transport ve recipient matrisi yok. | `V0-ARC-006` | `V15-NOT-001` | Done |
| `C15` | OS, package ve update compatibility matrisi yok. | `V0-ARC-007` | `V20-INS-001`, `V20-INS-002` | Done |
| `C16` | Artifact signing, SBOM ve provenance sözleşmesi yok. | `V0-ARC-008` | `V20-REL-001`, `V20-GAT-002` | Done |
| `C17` | Migration rehearsal veri profili ve control total kataloğu yok. | `V0-DAT-006` | `V20-MIG-001`, `V20-MIG-002` | Done |
| `C18` | Security doğrulama seviyesi ve sürümlü requirement tabanı yok. | `V0-SEC-001` | `V15-SEC-002`, `V20-SEC-001` | Planned |
| `C19` | Accessibility conformance hedefi kararlaştırılmamış. | `V0-CMP-005` | `V1-CUI-001`, `V1-CUI-002`, `V1-CUI-003`, `V1-WTR-001`, `V1-WTR-002`, `V1-WTR-003`, `V11-UI-001`, `V11-UI-002`, `V11-UI-003`, `V12-PUI-001`, `V12-PUI-002`, `V12-PUI-003`, `V13-UI-001`, `V13-UI-002`, `V13-UI-003`, `V14-CWB-001`, `V14-CWB-002`, `V14-OUI-001`, `V20-INT-006`, `V20-UAT-001` | Done |
| `C20` | Meal-card provider'ları tek adapter/certification işine sığmıyor. | `V0-MCD-001` | `V12-MCD-003`, `V20-INT-004` | Blocked |
| `C21` | QNB public API iptal ve webhook capability'sini doğrulamıyor. | `V0-QNB-001` | `V13-QNB-005`, `V20-INT-002`, `V20-CMP-001` | Blocked |
| `C22` | QR relay production topology, transport ve deployment sahibi yoktu. | `V0-ARC-009`, `V0-QRG-001` | `V14-QRT-001`, `V20-INT-006`, `V20-INS-001` | Blocked |
| `C23` | Bill'den bağımsız cari tahsilatın durable kaynağı ve reconciliation zinciri yoktu. | `V0-DOM-007` | `V13-ACC-004`, `V13-ACC-005`, `V13-ACC-006`, `V13-ACC-007` | Done |
| `C24` | Meal-card Approved sonucu allocation ve fiscal workflow'a bağlı değildi. | `V0-MCD-001` | `V12-MCD-004`, `V12-FSC-002`, `V12-REC-001` | Blocked |
| `C25` | T300/QNB adisyon stratejileri koşullu branch yerine birlikte zorunlu tutuluyordu. | `V0-CMP-001` | `V12-FSC-003`, `V12-FSC-004`, `V12-FSC-005` | Planned |
| `C26` | CustomerAccount handler V1.3 registry ve fiscal closure zincirine kayıtlı değildi. | `V0-DOM-007` | `V13-ACC-003`, `V13-ACC-008` | Done |
| `C27` | On-hand projection reservation producer'dan önce reserved/available değerlerini sahipleniyordu. | `V0-DAT-004` | `V11-INV-002`, `V11-INV-007`, `V11-RSV-001` | Done |
| `C28` | Transaction primitive gerçek Outbox oluşmadan post-commit handoff sahipleniyordu. | `V0-ARC-003` | `V1-FND-002`, `V1-FND-005`, `V1-FND-006` | Done |
| `C29` | V0-DOM-001 lifecycle kontratı provider timeout'u örtük decline/success olarak modelliyordu (`Retry 3x, then Failed`); Unknown/ReconciliationRequired ara durumu ve ReconciliationCase bağlantısı yoktu; PDF II.5.3/II.5.4 kanonik state listeleriyle çelişiyordu. | `V0-DOM-001` | `V12-HUG-001`, `V12-HUG-002`, `V12-PAY-003`, `V12-PAY-004`, `V12-FSC-001`, `V12-REC-001` | Done |
| `C30` | Shared integration-test fixture dosyaları task-owned surface dışında kalmıştı; mevcut test kodu provenance ve tek sahiplik kanıtı olmadan kabul edilemez. | `V1-FND-010` | `V1-FND-010` | Blocked |
| `C31` | Task-scope aracı görev Markdown'ının tamamını allowlist sayıyor; görev kendi `Owned surface` alanını değiştirerek write-set'ini genişletebiliyor. | `V0-GOV-001` | `V0-GOV-001` | Done |
| `C32` | Domain write ve Outbox enqueue aynı PostgreSQL transaction'ında değildi; commit/rollback atomikliği kanıtlanamıyordu. | `V0-ARC-003` | `V1-FND-011` | Done |
| `C33` | Runtime migration manifesti, diskte karşılıklı up/down script'i olmayan position'lar içeriyordu. | `V1-FND-004` | `V1-FND-012` | Planned |
| `C34` | Paralel başarısız login denemeleri failure counter güncellemelerini kaybedebilir ve lockout eşiğini atomik uygulamayabilirdi. | `V1-IAM-001` | `V1-IAM-004` | Planned |
| `C35` | Host database parolası command line ile alınabiliyor ve process/usage çıktısına sızabilirdi. | `V1-FND-004` | `V1-SEC-003` | Planned |
| `C36` | Bağımsız denetimde plan dependency/ownership düzeltmeleri için eksik kesin remediation sahibi belirlendi. | `V0-GOV-004` | `V0-GOV-004` | Done |
| `C37` | V0 gate closure kaydı ve audit manifesti güncel task/Markdown durumundan sapmıştı. | `V0-GOV-005` | `V0-GOV-005` | Done |
| `C38` | 2026-08-03 kullanıcı onaylı plan değişikliği: V0-ARC-004'ün `V0-ARC-003` (Blocked) dependency'si kaldırıldı; V0-DOC-001 dependency'leri yalnız kapanan karar görevlerine (`V0-ARC-001`, `V0-ARC-004`, `V0-DOM-001`, `V0-DOM-002`, `V0-DOM-003`, `V0-DOM-004`, `V0-DAT-002`) daraltıldı; çıkarılan karar konuları ilgili C-row sahiplerinde (C1, C3, C5, C6, C9, C10-C19, C23, C25, C26, C27, C28, C32) izlenmeye devam eder; `VALIDATION_CONTRACT.md` heading sayısı FIND-IA-0004'e göre 375→374 düzeltildi. | `V0-ARC-004`, `V0-DOC-001` | `GATE-V0-EXIT`, V0-GOV kapanış denetimi | Done |
| `C39` | 2026-08-03 kullanıcı onaylı plan değişikliği: V1-FND zinciri `GATE-V0-EXIT`'e bağımlı olduğu için V0 kapanışını yapısal olarak kilitliyordu; `V0-GOV-010`→`V1-FND-003`, `V0-GOV-013`→`V1-SEC-002`, `V0-GOV-014`→`V1-FND-002`, `V0-GOV-015`→`V1-FND-004` dependency'leri kaldırıldı (güvenlik konuları `V1-SEC-001`/`V20-SEC-001` tüketicilerinde izlenir); `V0-ARC-009`→`V0-SEC-001`, `V0-CMP-002`→`V0-CMP-001`, `V0-CMP-004`→`V0-CMP-001` dependency'leri kaldırıldı; kaldırılan bağımlılıklar plan denetim aracının forbidden setine işlendi; görevler gerçek test kanıtıyla kapanır. | `V0-GOV-010`, `V0-GOV-013`, `V0-GOV-014`, `V0-GOV-015`, `V0-ARC-009`, `V0-CMP-002`, `V0-CMP-004` | `GATE-V0-EXIT`, V0-GOV kapanış denetimi | Done |
| `C40` | 2026-08-03 kullanıcı onaylı plan değişikliği: gerçek dış kanıt gerektiren 11 V0 görevi (`V0-HUG-001`, `V0-QNB-001`, `V0-YSP-001`, `V0-MCD-001`, `V0-PRN-001`, `V0-QRG-001`, `V0-CMP-001`, `V0-SEC-001`, `V0-LIC-001`, `V0-BKP-001`, `V0-BKP-002`) kullanıcı onayıyla `Blocked` kalır; `GATES.md` "user-approved V0 deferrals" bölümü eklendi; `GATE-V0-EXIT` koşulu ve plan denetim aracının V0 gate-open kontrolü bu listeden muaf tutar; kanıtlar ilgili aşamada (V12-V20) toplanır; devir yeni product behavior başlatma izni vermez ve V0 karar kapsamını daraltmaz. | `GATE-V0-EXIT` | `GATE-V0-EXIT`, V0-GOV kapanış denetimi | Done |
| `C41` | 2026-08-04 kullanıcı onaylı plan değişikliği: `GATE-V0-EXIT` kapanışı resmen ilan edildi; 62 V0 görevinden 51 `Done`, 11'i devir listesinde `Blocked` (kanıt V12–V20'de); kapanış kararı `evidence/v0/gate-v0-exit-closure.md`'de kayıtlı; `GATE-V1-ENTRY` kapanma koşulu sağlandı; devir yeni product behavior başlatma izni vermez ve V0 karar/uygulama kapsamını daraltmaz. | `GATE-V0-EXIT` | `GATE-V0-EXIT`, V0-GOV kapanış denetimi | Done |
| `C42` | 2026-08-04 kullanıcı onaylı plan değişikliği: bağımsız denetim bulguları (FIND-IA-0056..0061) için beş yeni remediasyon görevi eklendi — `V1-IAM-005` (login timing sözleşmesi + kararlı test), `V1-FND-013` (host DI constructability), `V1-FND-014` (retry SQL identifier), `V1-FND-015` (inbox idempotency sözleşmesi), `V0-GOV-030` (gate evidence sayım refresh). Yüzey devirleri: `PasswordHasher.cs` V1-IAM-001'den V1-IAM-005'e; `AuthenticationService.cs` + `AuthenticationServiceTests.cs` V1-IAM-004'ten V1-IAM-005'e; `src/Host/Composition/HostComposition.cs` V0-GOV-015'ten V1-FND-013'e; `RetryPolicy.cs` + `RetryPolicyTests.cs` V0-GOV-014'ten V1-FND-014'e; `IInboxHandler.cs` + `InboxMessage.cs` V1-FND-002'den V1-FND-015'e. Devirler yalnız Owned surface daraltma satırıdır; kapsam genişletmez. Remediasyon görevleri zincirden önce başlar, yalnız kanıtlanmış bulguyu düzeltir; Aşama 3 kabul sırası değişmez. | `V1-IAM-005`, `V1-FND-013`, `V1-FND-014`, `V1-FND-015`, `V0-GOV-030` | GATE-V1-ENTRY, bağımsız denetim remediasyon denetimi | Done |

`C20` için provider-specific `V12-MCD-1xx` ve `V20-INT-1xx` dosyaları,
`V0-MCD-001` legal provider code ve approved provider listesini üretmeden
oluşturulmaz. `C21` için private/partner kanıt gelmezse iptal/webhook kapsamı
uygulama iddiasına dönüşmez. `C22` için topology kararı ve gerçek non-production
relay erişimi olmadan public QR transport tamamlanmış sayılmaz.

## Sıfır-context bağımsız denetim bulguları

| Finding | Kanıtlanan sorun | Düzeltme sahibi | Sonuç |
| --- | --- | --- | --- |
| `FIND-IA-0001` | Source register parser yanlış sütunu okuyordu. | Plan audit tool | 7. sütun consumer olarak doğrulanır. |
| `FIND-IA-0002` | Finding toplamı tool içinde hard-code idi. | Plan audit tool | Toplam `AUDIT_REPORT` kaydından türetilir. |
| `FIND-IA-0003` | IV.1 correction özetleri C9 owner'larına bağlıydı. | `V0-DOC-001` | Unit-specific C1-C9 owner çözümü eklendi. |
| `FIND-IA-0004` | IV.0 ve IV.1 heading sayım dışıydı. | `V0-DOC-001` | Heading toplamı 374 oldu. |
| `FIND-IA-0005` | Tamamlanmamış/non-decision `DEC` kaynakları vardı. | Plan audit tool | Geçersiz kayıtlar kaldırıldı; validator fail-closed oldu. |
| `FIND-IA-0006` | Master correction task source kapsamı eksikti. | `V0-DOC-001` | C1-C9, II.0-II.15 ve IV.0-IV.1 eklendi. |
| `FIND-IA-0007` | Root project ve migration composition sahipsizdi. | `V1-FND-001`, `V1-FND-004` | Exact surface'ler ayrıldı. |
| `FIND-IA-0008` | Router henüz olmayan handler'ları kabul ediyordu. | `V12-PAY-002`, `V12-PAY-003` | Contract ve composition ayrıldı. |
| `FIND-IA-0009` | Koşullu N/A status/dependency ile ifade edilemiyordu. | Plan governance | Kanıtlı `NotApplicable` sözleşmesi eklendi. |
| `FIND-IA-0010` | Secret/payload protection entegrasyonlardan sonraydı. | `V1-SEC-001`, `V1-SEC-002` | İlk dış entegrasyondan önceye taşındı. |
| `FIND-IA-0011` | Table, stock ve supplier producer dependency'leri eksikti. | İlgili consumer task'ları | Kesin dependency'ler eklendi. |
| `FIND-IA-0012` | Cross-module transaction execution sahibi yoktu. | `V1-FND-005` | Tek shared transaction task'ı eklendi. |
| `FIND-IA-0013` | Trace gate release candidate'dan önce çalışabiliyordu. | `V20-GAT-001` | `V20-REL-001` dependency'si eklendi. |
| `FIND-IA-0014` | İki handoff kendi dependency'sine dönüyordu. | `V14-ONL-002`, `V14-QRO-003` | Downstream handoff düzeltildi. |
| `FIND-IA-0015` | Validator PDF package olmadan açılamıyordu. | Plan audit tool | Lazy import ve pinned environment eklendi. |
| `FIND-IA-0016` | Root Markdown lint sözleşmesi tekrarlanabilir değildi. | Plan governance | Kök lint config eklendi. |
| `FIND-IA-0017` | Refund provider Approved öncesi finalize olabiliyordu. | `V12-ALC-003`, `V12-ALC-004` | Intent ve finalization ayrıldı. |
| `FIND-IA-0018` | Approved card charge crash-safe finalize edilmiyordu. | `V12-PAY-004` | Durable settlement orchestration eklendi. |
| `FIND-IA-0019` | Payment sırasında table/bill topology sahipsizdi. | `V12-TBL-001` | Fail-closed integration task'ı eklendi. |
| `FIND-IA-0020` | Cash tender handler yoktu. | `V12-CSH-003` | Atomik cash handler eklendi. |
| `FIND-IA-0021` | Online Accepted Order ile reservation atomik değildi. | `V14-ONL-002` | Atomic acceptance ve divergence sonucu eklendi. |
| `FIND-IA-0022` | Cari bakiye tahsilat kanıtı olmadan azalabiliyordu. | `V13-ACC-004` | Approved Payment/Cash source zorunlu oldu. |
| `FIND-IA-0023` | QNB cancellation reconciliation yanlış sıradaydı. | `V13-QNB-004` | Transport dependency ve cancel cases eklendi. |
| `FIND-IA-0024` | Compliance sign-off mali karar zincirini tüketmiyordu. | `V20-CMP-001` | Decision, implementation ve UAT dependency'leri eklendi. |
| `FIND-IA-0025` | Production deployment/hypercare sahibi yoktu. | `V20-REL-004`, `V20-REL-005` | İki ayrı tek-sahip task eklendi. |
| `FIND-IA-0026` | Coverage validation-only owner ile false-positive verebiliyordu. | Plan audit tool | Task sources ve parent/owner semantiği doğrulanır. |
| `FIND-IA-0027` | Kapı zinciri `V1-FND-007` remediation görevini bloke ediyordu. | `V1-FND-007` | 2026-08-01 kullanıcı onaylı istisna; `GATES.md` ve `VALIDATION_CONTRACT.md` kaydı eklendi; zincir diğer application görevleri için değişmez. |
| `FIND-IA-0028` | CI task-scope check'i temiz worktree yüzünden her zaman geçiyordu. | `V1-FND-007` | `--diff-base` modu (`git diff <base>...HEAD`) ve PR base SHA tabanlı workflow eklendi; contract güncellendi. |
| `FIND-IA-0029` | `build/project-manifest.json` git'te yoktu; fresh clone'da plan audit kırılıyordu. | `V1-FND-007` | `.gitignore` istisnası (`build/*` + `!build/project-manifest.json`) ve dosya izlemeye alındı. |
| `FIND-IA-0030` | `ModuleComposition/Primitives/*` ve `ModuleCompositionRoot.Modules` ölü koddur. | `V1-FND-007` | Dosyalar silindi; property kaldırıldı; FND-001 yüzey daraltması plan değişikliği olarak işlendi. |
| `FIND-IA-0031` | `PsqlScriptRunner.KillProcessTree` sessiz boş catch içeriyordu. | `V1-FND-007` | Yalnız `process.HasExited` guard'ı altında no-op; diğer kill hataları fail-closed yeniden fırlatır. |
| `FIND-IA-0032` | Commit footer sözleşmesi (`Task:`/`Gate:`) ihlal ediliyordu. | `V1-FND-007` | `e2c9e3a` → `91c8672` ve SEC-002 commit'i `421add3` footer'lı yeniden yazıldı; push edilmemiş local history. |
| `FIND-IA-0033` | V0-CMP-003 evidence `Status: InProgress`, plan `Done` idi. | `V1-FND-007` | Evidence metadata `Done` düzeltildi; içerik değişmedi, tarihli not eklendi. |
| `FIND-IA-0034` | Owned surface backtick ayrıştırıcısı path olmayan metinleri allowlist'e alıyordu. | `V1-FND-007` | Yalnız path şekilli parçalar (`/`, `\`, `.`, `*`, `?`) kabul edilir; test eklendi. |
| `FIND-IA-0035` | Plan handoff↔Dependencies karşılıklılık eksikliği iddiası (38). | `V1-FND-007` | Mekanik kurallarla (naif, sender impl, aynı modül, aynı sürüm) yeniden üretilemedi; zincirler tutarlı; kurgusal düzeltme yapılmadı, kanıt `evidence/V1-FND-007/` altında. |
| `FIND-IA-0036` | Plan-audit tool Markdown sayısı 247 hard-code'u yeni görev dosyasını engelliyordu. | `V1-FND-007` | Sayı 248'e güncellendi; `VALIDATION_CONTRACT.md` ile eşitlendi; manifest yeniden üretildi. |
| `FIND-IA-0037` | Owned surface ayrıştırıcıları `-` bullet devam satırlarındaki backtick'leri sessizce düşürüyor; FND-001/004 yüzeyleri eksik parse ediliyordu; 4 sahipsiz dosya oluştu. | `V1-FND-008` | Her iki araç da devam satırlarını okur; yeni `UNOWNED_PRODUCTION_FILE` denetimi eklendi; sahipsiz dosyalar orijinal sahiplerine devredildi (FND-001: ModuleCompositionRoot.cs, ModuleBoundaries/**; FND-004: PsqlScriptRunner.cs; FND-003: test_task_scope_diff.py; V0-DOM-001: docs/versioning-strategy.md). |
| `FIND-IA-0038` | FND-007 kabul kriteri "her commit footer içerir" karşılanmıyordu: 17/23 commit footer'sız. | `V1-FND-008` | Push edilmemiş 14 commit `Task:` footer'ıyla yeniden yazıldı; push edilmiş 9 commit (fc5ae22..8374fc3) force-push kararına kadar kayıtla istisna; `Directory.Build.props` RepositoryCommit pin'i güncellendi. |
| `FIND-IA-0039` | Plan-audit tool Markdown sayısı hard-code'u (247→248) her yeni görev dosyasında yeniden kırılıyordu. | `V1-FND-008` | `verify-manifest` sayıyı diskten türetir; AUDIT_REPORT üretecindeki sabit 247 ifadesi dinamikleşti; `VALIDATION_CONTRACT.md` güncellendi. |
| `FIND-IA-0040` | `655d0b2` (V0-DOM-001) allowlist dışı `docs/versioning-strategy.md` yazmıştı ve dosya hiçbir görevde sahipsizdi. | `V1-FND-008` | Dosya V0-DOM-001 yüzeyine eklendi; tarihsel ihlal geriye dönük değiştirilmedi, kayıt düşüldü. |
| `FIND-IA-0041` | `67ebaf8` (FND-005 oturumu) FND-004 yüzeyindeki `tests/Host/MigrationComposition/**` dosyalarını değiştirdi. | `V1-FND-008` | Tarihsel ihlal; geçmişe dönük yeniden atıf yapılmadı, kayıt düşüldü. |
| `FIND-IA-0042` | `1784dc5` (FND-003 oturumu) kendi yüzeyi dışında `src/BuildingBlocks/ModuleComposition/ModuleCompositionRoot.cs` oluşturdu. | `V1-FND-008` | Tarihsel ihlal; sahiplik FND-001'e devredildi, kayıt düşüldü. |
| `FIND-IA-0043` | `src/BuildingBlocks/ModuleComposition/ALKAROS.ModuleComposition.csproj` ve `src/`, `tests/` altındaki 23 `packages.lock.json` sahipsizdi (restore-locked artefaktları). | `V1-FND-008` | FND-001 yüzeyine `src/BuildingBlocks/**/ALKAROS.*.csproj`, `src/**/packages.lock.json`, `tests/**/packages.lock.json` pattern'leri eklendi. |
| `FIND-IA-0044` | Merkezi governance dosyaları (AGENTS.md, .markdownlint-cli2.jsonc, plan/*.md, plan/*.json, plan/*.lock, README'ler, evidence/v0/gate-v0-exit-closure.md) hiçbir görev yüzeyinde yok. | `V1-FND-008` | Orphan denetimi yalnız `src/`/`tests/`/`database/` izler; governance dosyaları sınıflandırıldı — bunlara dokunuş, mevcut uygulamadaki gibi açık plan değişikliği yüzey listesi gerektirir. |
| `FIND-IA-0045` | `a721e81` (V1-FND-005) `Directory.Build.props` pin'ini güncelledi; dosya FND-001 root-build yüzeyinde. | `V1-FND-008` | Tarihsel scope ihlali; geçmişe dönük değiştirilmedi. Pin, footer'lı yeniden yazım sonrası 97e9cf0'a güncellendi. |
| `FIND-IA-0046` | `41495d6` (V1-FND-005) `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json` yeniden üretti; dosyalar FND-007 yüzeyinde. | `V1-FND-008` | Tarihsel scope ihlali; geçmişe dönük değiştirilmedi, kayıt düşüldü. |
| `FIND-IA-0047` | `72cafee` (V1-FND-004) `.github/workflows/task-scope.yml` default task-id'sini değiştirdi; dosya FND-003 yüzeyinde. | `V1-FND-008` | Tarihsel scope ihlali; geçmişe dönük değiştirilmedi, kayıt düşüldü. |
| `FIND-IA-0048` | `47f0a64` (V1-FND-005) transaction testlerini `ALKAROS.slnx` içine kaydetti; dosya FND-001 yüzeyinde. | `V1-FND-008` | Tarihsel scope ihlali; geçmişe dönük değiştirilmedi, kayıt düşüldü. |
| `FIND-IA-0049` | `3a39b62` (V1-FND-004) `src/Host/Composition/Migrations/**` dosyaları, eski devam satırı formatı yüzünden yüzeyde görünmüyordu. | `V1-FND-008` | Yüzey devam satırı ayrıştırıcı düzeltmesi + ayrı bullet formatıyla giderildi; sınır denetimi tekrar çalıştırıldığında VIOLATION kalmadı. |
| `FIND-IA-0050` | `fc5ae22` kök baseline commit'i konvansiyon öncesi; hiçbir görev yüzeyine atfedilemez, footer'sız kaldı. | `V1-FND-009` | 2026-08-01 kullanıcı onaylı ("DÜZELT") push edilmiş geçmiş düzeltmesi kapsamında kayıtlı istisna; kurgusal Task ID atfedilmedi; `GATES.md` ve `VALIDATION_CONTRACT.md`'ye üçüncü zincir istisnası kaydı eklendi. |
| `FIND-IA-0051` | `1784dc5`/`8374fc3` (V1-FND-003 oturumu, yeniden yazım sonrası `0c37dc6`/`36c06cf`) yüzey dışı batch artefaktları: `0c37dc6` 239 yol (sln/slnx, global.json, nuget.config, docs/**, evidence/v0-* completion kanıtları, plan/v0/** vb.), `36c06cf` `.gitignore` + `tmp/**` (268 dosya). | `V1-FND-009` | Geçmiş bölünmedi; commit'ler `Task: V1-FND-003` footer'ı taşır, yüzey dışı yollar tam geçmiş denetiminde kayıtlı VIOLATION olarak düşüldü (2026-08-01, `evidence/V1-FND-009/boundary-audit-25.txt`). |
| `FIND-IA-0052` | Plan validator yalnız dependency kimliği ve cycle kontrolü yapıyor; `Done` bir task'ın açık direct veya transitive dependency'sini reddetmiyordu. | `V0-GOV-017` | Direct/transitive status-dependency denetimi fail-closed eklendi; geçersiz tarihsel `Done` kayıtları V0-GOV-018 ile candidate evidence olarak geri alınır. |
| `FIND-IA-0053` | Geçmiş Git/application ağacı `V1-FND-001` `Done` olmadan doğrudan hata sayılıyor; aday kanıt ile yeni application başlangıcı ayrışmıyordu. | `V0-GOV-021` | Ağaç candidate evidence kabul edilir; V0 açıkken yeni `implementation`/`integration` `InProgress` durumu fail-closed reddedilir. |
| `FIND-IA-0054` | `Blocked` görev, standardın zorunlu tuttuğu `Blocker` bölümünü silmeden executable statüye geçemiyor; task-scope bu zorunlu silmeyi reddediyordu. | `V0-GOV-022` | Yalnız legal `Blocked` status geçişindeki eksiksiz `Blocker` ekleme/silme işlemi izinli; diğer task gövdesi değişikliği fail-closed kalır. |
| `FIND-IA-0055` | Mevcut candidate-code kusurları düzeltilemeden açık V0 dependency zincirinde kilitli kalıyordu. | `V0-GOV-028` | Yalnız kayıtlı kimlikler için `--candidate-remediation` modu eklendi; allowlist sabit kalır, dependency/gate kapanışı ve yeni davranış üretilmez. |
| `FIND-IA-0056` | `UnknownUsernameLoginTakesComparableTimeToKnownUserLogin` tam solution koşusunda kararsız (stopwatch + canlı DB; bilinen yanlış parola yolunda ekstra failure-counter yazımı; 5 koşuda 1 flake). | `V1-IAM-005` | Stopwatch tabanlı eşik testi kaldırılır; deterministik sözleşme testi; tam set ardışık koşulda flake'siz. |
| `FIND-IA-0057` | Username timing koruması eşit iş garantilemiyor: unknown yolu sabit 600k dummy PBKDF2, bilinen yanlış parola yolu gerçek hash iteration'ı (10k–2M) + ekstra DB UPDATE. | `V1-IAM-005` | Work-factor yakınsama (rehash-on-login) + yazılı güvenlik sözleşmesi + deterministik kanıt. |
| `FIND-IA-0058` | Host "fail-closed constructability" iddiası kanıtsız: `BuildServiceProvider()` kayıtlı graph'ı doğrulamıyor. | `V1-FND-013` | Kayıtlı her servisin constructor graph'ı build'de doğrulanır; kırık graph fail-closed reddedilir. |
| `FIND-IA-0059` | `RetryPolicy.RecordFailureAsync` serbest `tableName`'i SQL'e interpolate ediyor. | `V1-FND-014` | Yalnız kayıtlı sabit tablo kimlikleri; serbest string fail-closed reddedilir. |
| `FIND-IA-0060` | Inbox handler sözleşmesi idempotency zorunlu kılmıyor; lease expiry sonrası yeniden işleme çift etki riski. | `V1-FND-015` | Handler sözleşmesi tekrar-teslimde çift etkiyi yasaklar; contract testleri. |
| `FIND-IA-0061` | GATE-V0-EXIT evidence sayımı güncel değil: 62/51/11 yazıyor, gerçek 66/55/11. | `V0-GOV-030` | Sayım güncel durumdan yeniden üretilir; 51/62 sayımı tarihsel hata kaydına işlenir. |

## Yapısal denetim bulguları

| Finding | Kanıt | Düzeltme sahibi | Sonuç |
| --- | --- | --- | --- |
| `FIND-PDF-001` | Sayfa 2 haritası `II.0-II.16`; içerik `II.15` ile bitiyor. | `V0-DOC-001` | `II.16` oluşturulmadı. |
| `FIND-SCHEMA-001` | Başlangıç görevlerinde `Surface state` ve kesin kaynak biçimi yoktu. | Plan denetimi | 228 görev ortak sözleşmeye taşındı. |
| `FIND-SOURCE-001` | 145 görevde `Source basis` yoktu. | Plan denetimi | Her görevde en az bir `PDF`, `CORR`, `EXT` veya `DEC` kaydı var. |
| `FIND-DEPENDENCY-001` | Sekiz dependency serbest gate metniydi. | Plan denetimi | Sabit `GATE-*` kimliklerine dönüştürüldü. |
| `FIND-HANDOFF-001` | Otuz handoff çözümlenemeyen geniş kapsam içeriyordu. | Plan denetimi | Kesin task/gate kimliği veya `None` kullanıldı. |
| `FIND-SURFACE-001` | 425 ilk yüzey planlanan yol olduğu halde mevcut gibi okunabiliyordu. | `V1-FND-001` | Bütün task yüzeyleri açık `Planned` durumunda. |
| `FIND-LANGUAGE-001` | En az 140 Goal bütünüyle English idi. | Plan denetimi | Görev anlatımları Türkçeleştirildi; teknik terimler English bırakıldı. |
| `FIND-DELIVERABLE-001` | 126 görev genel “production implementation” teslimatı taşıyordu. | Plan denetimi | Teslimatlar task-specific artifact ve test yüzeyine bağlandı. |

Bu tablodaki “sonuç” plan belgesi düzeltmesinin durumudur; hiçbir satır
application code'un uygulandığını veya gerçek entegrasyonun geçtiğini iddia etmez.
