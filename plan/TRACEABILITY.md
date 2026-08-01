# Denetim İzlenebilirliği

Bu kayıt, PDF düzeltmelerini ve plan incelemesinde kanıtlanan açıkları kesin görev
kimliklerine bağlar. PDF'nin bütün içerik birimleri `PDF_COVERAGE.md` içinde,
değişebilir dış kaynaklar `OFFICIAL_SOURCE_REGISTER.md` içinde izlenir.

## PDF Part IV düzeltmeleri

| Finding | Kanıtlanan sorun | Karar veya contract sahibi | Uygulama/doğrulama sahipleri | Durum |
| --- | --- | --- | --- | --- |
| `C1` | Migration sırası forward/cyclic foreign key riski taşıyor. | `V0-DAT-001` | `V20-MIG-001`, `V20-MIG-002` | Planned |
| `C2` | Order pre-reservation durumları eksik. | `V0-DAT-002` | `V11-RSV-001`, `V14-QRO-001` | Planned |
| `C3` | `account_transactions.amount` işaret kuralı tanımsız. | `V0-DOM-007` | `V13-ACC-001` | Planned |
| `C4` | `payment_allocations.idempotency_key` kapsamı ve çapraz-bill bütünlüğü eksik. | `V0-DOM-004` | `V1-FND-002`, `V12-ALC-001` | Planned |
| `C5` | Table status, QR `PendingConfirmation` durumunu güvenilir biçimde yansıtmıyor. | `V0-DOM-005` | `V14-QRO-002` | Planned |
| `C6` | Meal-card parent/child settlement status güncellemesi atomik değil. | `V0-DAT-004` | `V12-MCD-002` | Planned |
| `C7` | Polymorphic reference değer kataloğu ve kısıtları eksik. | `V0-DAT-002` | `V20-GAT-001` | Planned |
| `C8` | `I.46` başlangıç lifecycle listesi 14 diyor; doğrulanmış sayı 13. | `V0-DOC-001` | `V20-GAT-001` | Planned |
| `C9` | `recipe_ingredients.waste_factor` işlem sırası açık değil. | `V0-DOM-010` | `V11-PRD-002` | Planned |

## Plan denetiminde eklenen açıklar

| Finding | Kanıtlanan sorun | Karar veya validation sahibi | Uygulama/doğrulama sahipleri | Durum |
| --- | --- | --- | --- | --- |
| `C10` | Fee/tip davranışı PDF'de tanımlı değil. | `V0-CMP-004` | `V1-BIL-003` | Planned |
| `C11` | Purchase receipt variance ve fazla teslim politikası yok. | `V0-DOM-009` | `V11-PUR-001` | Planned |
| `C12` | Stok valuation ve historical recipe cost kaynağı yok. | `V0-DOM-010` | `V11-RCP-002`, `V11-PRD-002` | Planned |
| `C13` | Printer route precedence tanımlı değil. | `V0-DOM-011` | `V1-KIT-002` | Planned |
| `C14` | Notification transport ve recipient matrisi yok. | `V0-ARC-006` | `V15-NOT-001` | Planned |
| `C15` | OS, package ve update compatibility matrisi yok. | `V0-ARC-007` | `V20-INS-001`, `V20-INS-002` | Planned |
| `C16` | Artifact signing, SBOM ve provenance sözleşmesi yok. | `V0-ARC-008` | `V20-REL-001`, `V20-GAT-002` | Planned |
| `C17` | Migration rehearsal veri profili ve control total kataloğu yok. | `V0-DAT-006` | `V20-MIG-001`, `V20-MIG-002` | Planned |
| `C18` | Security doğrulama seviyesi ve sürümlü requirement tabanı yok. | `V0-SEC-001` | `V15-SEC-002`, `V20-SEC-001` | Planned |
| `C19` | Accessibility conformance hedefi kararlaştırılmamış. | `V0-CMP-005` | `V1-CUI-001`, `V1-CUI-002`, `V1-CUI-003`, `V1-WTR-001`, `V1-WTR-002`, `V1-WTR-003`, `V11-UI-001`, `V11-UI-002`, `V11-UI-003`, `V12-PUI-001`, `V12-PUI-002`, `V12-PUI-003`, `V13-UI-001`, `V13-UI-002`, `V13-UI-003`, `V14-CWB-001`, `V14-CWB-002`, `V14-OUI-001`, `V20-INT-006`, `V20-UAT-001` | Planned |
| `C20` | Meal-card provider'ları tek adapter/certification işine sığmıyor. | `V0-MCD-001` | `V12-MCD-003`, `V20-INT-004` | Blocked |
| `C21` | QNB public API iptal ve webhook capability'sini doğrulamıyor. | `V0-QNB-001` | `V13-QNB-005`, `V20-INT-002`, `V20-CMP-001` | Blocked |
| `C22` | QR relay production topology, transport ve deployment sahibi yoktu. | `V0-ARC-009`, `V0-QRG-001` | `V14-QRT-001`, `V20-INT-006`, `V20-INS-001` | Blocked |
| `C23` | Bill'den bağımsız cari tahsilatın durable kaynağı ve reconciliation zinciri yoktu. | `V0-DOM-007` | `V13-ACC-004`, `V13-ACC-005`, `V13-ACC-006`, `V13-ACC-007` | Planned |
| `C24` | Meal-card Approved sonucu allocation ve fiscal workflow'a bağlı değildi. | `V0-MCD-001` | `V12-MCD-004`, `V12-FSC-002`, `V12-REC-001` | Blocked |
| `C25` | T300/QNB adisyon stratejileri koşullu branch yerine birlikte zorunlu tutuluyordu. | `V0-CMP-001` | `V12-FSC-003`, `V12-FSC-004`, `V12-FSC-005` | Planned |
| `C26` | CustomerAccount handler V1.3 registry ve fiscal closure zincirine kayıtlı değildi. | `V0-DOM-007` | `V13-ACC-003`, `V13-ACC-008` | Planned |
| `C27` | On-hand projection reservation producer'dan önce reserved/available değerlerini sahipleniyordu. | `V0-DAT-004` | `V11-INV-002`, `V11-INV-007`, `V11-RSV-001` | Planned |
| `C28` | Transaction primitive gerçek Outbox oluşmadan post-commit handoff sahipleniyordu. | `V0-ARC-003` | `V1-FND-002`, `V1-FND-005`, `V1-FND-006` | Planned |

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
