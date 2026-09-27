# V1-RMD-380 - PosTerminal Cashier.tsx Tur 2 denetimi: modifikatör desteği YOK (ciddi, backend değişikliği gerektiriyor)

- Task ID: V1-RMD-380
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 5: PosTerminal'in
ana kasa satış ekranı (`Cashier.tsx`). Bu görev, Tur 2'nin en ciddi bulgusunu ortaya çıkardı ve
BİLİNÇLİ olarak kod değişikliği yapmadan (yalnızca araştırma + belgeleme) kapatıldı — gerekçesi
aşağıda.

**Bulgu — modifikatör desteği YOK, ve bu kez tek başına bir frontend düzeltmesi DEĞİL:**
`CatalogProduct` arayüzü (`contracts.ts`) `modifierGroups` alanını hiç taşımıyor — Cashier
vanilla'nın Tur 2'de (V1-RMD-376) bulduğu AYNI bulgu, ama bu kez PosTerminal'in ana, React
tabanlı satış ekranında. Ancak asıl önemli fark: Cashier vanilla'nın düzeltmesi yalnızca bir
FRONTEND eklemesiydi (backend zaten `OrderItemDraftDto.Modifiers` ile uçtan uca destekliyordu).
Burada durum farklı:

- PosTerminal'in Cashier.tsx'i `api.addItem()` → `POST /orders/{orderId}/items` çağırıyor, bu da
  **`src/Host/DualScreen/`** modülünün TAMAMEN AYRI, daha basit sipariş altyapısını kullanıyor
  (WaiterPwa/Cashier vanilla'nın kullandığı `OrderManagement`/`OrderItemDraftDto` yolu DEĞİL).
- Bu yolun kendi istek sözleşmesi — `AddOrderItemRequest(Guid ProductId, decimal Quantity, long
  ExpectedRevision)` (`DualScreenContracts.cs:107`) — hiçbir `Modifiers` alanı taşımıyor.
- `DualScreenStore.Orders.cs`'in `AddItemAsync`'i, kalemleri SADECE `ProductId`'ye göre
  eşleştirip birleştiriyor (`order.Items.FirstOrDefault(i => i.ProductId == request.ProductId
  && i.Status == OrderItemState.Draft)`) — modifikatör kavramı bu alanın DOMAIN modelinde
  (`Order.AddItem`/`ChangeItemQuantity`) hiç yok.
- Katalog OKUMA tarafı (`CatalogProductDto.ModifierGroups`) zaten modifikatör verisini taşıyor
  (WaiterPwa/Cashier vanilla'nın okuduğu aynı alan) — sorun veri eksikliği değil, bu YAZMA
  yolunun onu hiç kabul etmemesi.

Sonuç: zorunlu bir seçenek grubu olan bir ürün, PosTerminal'in ana kasa ekranından satıldığında,
seçim tamamen kaybediliyor — VE bu, Cashier vanilla'daki gibi salt bir frontend eksikliği değil,
sözleşme/domain seviyesinde bir boşluk. Düzgün bir düzeltme şunları gerektirir: (1)
`AddOrderItemRequest`'e bir `Modifiers` alanı eklemek, (2) `Order` domain agregat'ının
`AddItem`/eşleştirme mantığını modifikatörleri de dikkate alacak şekilde genişletmek, (3) kalıcı
depoyu (şema/JSON sütunu) buna göre değiştirmek, (4) fiyat hesaplamasını modifikatör fiyat
farkını içerecek şekilde güncellemek, (5) Cashier.tsx'e bir seçenek modalı eklemek (Cashier
vanilla'nın Tur 2'de eklediğine benzer).

**Neden şimdi kod değişikliği yapılmadı:** Bu, bir arayüz denetiminin "gerçek bulgu bul, gerçek düzeltme yap" disiplinini AŞAN bir kapsam:
sipariş domain modelinin ve kalıcı şemasının değişmesi gerekiyor — üretim verisiyle çalışan bir
sipariş/ödeme akışında dikkatli, ayrı bir incelemeyle ele alınması gereken bir değişiklik, bu
denetim turunun bir yan ürünü olarak aceleye getirilmemeli. Bu, önceki modüllerde ertelenen
"yeni backend yüzeyi gerektiriyor" bulgularıyla (Modül 3'ün çapraz-müdür keşif sorunu, Modül
4'ün yardım-onay geri bildirimi) aynı sınıftan, ama ÖNCELİK olarak onlardan daha yüksek: bu,
sessiz veri kaybı/hatalı fiyatlandırma riski taşıyan, kod tabanının EN ÇOK KULLANILAN ekranını
etkiliyor.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-380-posterminal-cashier-round2-audit.md`

## In scope

- Yok — kod değişikliği yapılmadı, bilinçli olarak. Bulgu belgelendi.

## Out of scope

- Yukarıda açıklanan tam düzeltme (backend sözleşme + domain + şema + frontend) — ayrı, özel bir
  görev olarak ele alınmalı, Semih'in önceliklendirmesine bırakıldı.
- P2/P4/T7/T8: bu ekranın geri kalanı (arama, sekme ARIA'sı, axe taraması) Modül 5'in Tur 1'inde
  (V1-RMD-364) zaten kapatılmıştı; React'in kendi diffing'i sayesinde T8'in vanilla client'lardaki
  "debounce'suz tam yeniden render" sınıfından bir sorunu da yok.

## Dependencies

- None

## Acceptance evidence

- Araştırma bulguları gerçek kod okumasıyla doğrulandı: `src/Clients/PosTerminal/src/contracts.ts`
  (`CatalogProduct`), `src/Clients/PosTerminal/src/api.ts` (`addItem`),
  `src/Host/DualScreen/DualScreenContracts.cs` (`AddOrderItemRequest`, `CatalogProductDto`),
  `src/Host/DualScreen/DualScreenStore.Orders.cs` (`AddItemAsync`'in ProductId-only eşleştirmesi).
- Kod değişikliği olmadığı için test/mutation-check gerekmedi.

## Handoff

- None
