# V1-RMD-1161 - Kasa kalem ekleme ucu isteğe bağlı kalem notu taşır; not mutfak fişine ve panosuna ulaşır

- Task ID: V1-RMD-1161
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasanın (PosTerminal) kullandığı `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items` ucu kalem notu taşımaz (`AddOrderItemRequest(ProductId, Quantity, ExpectedRevision, Modifiers)`); `SpecialInstructions` yalnız `table-draft` yolunda vardır (`evidence/V1-RMD-1160/CONTRACTS.md` (a)). Mutfak tarafı hazırdır: `OrderItem.Notes` → `KitchenTicket.CreateFromOrder` → `kitchen_ticket_items.notes` → fişte `NOT: ...` ve mutfak panosunda `Not: ...`. Eksik olan yalnız uç noktadaki alan ve bunun uçtan uca kanıtıdır.

1. `AddOrderItemRequest` sonuna isteğe bağlı `SpecialInstructions` eklenir (geriye uyumlu: alan yokken davranış aynıdır). Değer kırpılır, boşsa not yok sayılır; en çok 200 karakter (NFC ve QR kalem notu sınırıyla aynı; mutfak fişi için kısa not) ve satır sonu dahil kontrol karakteri içeremez (fişi bozar). İhlal 400 `VALIDATION_FAILED` ve Türkçe `Not izin verilen aralığın dışında.` / `Not geçersiz veya eksik.` döner; bunun için `ApiErrorCatalog` etiket tablosuna `specialInstructions` → `Not` eklenir. İstek reddedilince sipariş sürümü değişmez.
2. Not `OrderItem` yapıcısının mevcut `notes` alanına bağlanır. Birleştirme kuralı korunur ve kanıtlanır: notlu ekleme her zaman ayrı satırdır; notsuz ekleme notlu satıra birleşmez; notsuz iki ekleme eskisi gibi tek satırda birleşir.
3. Kasa ucunun anlık görüntüsü (`orders/active`, yalnız kimlikleri isteyen yol) satıra `SpecialInstructions` taşır ki kasa sepeti notu gösterebilsin; boşken JSON'a yazılmaz; müşteri ekranı yolları notu hiç görmez (müşteri ekranı izin listesi testi).
4. Uçtan uca zincir gerçek Host + Postgres ile kanıtlanır: `/items` (notlu) → `/submit` → `kitchen_ticket_items.notes` → mutfak panosu API'si (`GET .../kitchen-operations/tickets`) notu döner → `KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync` ile üretilen yazıcı işi yükü `NOT: ...` içerir (Türkçe karakterli not dahil).

Sipariş sunucuda taslak iken not eklenir; sonradan not değiştirme (`PATCH`) ve mevcut kaleme not ekleme bu görevde yoktur.

## Owned surface

- `evidence/V1-RMD-1161/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenContracts.cs
  - src/Host/DualScreen/DualScreenStore.Orders.cs
  - src/Host/DualScreen/DualScreenStore.Display.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs
  - tests/Host/MigrationComposition/DualScreen/ItemNoteKitchenPathHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp` ile geri alınır)
2. İki bağımsız qa incelemesi
3. `ALKAROS.Host.Tests` (MigrationComposition) ve `ALKAROS.Architecture.ApiConventions.Tests` (`idempotency_allowlist.json` ve `api_convention_allowlist.json` değişmeden yeşil kalır: uç noktanın sürüm koruması `ExpectedRevision` olarak sürer)

## Out of scope

- İstemci (PosTerminal, vanilla Cashier, WaiterPwa)
- Miktar değiştirme ucuna not, kalem notunu sonradan düzenleme
- Beklet / park sözleşmesi
- Migration (`orders.order_items.notes` zaten `TEXT`)

## Dependencies

- V1-RMD-1160

## Acceptance evidence

1. Gerçek Postgres testleri: notlu ekleme kalemde saklanır (kırpılmış); boş not null olur; 201 karakter ve kontrol karakteri reddedilir, sürüm değişmez; notsuz çağrı eski davranışı korur; birleştirme kuralı (notlu ayrı, notsuz notluya birleşmez, notsuz+notsuz birleşir); kasa anlık görüntüsü notu taşır, müşteri ekranı taşımaz
2. HTTP: aşırı uzun not Türkçe 400; uçtan uca zincir testi (fiş satırı, pano JSON'u, yazıcı işi yükü)
3. Mutant listesi (alan bağlama, kırpma, uzunluk, kontrol karakteri, birleştirme koşulu, anlık görüntü alanı)
4. İki ardışık bağımsız qa-* incelemesi
5. `dotnet test` ilgili iki proje exit code 0; plan audit ve consistency audit 0 hata 0 uyarı

## Handoff

- V1-RMD-1162
