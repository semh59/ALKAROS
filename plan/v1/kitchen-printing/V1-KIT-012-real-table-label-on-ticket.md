# V1-KIT-012 - Mutfak biletine gerçek masa etiketi ekleme

- Task ID: V1-KIT-012
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-KDS-001`'in kendi kaydettiği kapsam sapması: `KitchenTicketV1`'in hiç
masa kimliği taşımaması yüzünden Expo görünümü "masa bazlı gruplama"
yerine sipariş kimliğiyle (`orderId`) gruplamak zorunda kaldı (bkz.
`KitchenOperationsWorkspace.tsx`'teki `groupByOrder`'ın kendi doc-comment'i).
Bu görev o boşluğu kapatır: `KitchenTicketV1`'e gerçek `TableId`/
`TableNumber` alanları eklenir — sipariş üzerinden (`orders.orders.
table_id`) ve masa yönetimi üzerinden (`table_mgmt.tables.table_number`)
**taze okunur**, Kitchen'ın kendi şemasına (`kitchen.kitchen_tickets`)
hiç kopyalanmaz/depolanmaz. Aynı desen zaten `KitchenOrderSubmissionDispatcher`'ın
yaş-kısıtı/kategori okumalarında var (V1-RMD-137/V1-KIT-006 emsali) —
"backend akıllı, frontend aptal" (`foundations.md` §0).

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs,
  KitchenOperationsContracts.cs (Sınırlı ek — V1-RMD-082 sahipliğinde
  kalan dosyalar) — `KitchenTicketV1`'e `TableId`/`TableNumber` alanları;
  `orders.orders` + `table_mgmt.tables`'ı tek toplu sorguyla (liste ucu)
  ve tekli sorguyla (tek bilet uçları) okuyan yeni özel metodlar.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs,
  KitchenOperationsTestDatabase.cs (Sınırlı ek — V1-RMD-082 sahipliğinde
  kalan dosyalar) — yeni testler; masalı/masasız (paket/bar) sipariş
  senaryosu için seed yardımcıları.

## In scope

1. `KitchenTicketV1(..., Guid? TableId, string? TableNumber)` — ikisi de
   null olabilir (sipariş hiç masaya bağlı değilse, ör. paket sipariş —
   `orders.orders.table_id` zaten nullable).
2. Hem `GetActiveTicketsAsync` (liste) hem `GetTicketAsync`/
   `TransitionTicketAsync`/`TransitionItemAsync`/`UndoItemAsync` (tekli)
   uçlarının hepsi bu alanları doldurur — bir bileti nasıl okursan oku,
   masa etiketi tutarlı görünür.
3. Liste ucu N+1 sorgu üretmez: aynı `orderId`'ye sahip birden fazla
   bilet varsa (bir sipariş birden fazla istasyona düşebilir), tek toplu
   `ANY(@order_ids)` sorgusuyla çözülür.

## Out of scope

- `kitchen.kitchen_tickets` şemasına yeni bir kolon eklemek/migration —
  bu görev taze okuma kullanır, kalıcı kopya tutmaz.
- Frontend'in bunu kullanması (`V1-KDS-005`).

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek
  Postgres'e karşı yeşil; en az iki yeni test — masaya bağlı bir sipariş
  için doğru `tableId`/`tableNumber` döner, masasız (paket) bir sipariş
  için ikisi de null döner; liste ucunun aynı siparişten gelen birden
  fazla bilette tutarlı etiket döndürdüğü.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.

## Handoff

- V1-KDS-005
