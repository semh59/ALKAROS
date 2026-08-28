# V1 Production Experience Decision

## Decision metadata

- Decision ID: V1-GOV-009
- Date: 2026-08-25
- Status: Accepted
- Approver: Semih, Product Owner
- Product basis: `PO:2026-08-25`
- Repository candidate: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Decision owner: `/root/gov009_product_experience`

## Decision

Mevcut `PosTerminal`, V1 ürün deneyimi olarak kabul edilmez. O yüzey; login, ürün okuma, walk-in sipariş ve müşteri
ekranı eşleştirmesini gerçek Host üzerinde çalıştıran değerli ama dar bir production dikey dilimdir. Masa yönetimi,
masa bazlı sipariş, mutfak/operasyon ve menü/katalog yönetimi bulunmadan rekabetçi bir restoran ürünü veya V1'in
tam kullanıcı yüzeyi olarak sunulamaz.

V1 için seçilen yön, tek bir role-aware production shell içinde şu çalışma alanlarını yayınlamaktır:

1. `Kasa`: hızlı satış, aktif sipariş ve müşteri ekranı.
2. `Masalar`: bölge bazlı masa planı, masa durumu ve masa işlemleri.
3. `Siparişler`: masa/sipariş ayrıntısı ve mutfak ilerlemesi.
4. `Mutfak & Operasyon`: ticket, istasyon, yazıcı/reprint ve sistem sağlık görünümü.
5. `Menü & Katalog`: kategori, vergi, ürün, modifier ve effective price yönetimi.

Cashier terminal, manager desktop/tablet, kitchen display ve customer display aynı uygulama sözleşmesini kullanır;
fakat rol, izin ve cihaz sınırlarıyla farklı çalışma alanları görür. `WebPrototype` hiçbir koşulda production yüzeyi,
production contract kanıtı veya shipping asset'i değildir.

## Sources

### Binding sources

- `PO:2026-08-25`: Semih mevcut arayüzü V1 ve rekabetçi kalite için reddetti; masa yönetimi ile menü ekleme dahil
  gerçek ürün kapsamının production yüzeylerinde bulunmasını istedi.
- Repository contract ve source inventory, 2026-08-25 tarihinde aşağıdaki candidate/worktree üzerinde incelendi.

### Benchmark-only sources

Bu kaynaklar ALKAROS davranışı üretmez; yalnız güncel restoran ürünü etkileşim seviyesini karşılaştırmak için
kullanılmıştır.

- Square Support, [Create a floor plan](https://squareup.com/help/us/en/article/6427-building-your-floor-plan),
  erişim 2026-08-25. Bölge ve masa haritasını servis akışıyla, masa birleştirmeyi POS kullanımıyla ilişkilendirir.
- Square Support, [Create and update menus](https://squareup.com/help/us/en/article/6424-create-menus-with-square-for-restaurants),
  erişim 2026-08-25. Menü, kategori ve ürünün farklı sorumluluklarını; kanal, konum ve zaman görünürlüğünü ayırır.
- Square Support, [Create and edit items](https://squareup.com/help/us/en/article/8335-create-and-edit-items),
  erişim 2026-08-25. Ürün oluşturma/düzenlemenin yetki, kategori, fiyat, SKU, modifier ve konum bağlamıyla tek bir
  yönetim akışı olması gerektiğini gösterir.

## Repository capability inventory

Sınıflandırma anlamları:

- `production UI`: shipping istemcide gerçek Host/contract ile erişilebilir görsel akış.
- `production headless`: production C# domain/repository/engine kodu var, fakat görsel yüzey veya production HTTP
  erişimi yok.
- `mock-only`: yalnız `WebPrototype` içindeki yerel sahte veri ve davranış.
- `missing`: repository'de doğrulanmış domain/transport/UI sözleşmesi yok.

| V1 capability | Classification | File and endpoint evidence | Consequence |
| --- | --- | --- | --- |
| Login, cashier session | production UI | `src/Clients/PosTerminal/src/api.ts:61-70`; `POST /api/v1/auth/login`, `GET /api/v1/auth/session`, `POST /api/v1/auth/logout` in `src/Host/DualScreen/DualScreenApplication.cs:214-264` | Gerçek giriş var; role-aware shell yok. |
| Walk-in catalog browse | production UI | `src/Clients/PosTerminal/src/App.tsx:349-427`; `GET /api/v1/terminals/{terminalId}/catalog` at `DualScreenApplication.cs:266-281` | Katalog yalnız satış için okunur; yönetilemez. |
| Walk-in draft/order submit | production UI | `src/Clients/PosTerminal/src/api.ts:76-115`; order endpoints at `DualScreenApplication.cs:284-382` | Masa kimliği ve masa çalışma alanı bu akışta yok. |
| Customer display pairing/snapshot/revoke | production UI | `src/Clients/PosTerminal/src/api.ts:116-138`; pairing/snapshot endpoints at `DualScreenApplication.cs:385-442`; SignalR hub at line 147 | Ayrı display storage/DTO sınırı var; V1 shell'in bir parçası olarak korunur. |
| Cashier table/session shell | production headless | `src/Clients/Cashier/TableShell/CashierShellEngine.cs:7-132`; `src/Clients/Cashier/ALKAROS.Cashier.csproj` class library | Renderer, browser entry point, transport adapter ve Host endpoint yok. |
| Cashier table order entry | production headless | `src/Clients/Cashier/OrderEntry/OrderEntryEngine.cs:7-115` | Draft engine var; production UI veya server adapter yok. |
| Cashier operations status | production headless | `src/Clients/Cashier/OperationsStatus/OperationsStatusEngine.cs:6-68` | Liste/reprint validation engine'i var; production UI ve gerçek command dispatch yok. |
| Zone and table lifecycle | production headless | `ITableRepository`/`IZoneRepository` in `src/Modules/Tables/TableLifecycle/TableRepository.cs`; registrations in `TablesModule.cs:13-18`; schema `database/migrations/V1/V1-TBL-001/010-tables.up.sql` | Add/list/status primitives var; hiçbir table HTTP endpoint'i yok. |
| Table transfer, merge, reservation, pointer rebuild | production headless | `src/Modules/Tables/TableTransfer/**`, `TableMerge/**`, `Reservations/**`, `CurrentPointers/**`; `TablesModule.cs` yalnız lifecycle repository'lerini register eder | Kod vardır fakat default module composition içinde bu servisler register edilmez; production erişimi missing'dir. |
| Geometric floor editor | missing | `010-tables.up.sql` zone, table number, capacity, active ve status tutar; position, size veya shape alanı yok | V1 bu kararla drag/drop geometrik editör iddia etmez. Bölge bazlı operasyonel masa planı sunar. |
| Catalog category/tax/product/modifier CRUD | production headless | Repository CRUD contracts in `src/Modules/Catalog/ProductCatalog/Repositories.cs`; DI registrations in `CatalogModule.cs:15-24` | Domain ve PostgreSQL hazır; management HTTP/API ve production UI yok. |
| Effective-dated price management | production headless | `src/Modules/Catalog/Pricing/Repositories.cs`; `database/migrations/V1/V1-CAT-002/007-catalog-pricing.up.sql` | Yetkili yönetim endpoint'i ve editor yok. |
| Time/channel/location menu publishing | missing | `V1-CAT-001` bunu kapsamaz ve `V1-CAT-001/002` handoff'u `V11-MNU-001`'dir | V1 ekranı bu davranışı varmış gibi göstermeyecek; alan adı `Menü & Katalog`, sınır açıklaması açık olacaktır. |
| Kitchen ticket lifecycle | production headless | `IKitchenTicketRepository` in `src/Modules/Kitchen/TicketLifecycle/IKitchenTicketRepository.cs`; registration `KitchenModule.cs:15-26` | Aktif station sorgusu ve status persistence var; KDS HTTP/UI yok. |
| Printer routing, queue and unknown/reprint | production headless | `src/Modules/Kitchen/Routing/IRepositories.cs`, `PrintQueue/IPrintQueueRepository.cs`, `PhysicalPrintRecovery/**`; registrations `KitchenModule.cs:17-25` | Operatörün görebileceği gerçek yüzey ve authorized endpoint yok. |
| Backup and system health | production headless | `IBackupHealthService` in `src/Modules/Operations/BackupHealth/BackupHealthService.cs`; registration `OperationsModule.cs:12-18` | Owner/supervisor dashboard endpoint'i ve UI yok. |
| Audit event query | production headless | `src/Modules/Audit/EventStore/**`; registration `AuditModule.cs:10-16` | Audit kayıt altyapısı var; bu V1 kararında düzenleme değil, yalnız izinli okunabilir operasyon görünümü planlanır. |
| Rich table/menu/kitchen screens | mock-only | `src/Clients/WebPrototype/index.html:15-24` açıkça “Yerel mock runtime · gerçek backend yok”; table/menu/kitchen tabs lines 91-105; add-table/add-product modals lines 540-633; `app.js:3` production backend'e bağlanmadığını söyler | Kullanıcının aradığı ekranlar silinmedi; production'a hiç bağlanmamış prototipte kaldı. |
| Waiter phone/tablet experience | production headless + mock-only | `src/Clients/WaiterPwa/**` engine/store sınıfları; visual device switcher `WebPrototype/index.html:27-39` | Bu kararın production shell tesliminde cashier/manager/kitchen önceliklidir; mock waiter yüzeyi shipping kanıtı değildir. |

Host'taki production HTTP yüzeyinin tamamı `src/Host/DualScreen/DualScreenApplication.cs:208-442` aralığındaki health,
auth, catalog-read, order mutation ve dual-screen endpoint'lerinden oluşur. Table, catalog mutation, kitchen veya
operations endpoint'i yoktur. `ModuleRegistry.DefaultCatalog` modülleri dependency injection'a dahil etse de
`HostComposition` yalnız service composition/migration yürütür; bu, kullanıcıya ulaşan bir API veya UI değildir.

## Why the features appeared to disappear

`V1-CUI-001`, `V1-CUI-002` ve `V1-CUI-003` `Done` görünür; ancak sahip oldukları çıktılar C# state engine'leridir.
Task dosyaları production code dese de gerçek render host, browser bundle veya API adapter teslim etmemiştir.
Benzer biçimde `V1-TBL-*`, `V1-CAT-*`, `V1-KIT-*` ve `V1-OPS-*` çoğunlukla domain/repository katmanını kapatmıştır.

`V1-RMD-006` daha sonra yalnız dual-screen POS dikey dilimini gerçek Host'a bağladı. `V1-RMD-010` da bu dar yüzeyin
responsive/a11y remediasyonuyla sınırlandı. Dolayısıyla masa yönetimi ve ürün ekleme işlevleri gizli bir route'ta
değildir; yalnız headless kod ve mock prototip düzeyinde kalmıştır. Bu bir CSS kusurundan önce plan/surface custody
kusurudur.

## Production shell information architecture

### Role and device boundaries

| Role | Primary device | Visible workspaces | Forbidden by default |
| --- | --- | --- | --- |
| Cashier | 1280+ touch terminal; 1024 tablet fallback | Kasa, Masalar, Siparişler, customer-display state | Katalog structure, backup, printer route editing |
| Supervisor | POS/tablet/desktop | Cashier areas plus transfer, merge, reservation, reprint approval | Backup execution unless separately permitted |
| Manager/owner | 1024+ tablet or desktop | Menü & Katalog, masa setup, printer routes, system health, audit read | Cashier mutation without terminal/session binding |
| Kitchen operator | Kitchen display/tablet | Station queue, ticket item transitions, printer warnings | Customer/personnel/payment details and catalog administration |
| Customer display | Separate browser storage/display | Allowlisted active-order snapshot only | Staff, token, permission, internal note and navigation data |

Authorization is server-side and capability-based. Gizlenen navigation yetki kanıtı değildir. Unauthorized deep link veya
mutation `403`; expired/missing session `401`; customer display cookie'si staff resource'a erişemez.

### Global shell

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ ALKAROS · Şube · Terminal · Bağlantı · Kullanıcı              Hızlı işlemler │
├──────────────┬───────────────────────────────────────┬──────────────────────┤
│ Kasa         │                                       │ Context / activity   │
│ Masalar      │        Active workspace               │ drawer               │
│ Siparişler   │                                       │ conflict, alerts,    │
│ Mutfak & Ops │                                       │ totals, recovery     │
│ Menü/Katalog │                                       │                      │
├──────────────┴───────────────────────────────────────┴──────────────────────┤
│ Persistent offline/stale/unauthorized status; never color-only             │
└─────────────────────────────────────────────────────────────────────────────┘
```

- 1280 px ve üstünde kalıcı navigation rail, iki/üç bölmeli çalışma alanı ve bağlamsal drawer kullanılır.
- 768-1279 px aralığında rail compact olur, ayrıntı drawer/sheet'e dönüşür.
- 320-767 px aralığında izinli ana alanlar bottom navigation ve tam ekran drill-down kullanır; tablo/editor aynı anda
  yan yana zorlanmaz.
- İşlevsel renkler yalnız status ve severity için kullanılır. Ana yüzey nötr, okunabilir, yüksek bilgi yoğunluklu;
  dekoratif gradient ve gereksiz büyük kartlar operasyonel içeriğin önüne geçmez.
- Tüm primary/secondary actions açık hiyerarşiye, en az 44x44 px hedefe, görünür focus'a ve metin etiketine sahiptir.

### Workspace blueprint

#### Tables

```text
[Zone tabs/search] [Available 12 | Occupied 7 | Reserved 2 | Attention 1]
┌──────────────────────────── Table map/grid ──────────────────────┐
│ S-01 Available 4p │ S-02 Occupied 22m ₺… │ S-03 Ready to serve │
│ B-01 Reserved …   │ B-02 Cleaning         │ ...                  │
└──────────────────────────────────────────────────────────────────┘
[Selected table drawer: order, bill, guests, rowVersion, allowed actions]
```

Masa kartı number, zone, capacity, textual status, elapsed time ve mevcut order/bill pointer'ını gösterir. Cashier
operasyonel aksiyonları; manager zone/table setup'ı görür. Transfer, merge, reservation ve status transition yalnız
server'ın döndürdüğü allowed command ve current row version üzerinden yürür.

#### Order entry

Category rail, product grid/list ve persistent order summary mevcut dual-screen davranışını korur; seçili table/order
bağlamı shell header'ında görünür. Submit sonucu ticket durumuna bağlanır. Draft kaybı, gizli total veya belirsiz
double-click kabul edilmez.

#### Menu and catalog

Sol panel searchable/filterable entity list; orta panel seçili category/product/modifier ayrıntısı; sağ drawer create
ve edit formudur. Product create akışı category, tax profile, type, stock mode, SKU, current/effective price ve modifier
bağlarını aynı doğrulama özetinde gösterir. Değişiklik başarıyla commit olmadan satış kataloğuna yansımış gibi sunulmaz.

Bu V1 yüzeyi gerçek bir `menu` aggregate, location/channel publication veya time-based availability iddia etmez.
Bunlar `V11-MNU-001` kapsamıdır. Ekran bu sınırı kullanıcıya teknik jargonla değil “Satış kataloğu” açıklamasıyla
bildirir.

#### Kitchen and operations

Kitchen operator station bazlı ticket kolonları/listesi görür; item transition tek dokunuşla fakat optimistic
concurrency ile yapılır. Supervisor printer health, queue, Unknown delivery ve reason-required reprint'i görür.
Manager health alanı database, disk ve last backup durumunu gösterir; başarısız/unknown sağlık yeşil gösterilmez.

## Interaction sequences

### Table-to-kitchen service

1. User sees role-allowed zone/table map with freshness timestamp.
2. User selects a table; system loads authoritative table, order/bill pointer and row version.
3. User opens/continues the order and adds products/modifiers.
4. System preserves the draft during validation, network or catalog conflict.
5. User submits once; system returns idempotent order revision and kitchen ticket state.
6. Kitchen operator sees the ticket in the correct station and advances item state.
7. Cashier observes progress without manual page reload; stale state is visibly bounded.

### Add a table

1. Authorized manager opens `Masalar > Düzenle` and sees zones plus validation constraints.
2. Manager chooses zone, table number and capacity; primary action remains disabled until valid.
3. System creates through the real table API and returns the authoritative row version.
4. New table appears in the correct zone as `Available`; conflict/duplicate keeps form data and points to the field.

### Add a catalog product

1. Authorized manager opens `Menü & Katalog > Yeni ürün`.
2. Manager enters identity, category, tax, SKU/type/stock mode, price and optional modifier groups.
3. System shows a review summary and submits one recoverable workflow to real catalog APIs.
4. Validation/conflict keeps entered data; success opens the authoritative product detail.
5. Cashier catalog refresh shows the product only when active and effectively priced.

### Unknown print recovery

1. Supervisor sees an explicit `Teslimat bilinmiyor` alert with affected ticket/printer and timestamp.
2. System never starts automatic reprint.
3. Supervisor with permission enters a mandatory reason and confirms duplicate risk.
4. System records actor/reason and returns the authoritative reprint state.

## Required state contract

Every critical workspace and mutation must expose all applicable states below. A spinner without a bounded outcome is
not a state implementation.

| State | Shell | Tables | Orders | Kitchen/operations | Menu/catalog |
| --- | --- | --- | --- | --- | --- |
| Default | Allowed navigation and freshness | Zone/status map | Draft/active order | Station/health overview | Entity list/detail |
| Loading | Skeleton, no false data | Map skeleton | Draft/order skeleton | Ticket/health skeleton | List/detail skeleton |
| Empty | Role-specific next action | Create first zone/table | Open first order | No active tickets / no health sample | Create first category/product |
| Busy | Disable duplicate command | Add/transition/merge pending | Item/submit pending | Transition/reprint/backup pending | Save/delete pending |
| Success | Route/status announcement | Authoritative table/version | Revision/ticket confirmation | Updated ticket/job/health | Authoritative entity detail |
| Error | Bounded error and retry | Field/domain error; draft retained | Draft retained; retry safe | Failure never shown healthy | Form retained; field summary |
| Offline | Persistent global banner | Read-only cached map if fresh enough | Local mutation is not invented | No status mutation | No management mutation |
| Stale | Timestamp and refresh action | Financial/pointer data hidden when unsafe | Totals hidden/refresh required | Ticket age warning | Editing blocked until refreshed |
| Unauthorized | Login or access-denied route | No hidden mutation fallback | `401/403` distinction | Permission-specific recovery | Manager permission required |
| Conflict | Global actionable notice | Server version + reload/reconcile | Current draft + server revision | Current server ticket/job | Keep form + current server entity |

Domain states remain explicit: `Reserved`, `Cleaning`, `OutOfService`; `Submitted`, `Preparing`, `Ready`, `Served`;
`Unknown`, `ReprintApproved`, `Failed`, `DeadLetter`; `Unavailable`, inactive and no-effective-price.

## Quality acceptance

- All shell routes support keyboard-only completion, logical focus order, focus restoration and named dialogs.
- All viewports in V1-RMD-010 remain mandatory: 1920x1080, 1440x900, 1366x768, 1280x800, 1024x768,
  768x1024, 430x932, 390x844 and 320x568 plus breakpoint ±1 px.
- No horizontal overflow; primary action, order total and connectivity/freshness remain visible in their workspace.
- Touch targets are at least 44x44 px; WCAG 2.2 AA contrast and 200/400% zoom pass; reduced-motion is honored.
- Automated accessibility has zero critical/serious findings, but automated scan alone is not designer acceptance.
- Every mutation has a bounded busy state, success acknowledgement, error recovery and conflict reconciliation.
- Table cards, kitchen tickets and catalog rows prioritize scan speed: consistent alignment, restrained density,
  semantic typography and status text in addition to color.
- Customer, cashier and manager screens do not share permissions, cookies or DTO fields merely because they share a
  bundle.

## Downstream task chain

The task files below are created by `V1-GOV-010`; these definitions are custody requirements, not authorization to
edit the listed surfaces in V1-GOV-009.

### V1-GOV-010 — Production experience task custody

- Work type: decision/documentation.
- Owned surface: exact new task files `V1-RMD-013` through `V1-RMD-021`; existing task files `V1-RMD-010`,
  `V1-RMD-011`, `V1-GOV-004`; `plan/v1/README.md`; `plan/AUDIT_MANIFEST.json`; its own evidence.
- Dependencies: `V1-GOV-009`.
- Acceptance: all tasks below exist with non-overlapping exact ownership; RMD-010 resumes only after RMD-020;
  RMD-011 follows RMD-010; RMD-021 follows RMD-011; GOV-004 follows RMD-021; plan validation is exit 0.

### V1-RMD-013 — Table management production API

- Owned surface: `src/Host/Experience/Tables/**`, `src/Modules/Tables/TableLifecycle/TablesModule.cs`,
  `tests/Host/Experience/Tables/**`, `evidence/V1-RMD-013/**`.
- Dependencies: `V1-GOV-010`, `V1-RMD-009`.
- Contract: permissioned zone/table read/create/update where repositories support it; status transition, reservation,
  transfer, merge/unmerge and pointer read with row-version conflict envelopes. Transfer/merge/reservation/current
  pointer services must be registered before endpoints can resolve.
- Manual scenario: create zone and table, occupy it, transfer/merge with an unpaid order, then reproduce stale-version
  `409` without partial change.

### V1-RMD-014 — Catalog management production API

- Owned surface: `src/Host/Experience/Catalog/**`, `tests/Host/Experience/Catalog/**`,
  `evidence/V1-RMD-014/**`.
- Dependencies: `V1-GOV-010`, `V1-RMD-009`.
- Contract: permissioned category, tax profile, product, modifier group/modifier, product-modifier assignment and
  effective price read/write endpoints; stable validation/error contracts and bounded list queries.
- Manual scenario: create category, tax, product, effective price and modifier; confirm duplicate SKU, negative price
  and overlapping price are rejected while entered data can be retried.

### V1-RMD-015 — Kitchen and operations production API

- Owned surface: `src/Host/Experience/KitchenOperations/**`, `tests/Host/Experience/KitchenOperations/**`,
  `evidence/V1-RMD-015/**`.
- Dependencies: `V1-GOV-010`, `V1-RMD-009`.
- Contract: station ticket query/transition, printer and route query/update, print queue/Unknown recovery, authorized
  reason-required reprint, latest health/recent backup read and permissioned backup command. DTOs exclude customer and
  secret data not needed by the role.
- Manual scenario: submit a real order, advance its ticket, reproduce Unknown print delivery, reprint with supervisor
  reason and observe a failed health/backup state without false success.

### V1-RMD-016 — Production shell and design system

- Owned surface: `src/Clients/PosTerminal/src/shell/**`, `src/Clients/PosTerminal/src/design-system/**`,
  `evidence/V1-RMD-016/**`.
- Dependencies: `V1-GOV-010`.
- Contract: role-aware navigation, global session/connectivity/freshness status, layout primitives, semantic tokens,
  dialogs/drawers and responsive workspace frame. No mock runtime or hard-coded success data.
- Manual scenario: use keyboard and touch at 1280x800, 768x1024 and 390x844; role changes expose only permitted routes,
  focus remains deterministic and offline status never disappears.

### V1-RMD-017 — Table workspace UI

- Owned surface: `src/Clients/PosTerminal/src/features/tables/**`, `evidence/V1-RMD-017/**`.
- Dependencies: `V1-RMD-013`, `V1-RMD-016`.
- Contract: zone-filtered table map/list, add-zone/add-table, selected table context and permissioned
  transition/reservation/transfer/merge actions with the full state contract.
- Manual scenario: manager adds Salon/S-09; cashier opens it, hits a deliberate row-version conflict and recovers
  without losing current order context.

### V1-RMD-018 — Menu and catalog workspace UI

- Owned surface: `src/Clients/PosTerminal/src/features/catalog/**`, `evidence/V1-RMD-018/**`.
- Dependencies: `V1-RMD-014`, `V1-RMD-016`.
- Contract: category/tax/product/modifier/price list-detail-editor flows with searchable dense tables, accessible forms,
  authoritative validation and the full state contract. No V11 menu publishing claim.
- Manual scenario: manager creates a valid priced product with modifier and sees it in the sellable catalog; invalid
  price/conflict retains the form and explains the exact field.

### V1-RMD-019 — Kitchen and operations workspace UI

- Owned surface: `src/Clients/PosTerminal/src/features/kitchen-operations/**`, `evidence/V1-RMD-019/**`.
- Dependencies: `V1-RMD-015`, `V1-RMD-016`.
- Contract: station-scoped ticket board/list, printer/queue alerts, Unknown/reprint recovery and manager health/backup
  views with role-specific data minimization and the full state contract.
- Manual scenario: kitchen advances one item while another remains preparing; supervisor resolves Unknown only after
  entering a reason; failed backup stays visibly failed.

### V1-RMD-020 — Production experience composition and real E2E

- Owned surface: `src/Host/DualScreen/DualScreenApplication.cs`, `src/Clients/PosTerminal/index.html`,
  `src/Clients/PosTerminal/package.json`, `src/Clients/PosTerminal/pnpm-lock.yaml`,
  `src/Clients/PosTerminal/src/App.tsx`, `api.ts`, `contracts.ts`, `main.tsx`, `styles.css`,
  `src/Clients/PosTerminal/vite.config.ts`, `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`,
  `tests/Clients/PosTerminal/Experience/**`, `evidence/V1-RMD-020/**`.
- Dependencies: `V1-RMD-017`, `V1-RMD-018`, `V1-RMD-019`.
- Contract: map the three API groups, compose shell/workspaces, preserve dual-screen DTO/auth behavior and exercise real
  HTTPS Host plus PostgreSQL without mock-success paths.
- Manual scenario: login, add table, add product, open table order, submit, advance kitchen state, reconnect/restart,
  verify customer display and permission denial in separate browser storage areas.

### V1-RMD-010 — Integrated responsive/accessibility reacceptance

- Sequence change: remains `Blocked` until V1-RMD-020 is `Done`; V1-GOV-010 updates its dependency and acceptance
  wording without discarding current uncommitted improvements.
- Manual scenario: repeat its complete viewport/state matrix against the integrated production shell, not the rejected
  vertical slice.

### V1-RMD-011 — Prototype quarantine

- Sequence change: remains after V1-RMD-010. It may harden and visibly quarantine `WebPrototype`; it cannot donate mock
  contracts or be counted as production feature evidence.

### V1-RMD-021 — Independent designer acceptance

- Work type: validation.
- Owned surface: `docs/audit/V1_PRODUCTION_EXPERIENCE_DESIGN_ACCEPTANCE_2026-08-25.md`,
  `evidence/V1-RMD-021/**`.
- Dependencies: `V1-RMD-011`.
- Acceptance: a fresh reviewer who implemented none of RMD-013..020 captures every role, critical flow, required state,
  viewport/breakpoint, focus order, accessibility tree, overflow/bounds, console/network record and visual hierarchy
  verdict. Any critical/serious accessibility issue, missing required state, mock-backed production flow or broken
  table-to-kitchen/catalog workflow fails the task.
- Manual scenario: Semih completes table creation, product creation and table-to-kitchen service without hidden setup,
  then explicitly accepts or rejects the experience; silence is not approval.

### V1-GOV-004 — Final audit reseal

- Sequence change: dependency becomes `V1-RMD-021`. A designer pass does not waive remaining hardware, provider,
  license, backup/RPO-RTO, security assessment or signed go-live blockers.

## Rejected alternatives

1. **Approve the current PosTerminal.** Rejected by `PO:2026-08-25`; it does not expose the V1 operational product.
2. **Ship WebPrototype or copy its mock runtime.** Rejected because the source explicitly has no production backend;
   it would create false success and conflicting contracts.
3. **Expand V1-RMD-010 in place.** Rejected because its exact ownership and goal are responsive/a11y remediation of the
   dual-screen slice; adding APIs, IA and modules would violate task custody.
4. **Implement every screen in one remediation task.** Rejected because Host, table, catalog, kitchen and UI surfaces
   need different owners, contract tests and ordered composition.
5. **Claim Square feature parity.** Rejected. Square is benchmark context only. Geometric drag/drop floor layout and
   multi-location/channel/time menu publishing lack V1 repository contracts and remain missing/deferred.
6. **Create a visually separate manager demo app.** Rejected. V1 chooses one production shell with strict role routes;
   a second mock/dashboard surface would repeat the current fragmentation.

## Exact affected task IDs

- Historical capability tasks: `V1-CUI-001`, `V1-CUI-002`, `V1-CUI-003`, `V1-TBL-001` through `V1-TBL-007`,
  `V1-CAT-001` through `V1-CAT-004`, `V1-KIT-001` through `V1-KIT-004`, `V1-OPS-001`, `V1-OPS-002`,
  `V1-WTR-001` through `V1-WTR-005`, `V1-RMD-006`.
- Existing audit/remediation sequence: `V1-RMD-010`, `V1-RMD-011`, `V1-GOV-004`.
- New custody and delivery sequence: `V1-GOV-010`, `V1-RMD-013` through `V1-RMD-021`.
- Explicit deferred menu aggregate: `V11-MNU-001`.

## Production readiness consequence

This decision does not make ALKAROS production-ready. Until the task chain is implemented, independently accepted and
the final audit is resealed, the correct verdict remains `NOT PRODUCTION READY`. The current PosTerminal may be used as
a tested vertical-slice input, but it is not an approved V1 product experience.
