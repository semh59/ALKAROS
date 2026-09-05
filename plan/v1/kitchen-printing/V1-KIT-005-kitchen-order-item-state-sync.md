# V1-KIT-005 - Kitchen ticket item state sync to order items

- Task ID: V1-KIT-005
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`OrderItem.KitchenState` (`Orders/OrderAggregate/OrderEnums.cs`), üretimde
hiçbir zaman `NotSent`'in ötesine geçmiyordu: `KitchenTicketItem`
(`Kitchen/TicketLifecycle/**`) `Queued/Preparing/Ready/Served/Cancelled`
durumlarını tamamen kendi tarafında takip ediyordu, bunu yansıttığı
`OrderItem`'a geri yazan hiçbir orkestrasyon yoktu. Bu görev, **yalnız
`V1-SET-002`'nin `kitchen.live_sync_enabled` anahtarı açıkken**, bu
senkronizasyonu kurdu; böylece `V1-WTR-009` (hazır bildirimi) ve `V1-IAM-027`
(gönderildi-ama-servis-edilmedi kalemi iptal) her zaman `NotSent` dönen sahte
bir alan yerine gerçek bir veriye bakabilir.

## Owned surface

- `plan/v1/kitchen-printing/V1-KIT-005-kitchen-order-item-state-sync.md`
- `src/Modules/Kitchen/OrderItemStateSync/**`
- `tests/Modules/Kitchen/OrderItemStateSync/**`
- `src/Modules/Orders/Integration/KitchenEventOrderConsumer.cs` (yeni dosya —
  `TableEventOrderConsumer.cs`'in yanına, aynı desen; o dosyaya dokunulmadı)
- `src/BuildingBlocks/IntegrationContracts/KitchenIntegrationEvents.cs`
  (yeni dosya — `TableIntegrationEvents.cs`'e komşu, kendi olay sözleşmesi;
  o dosyaya dokunulmadı)
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Modules/Orders/OrderAggregate/OrderItem.cs` (`V1-RMD-064` sahipliğinde
  kalır) — yeni `AdvanceKitchenState(KitchenState)` metodu; mevcut hiçbir
  metot değişmedi.
  `src/Modules/Orders/OrderAggregate/Order.cs` (`V1-RMD-064` sahipliğinde
  kalır) — yeni `AdvanceItemKitchenState(Guid, KitchenState)` metodu;
  `CancelItem`'ın yanına, aynı desen.
  `src/Modules/Orders/OrderAggregate/OrdersModule.cs` (`V1-RMD-064`
  sahipliğinde kalır) — yeni `KitchenEventOrderConsumer` kaydı, mevcut
  `TableEventOrderConsumer` kaydının hemen altına.
  `src/Modules/Orders/ALKAROS.Orders.csproj` — proje referansı değişmedi.
  `src/Modules/Kitchen/ALKAROS.Kitchen.csproj` — yeni
  `ALKAROS.Messaging.csproj` proje referansı eklendi (yayıncı outbox'a
  yazabilsin diye).
  `src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs` (`V1-RMD-082`
  sahipliğinde kalır) — kurucuya `ISettingsService settings, OutboxStore
  outbox` eklendi; `TransitionItemAsync` başarılı kalem geçişinden sonra
  yayıncıyı çağırır.
  `src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs`
  (`V1-RMD-082` sahipliğinde kalır) — `AddKitchenOperationsExperience`'a
  Settings + `OutboxStore` kayıtları eklendi (V1-IAM-025/OfflineReconciliation
  deseni).
  `tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs`,
  `tests/Modules/Orders/OrderAggregate/ALKAROS.Orders.OrderAggregate.Tests.csproj`
  (`V1-RMD-064`/ilgili sahipliğinde kalır) — yeni `OrderKitchenStateSyncTests`
  sınıfı ve yeni `KitchenEventOrderConsumerTests.cs` dosyası; mevcut testler
  değişmedi.
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- `KitchenTicketItem.TransitionTo` bir durum değişikliği yaptığında
  (`Queued→Preparing→Ready→Served` veya `→Cancelled`), ve
  `kitchen.live_sync_enabled` açıksa, ilgili `OrderItem.KitchenState`'i
  gerçekten günceller. `Queued` de yayımlanır (atlanmaz): bu, void'in
  gönderilmeden-önce serbest yolunu kapatan tam sınır (`NotSent → Sent`).
- Anahtar kapalıyken hiçbir davranış eklenmez (mevcut durum korunur;
  `KitchenOrderItemStateSyncPublisher.PublishAsync` erken döner).
- Aktarım, mevcut Table Management deseninin birebir aynısı: Kitchen kendi
  kısa işleminde outbox'a bir olay yazar (`KitchenTicketItemStateChanged`),
  Orders modülü kendi `IIntegrationEventConsumer`'ında
  (`KitchenEventOrderConsumer`) bunu tüketip yalnız kendi satırını günceller
  (V0-ARC-001 §2).
- **Tasarım sapması (belgelenen, kabul edilen ödün):**
  `IKitchenTicketRepository.SaveAsync`'in bağlantı/işlem parametreli bir
  aşırı yüklemesi yok (Table Management'ın `ReparentActiveOrdersToTableAsync`'inin
  aksine), bu yüzden outbox yazısı bilet kaydının AYNI işleminde değil,
  hemen ardından kendi kısa işleminde olur. Tam atomik transactional-outbox
  garantisinden daha dar bir garanti (iki commit arası çok kısa bir pencerede
  bir çökme olayı düşürebilir) — mutfak durumu mali değil operasyonel
  olduğu ve her tüketici zaten en-az-bir-kez/sıra-dışı teslimata dayanıklı
  olmak zorunda olduğu için kabul edildi.

## Out of scope

- Bildirim (`V1-WTR-009`'un kapsamında).
- Void/waste akışı (`V1-IAM-027`'nin kapsamında).

## Dependencies

- V1-SET-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `python tools/project-manifest/project_manifest_tool.py`: VALID (yeni
  proje referansı ve yeni test projesi tutarlı).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Kitchen.OrderItemStateSync.Tests` 7/7 (yeni proje — anahtar
  kapalıyken hiç outbox satırı yazılmaz; her beş durum için (Queued dahil)
  açıkken tam bir satır yazılır; boş sipariş id'si reddedilir).
  `ALKAROS.Orders.OrderAggregate.Tests` 97/97 (92 → 97: `OrderKitchenStateSyncTests`
  domain testleri — aktif kalemde durum ilerler, Draft/Cancelled/Complimentary
  kalemde reddedilir, aynı durumun tekrar teslimatı no-op ve aynı instance'ı
  döner, iptal edilmiş kalem geç gelen bir mutfak olayını yok sayar, bilinmeyen
  kalem id'si `ArgumentException`; `KitchenEventOrderConsumerTests` gerçek
  repo + DB — durum kalem üzerine yansır ve satır sürümü ilerler, `Queued`
  Orders'ın `Sent` adına eşlenir, aynı durumun tekrar teslimatı satır
  sürümünü ikinci kez artırmaz, void edilmiş bir kalem geç gelen olayı
  yok sayar (Status Cancelled kalır, KitchenState de Cancelled'da kalır)).
  `ALKAROS.Kitchen.TicketLifecycle.Tests` 18/18,
  `ALKAROS.Host.Experience.KitchenOperations.Tests` 4/4,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8 (Kitchen → Messaging BuildingBlock
  referansı sınır ihlali değil) — hepsi regresyonsuz.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- Not: `Kitchen.csproj`'a yeni proje referansı eklenmesi tüm bağımlı
  projelerin `packages.lock.json`'ını geçersiz kıldı;
  `dotnet restore ALKAROS.slnx --force-evaluate` ile yeniden üretildi.

## Handoff

- V1-WTR-009
- V1-IAM-027
