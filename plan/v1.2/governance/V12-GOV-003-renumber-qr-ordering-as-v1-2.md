# V12-GOV-003 - Renumber QR/online ordering as V1.2, shift Payment and Customer Account back

- Task ID: V12-GOV-003
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

Semih'in açık isteğiyle sürüm sırasını kalıcı olarak değiştirmek: QR/NFC/online
sipariş kanalı ("V1.4") artık `V1.2` olarak numaralandırılır — V1.1'den hemen
sonra, ödeme/fiscal/kasa ve cari hesap/faturalama işini hiç beklemeden. Eski
`V1.2` (ödeme/fiscal/kasa/meal-card) `V1.3` olur; eski `V1.3` (cari
hesap/dönemsel faturalama/QNB) `V1.4` olur. Bu, `V12-GOV-002`'nin QR için
aldığı tek-seferlik "GATE-V13-EXIT'i bekleme" istisnasını yapısal/kalıcı hale
getirir — istisna artık gereksizdir çünkü QR zaten en baştan beklemez.

## Owned surface

- Bu görevin kendi dosyası: plan/v1.2/governance/V12-GOV-003-renumber-qr-ordering-as-v1-2.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez; aşağıdaki
  yollarda yapılan işlem, ilgili task'ların içeriğini/davranışını
  değiştirmeden yalnız sürüm/task-ID etiketini günceller (dizin taşıma,
  dosya yeniden adlandırma, metin içi etiket düzeltmesi) — sahiplik iddiası
  değildir, o yüzden geri-tik olmadan yazıldı:
  - plan/v1.2, plan/v1.3, plan/v1.4 (üç dizinin birbiriyle yer değiştirmesi;
    içindeki her dosyanın adı ve içeriğindeki task ID/gate/sürüm etiketleri)
  - plan/GATES.md, plan/README.md, plan/TRACEABILITY.md,
    plan/PDF_COVERAGE.md, plan/OFFICIAL_SOURCE_REGISTER.md,
    plan/TASK_STANDARD.md, plan/VALIDATION_CONTRACT.md (etiket düzeltmesi)
  - database/migrations klasöründe eski V14 klasörünün V12 olarak yeniden
    adlandırılması (migration numaraları 077-082 değişmedi, yalnız versiyon
    klasörü ve içindeki task-ID alt klasörleri yeniden adlandırıldı)
  - tools/plan-audit/plan_audit_tool.py (aracın kendi iç task-ID/gate
    referans tablolarının yeni etiketlerle güncellenmesi; mantık değişmedi)
  - Halihazırda Done kapanmış QR/NFC/relay modüllerinde (src/Host/DualScreen,
    src/Host/Experience/NfcOrdering, src/Host/Experience/RelaySettings,
    src/Integrations/QrRelay, src/Modules/Identity/Authorization,
    src/Modules/Orders/OrderAggregate, src/Modules/QrOrdering,
    src/Modules/Tables/TableMerge, src/Modules/Tables/TableTransfer,
    src/Clients/PosTerminal/src altında api.ts/contracts.ts/routes,
    tests altında bunların karşılıkları) yalnız task-ID doc-comment metinleri
  - docs klasöründe qr-relay-*, qr-nfc-ordering, table-reservation-policy,
    accessibility-target, lifecycle-transition-contracts,
    projection-ownership, api-contract-standard, money-tax-business-date,
    bill-order-cardinality, cash-session-design,
    customer-credit-invoice-semantics, payment-allocation-integrity,
    refund-ledger, security-verification-baseline dosyaları
  - plan/v0, plan/v1, plan/v1.1, plan/v1.5, plan/v2.0 altında bu üç sürüme
    task-ID ile çapraz referans veren dosyalar

## In scope

- Sürüm etiketlerinin (`V12`/`V13`/`V14`, `v1.2`/`v1.3`/`v1.4`) ve
  bunlara bağlı task ID önek/dosya adlarının, `GATES.md`'deki gate
  tanımlarının (giriş/çıkış bağımlılık zinciri dahil) tutarlı biçimde
  yeniden atanması.
- `GATE-V0-EXIT`'teki `V0_DEFERRED_TASKS` "Reopen stage" sütununun ilgili
  içeriğin yeni etiketiyle güncellenmesi (ayrıca `V0-YSP-001` satırındaki
  önceden var olan yanlış "Yapı Kredi" açıklaması "Yemeksepeti" olarak
  düzeltildi — bu görevle ilgisiz, ayrıca bulunmuş bir hata).

## Out of scope

- `GATE-V0/V1/V11/V15/V20-*` pozisyonel gate kimliklerinin kendisi — bunlar
  zincirdeki sabit konum etiketleridir (`V0 -> V1 -> V11 -> V12 -> V13 ->
  V14 -> V15 -> V20`), içerik taşınsa da konum adı değişmez; yalnız o
  konumun *açıklama metni* ve konuma özgü adlandırılmış gate'ler
  (`GATE-V1x-MEAL-CARD-ADAPTERS`, `GATE-V1x-FSC-STRATEGY`) taşınan içerikle
  birlikte hareket eder.
- Dated/forensic kayıtlar bilerek değiştirilmedi (o tarihte doğru olan
  etiketleri korurlar): `plan/AUDIT_REPORT.md`, `plan/AUDIT_BASELINE_MANIFEST.json`,
  `plan/AUDIT_MANIFEST.json`, `plan/REMAINING_WORK_PLAN.md`, `evidence/**`,
  `.design-sync/**`, `docs/audit/**`, `docs/performance/infra-tuning.md`,
  `docs/performance/load-baseline-v1.md`, `docs/versioning-strategy.md`
  (yalnız pozisyonel `GATE-V14-EXIT` bağımlılığı içerirler),
  `docs/data/canonical-value-catalog.md` (belirsiz wildcard kategori
  etiketleri, gerçek task ID değil).
- Migration çalışma zamanı davranışı: `database/MigrationComposition/order.json`
  ve `src/Host/Composition/Migrations/MigrationManifest.cs` yalnız numaralarla
  (077-082) çalışır, versiyon klasör adına bağlı değildir — değişiklik yok.

## Dependencies

- V12-GOV-002

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-07 ("v1.4 değil adını
v1.2 yap diğerlerini sıraya göre dosya adını değiştir").

## Deliverables

- Üç sürüm dizininin taşınması ve içindeki tüm dosyaların/task ID'lerin
  yeniden numaralandırılması.
- `GATES.md`'nin gate tanım tablosunun taşınan içerikle tutarlı yeniden
  yazılması.
- Repo genelinde eski (artık geçersiz) task ID etiketlerinin kalmadığının
  doğrulanması.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: bu değişiklikten
  kaynaklı yeni ihlal yok.
- Repo genelinde (dated/forensic kayıtlar hariç) `V1[234]-XXX-NNN` deseninde
  hiçbir geçersiz (artık var olmayan bir dosyaya karşılık gelmeyen) task ID
  kalmadığı, mevcut `plan/v1.2/**`, `plan/v1.3/**`, `plan/v1.4/**` dosya
  adlarından türetilen geçerli-kimlik kümesiyle programatik olarak
  doğrulandı.
- `dotnet build ALKAROS.slnx` ve ilgili test projeleri, migration klasör
  taşımasından etkilenmeden yeşil kalır (gerçek Docker doğrulaması bu
  görevin kapanışına eklenmiştir).

## Handoff

- None
