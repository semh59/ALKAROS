# V1 Desktop POS and Container Release Decision

## Decision metadata

- Decision ID: V1-GOV-017
- Date: 2026-08-28
- Status: Accepted
- Approver: Semih, Product Owner
- Product basis: `PO:2026-08-28`
- Repository candidate: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Decision owner: `/root`

## Decision

Mevcut `Masalar > Harita` kart grid'i production salon yönetimi olarak kabul edilmez. V1 deneyimi desktop-first,
yüksek bilgi yoğunluklu ve gerçek restoran operasyonunu görünür kılan bir çalışma alanına yükseltilecektir. Çalışma;
yalnız CSS rötuşu değil, additive floor/seat verisi, gerçek Host API'leri, masa operasyonları, hesap bölme, katalog
yönetimi ve container release sözleşmesini birlikte fakat ayrı sahipli görevlerle teslim edecektir.

Seçilen ürün davranışı:

1. Zone bazlı gerçek mekânsal salon planı; masalar koordinat, boyut, şekil ve yön bilgisiyle yerleşir.
2. Her masa kapasiteden bağımsız, stabil kimlikli sandalye/yer kayıtlarına sahip olabilir.
3. Seçili masa bağlamında reservation, transfer, merge, unmerge ve durum geçişleri görünürdür; yalnız server
   `allowedCommands` sonucu eylemi etkinleştirir.
4. Hesap; kişi/sandalye, ürün miktarı, eşit pay veya açık tutar ile bölünebilir. Mevcut `SplitEngine` ve
   `billing.bill_allocations` source-of-truth olarak kullanılır; ödeme yapılmış gibi sahte sonuç üretilmez.
5. Menü yönetimi desktop list/detail/editor düzeninde category, tax, product, modifier ve effective price akışını tek
   operasyonel yüzeyde toplar.
6. Host, PosTerminal production bundle, PostgreSQL 18, migration runner, health checks ve kalıcı veri tek Docker
   Compose release sözleşmesiyle ayağa kalkar.
7. Browser doğrulaması kullanıcıya devredilmez; viewport, zoom-equivalent reflow, keyboard, accessibility, console,
   network, screenshot ve bounding-box kanıtları otomatik üretilir.

## Verified repository baseline

| Capability | Current state | Decision consequence |
| --- | --- | --- |
| Table lifecycle | Production domain/API | Reuse; row version and allowed commands remain authoritative. |
| Transfer, merge, unmerge, reservation | Production domain/API | Make primary table-context actions; do not hide behind incidental controls. |
| Floor coordinates, shape, rotation | Missing | Additive migration and versioned API required. |
| Individual chairs/seats | Missing; only `capacity` exists | Add explicit stable seat records; do not infer persisted chairs from capacity. |
| Bill equal/item/amount split | Production domain/persistence, no Host/UI | Expose through authorized API and recoverable UI. |
| Card-grid `Harita` | Production UI but rejected | Replace with spatial canvas; retain dense list as secondary operational view. |
| Catalog management | Production API/UI foundation | Recompose to desktop list/detail/editor and close missing state/quality gaps. |
| Docker release | Missing | Add multi-stage image and complete Compose topology; no localhost-only production claim. |

`WebPrototype` is not a contract source. Its visuals may be compared for missing concepts, but no mock state, endpoint
or hard-coded success value may enter the production bundle.

## Desktop information architecture

### 1440x900 and above

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ ALKAROS  Branch / Terminal / User         Search      Display      Sign out │
├───────────┬──────────────────────────────────────────────┬───────────────────┤
│ Cashier   │ Zone tabs  Availability  Reservation alerts │ Selected table    │
│ Tables    │ ┌──────────────────────────────────────────┐ │ S-09 · Occupied   │
│ Orders    │ │              Spatial floor              │ │ 4 seats · 28 min  │
│ Kitchen   │ │  ○S01     □S02        ▭S03              │ │ Order / bill      │
│ Catalog   │ │       ◇S04       ○S05                    │ │ Guests / actions  │
│ Reports   │ └──────────────────────────────────────────┘ │ Transfer · Merge  │
│           │ Queue/search/list toggle                     │ Split bill         │
├───────────┴──────────────────────────────────────────────┴───────────────────┤
│ Online · authoritative timestamp · conflict/offline recovery               │
└──────────────────────────────────────────────────────────────────────────────┘
```

- The floor is the dominant work surface, not a decorative card collection.
- Table status remains textual and color-independent. Amount and elapsed time align consistently for scan speed.
- The right inspector keeps table, order, bill and guest context visible without navigating away.
- Setup mode is explicit and permissioned. Operational mode cannot accidentally drag or resize tables.

### 1024-1439

- Navigation becomes compact.
- Floor remains primary; inspector is a persistent 320-360 px panel when space permits and a named drawer otherwise.
- Filter controls collapse into one toolbar without hiding availability counts or freshness.

### 768-1023

- Floor and list are mutually exclusive views.
- Selected-table context opens as a full-height drawer with focus trap, Escape and focus restoration.
- Setup editing uses explicit select/move/save steps; touch drag is never the only input method.

### 320-767

- Operational list is primary. A mini-map may be read-only; geometric editing is manager desktop/tablet only.
- Table detail and bill split are full-screen drill-downs.
- Bottom navigation, status and header never cover content; every action remains at least 44x44 CSS px.

## Spatial floor contract

### Data model

- Each zone has a logical canvas width and height in integer layout units.
- Each table has `x`, `y`, `width`, `height`, `shape`, `rotationDegrees` and `layoutRowVersion`.
- Supported V1 shapes are `Rectangle`, `Round` and `Square`; arbitrary polygons are excluded.
- Coordinates and dimensions are bounded by the owning zone canvas. Overlap is permitted only when the server returns a
  documented merge group; setup saves otherwise reject overlap.
- Seats have stable IDs, table ID, display number, normalized edge position, active flag and row version.
- Capacity and active seat count are separately visible. A mismatch is an actionable setup warning, not silently fixed.

### Interaction sequence: floor setup

1. Manager enters `Düzenle`; the system announces setup mode and freezes operational mutations.
2. Manager selects a table or creates one with number, capacity, shape and seat count.
3. Move/resize/rotate updates a local draft; keyboard alternatives adjust position and size in deterministic steps.
4. The system shows overlap, out-of-bounds and duplicate-number errors before submission.
5. Save submits expected row versions for the zone, table and seats in one transaction.
6. Success replaces the draft with authoritative coordinates and versions; conflict preserves the draft and offers
   compare/reload, never blind overwrite.

### Interaction sequence: table service

1. Cashier sees zone plan, textual table status, elapsed time, order/bill marker and reservation marker.
2. Selecting a table opens the inspector without moving layout geometry.
3. Allowed actions are returned by the server. Unsupported or unauthorized operations remain visible only when an
   explanation materially helps; they are never fake-enabled.
4. Transfer selects a source and target, summarizes the order/bill movement and requires a reason.
5. Merge selects two or more tables, identifies the primary table and shows resulting capacity/order context.
6. Unmerge appears for active merge groups and explains which table retains the authoritative order/bill pointers.
7. Reservation shows party size, time and reason; claim/cancel actions require current versions.

## Seat-aware order and bill split contract

The word `seat` refers to a stable place at a table; `person` is an allocation label that may or may not map to a seat.
No personal identity is required.

```text
┌──────────────────── Bill ₺1,240.00 ────────────────────┐
│ Unassigned items                    Allocation owners  │
│ 2 × Steak ........ ₺760             Seat 1  ₺420      │
│ 1 × Salad ........ ₺180      →      Seat 2  ₺390      │
│ 3 × Drink ........ ₺300             Shared  ₺430      │
├────────────────────────────────────────────────────────┤
│ Equal by person | By item/quantity | By amount | Reset │
│ Unallocated ₺0.00 · tax/remainder deterministic       │
│                         Review split   Save allocation │
└────────────────────────────────────────────────────────┘
```

- Equal split requires at least two owners and assigns rounding remainder deterministically.
- Item split supports partial quantities and rejects cumulative over-allocation.
- Amount split must exactly equal payable amount.
- Owners use stable seat references when applicable; free person labels are normalized and bounded.
- Saving replaces the design atomically. A server conflict preserves the local allocation draft.
- Split design does not execute payment. Paid/partially paid allocation behavior remains fail-closed unless an approved
  payment contract explicitly supports it.

## Menu management contract

Desktop layout uses a dense searchable entity list, authoritative detail panel and explicit editor drawer:

```text
┌ Categories / Products ─┬ Product detail ───────────────┬ Create or edit ────┐
│ Search · filters       │ Name / SKU / state           │ Identity           │
│ Espresso       Active  │ Category / tax / modifiers   │ Classification     │
│ Latte          Active  │ Effective price timeline     │ Price + effective  │
│ Seasonal       Draft   │ Validation / audit summary   │ Review · Save      │
└────────────────────────┴───────────────────────────────┴────────────────────┘
```

- Create/edit retains all entered fields after validation or conflict.
- Effective price history is a timeline, not one mutable-looking price box.
- Category, tax, modifier and price dependencies are loaded before the editor becomes submittable.
- Empty, loading, unavailable-price, inactive, conflict, unauthorized, offline and success states are visually
  distinct and bounded.
- V1 does not claim multi-location/channel/time menu publishing; that remains a later contract.

## Required state matrix

| State | Floor | Table inspector | Split bill | Catalog |
| --- | --- | --- | --- | --- |
| Loading | Geometry skeleton | Context skeleton | Bill skeleton | List/detail skeleton |
| Empty | Create first zone/table | No selection instruction | No payable bill | Create first entity |
| Busy | Editing locked with progress | Command deduplicated | Save deduplicated | Save deduplicated |
| Success | Authoritative layout/version | Updated status/pointers | Saved allocations/totals | Authoritative detail |
| Error | Bounded retry | Draft/context retained | Allocation draft retained | Form retained |
| Offline | Read-only last-safe layout | No mutation | No allocation save | No mutation |
| Stale | Timestamp and refresh | Financial pointers hidden | Totals hidden | Editor blocked |
| Unauthorized | Access-denied workspace | `403` explanation | Permission explanation | Manager permission |
| Conflict | Compare/reload draft | Server version shown | Local/server allocation | Local/server entity |

## Accessibility and visual-quality contract

- WCAG 2.2 AA contrast; status never color-only.
- Keyboard completes table selection, setup positioning, transfer/merge/unmerge, reservation and split allocation.
- Drag/drop always has a keyboard and form alternative.
- Dialogs and drawers have an accessible name, focus trap, Escape behavior and focus restoration.
- All targets are at least 44x44 CSS px; focus indicators remain visible over every table status color.
- 1920x1080, 1440x900, 1366x768, 1280x800, 1024x768, 768x1024, 430x932, 390x844 and 320x568 plus every
  breakpoint at ±1 px are automated.
- 200/400% reflow, reduced motion, DOM/accessibility snapshot, focus transcript, console/network log, screenshot and
  bounding boxes are evidence requirements.
- Visual acceptance rejects arbitrary gradients, oversized empty cards, clipped ellipsis as primary information,
  inconsistent spacing, low-density desktop layouts and controls whose hierarchy depends only on fill color.

## Complete Docker release architecture

```text
operator -> HTTPS edge/Host container -> ALKAROS Host + PosTerminal static bundle
                                      -> PostgreSQL 18 persistent volume
                                      -> ordered migration init/run contract
```

- Multi-stage build pins .NET SDK/runtime and Node/pnpm versions used by repository validation.
- Production image contains the published Host, PosTerminal production assets and migration files required at startup.
- Compose uses digest-pinned PostgreSQL 18, named persistent volume, health checks, restart policy and dependency
  conditions.
- Secrets come from environment/secret files excluded from Git. Compose fails closed when required secrets are absent.
- HTTPS/proxy behavior matches production Host security rules. Development certificate bypass is not a production path.
- Database readiness is not application readiness; Host health becomes healthy only after migration and module startup.
- Restart proves persisted tables, products, orders, seats, layout and split designs survive.
- One documented command builds and starts the complete stack; one command reports health. No prebuilt local `dist`,
  `bin`, `obj`, long-lived test container or stale evidence is reused.

## Delivery chain

1. `V1-RMD-026`: floor-plan and seat persistence/domain/API.
2. `V1-RMD-027`: operational bill-splitting API.
3. `V1-RMD-028`: desktop spatial floor workspace.
4. `V1-RMD-029`: seat-aware order and bill-split workspace.
5. `V1-RMD-030`: desktop-quality menu management.
6. `V1-RMD-031`: complete containerized release.
7. `V1-RMD-032`: integrated designer and release acceptance.

## Rejected alternatives

1. **Polish the card grid.** Rejected: it cannot represent spatial service or setup operations.
2. **Infer chairs from capacity only.** Rejected: seat identity and placement cannot survive edits or allocations.
3. **Copy WebPrototype behavior.** Rejected: it is mock-backed and not a production contract.
4. **Implement UI before persistence/API.** Rejected: it would force placeholder geometry and fake actions.
5. **Put all work in one remediation task.** Rejected: migration, API, UI, Docker and acceptance require separate
   ownership and evidence.
6. **Call Docker Compose production by itself.** Rejected: missing secrets, TLS, health, migration, persistence and
   restart evidence remain blockers.

## Production-readiness consequence

This decision authorizes implementation but does not approve the current product. ALKAROS remains
`NOT PRODUCTION READY` until every delivery task passes and external hardware/provider/license/backup/security/go-live
evidence required by the final audit is present.
