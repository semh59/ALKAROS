# V1-IAM-027 - Sent-but-unserved item void with waste

- Task ID: V1-IAM-027
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`docs/domain/void-complimentary-discount-policy.md`'nin `## Amendment`
bölümünün (Semih, 2026-09-04) kodlanması: `kitchen.live_sync_enabled` açıkken
(`V1-SET-002`), mutfağa gönderilmiş ama henüz servis edilmemiş
(`KitchenState ∈ {Sent, Preparing, Ready}`, `V1-KIT-005` gerçek değeri
yazdığı için artık bilinebilir) bir kalem, `bills.void` grant'iyle
(politika/delegasyon/yönetici — model §4) iptal edilebilir. İptal, eşleşen
mutfak bilet kalemini de düşürür ve açık bir Bill üzerindeyse `BillItem`'ı
kaldırıp `BillLineType.Waste` satırı yazar (bu enum değeri `III.7.2`
kataloğunda vardı, hiç üretilmemişti).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-027-sent-unserved-void-with-waste.md`
- `src/Host/Experience/Orders/SentItemVoid/**` (yeni — orkestrasyon Host
  katmanında yaşıyor, `ItemExceptionHandler`'ın içinde değil, böylece
  Orders/Kitchen/Billing birbirine bağımlı olmuyor, V0-ARC-001; Host zaten
  üçüne de bağımlı)
- `tests/Host/Experience/Orders/VoidSent/**` (yeni proje — üst dizinde
  `V1-RMD-083`/`V1-ORD-005`/`V1-BIL-005`'in sahip olduğu yollarla
  çakışmıyor)
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Modules/Orders/OrderAggregate/OrderItem.cs` (`V1-RMD-064` sahipliğinde
  kalır) — `Cancel()`'ın reddi gevşetildi: yalnız `Served`/`Cancelled`
  durumundan reddeder (önceden `NotSent` dışındaki her şeyden reddediyordu).
  `V1-ORD-005`'in ücretsiz yolu etkilenmedi — `ItemExceptionHandler
  .VoidItemAsync` kendi `LateVoidRejectedException` kontrolünü `Cancel()`'dan
  ÖNCE, hâlâ yalnız `NotSent` için, yapıyor.
  `tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs`
  (`V1-RMD-064`/ilgili sahipliğinde kalır) — `PreparedActiveItemCannotBeVoided`
  ve `VoidIncompatibleItemThrows` testleri yeni davranışı yansıtacak şekilde
  güncellendi (artık Sent/Preparing/Ready iptal EDİLEBİLİYOR; yalnız Served
  hâlâ reddediliyor; sipariş-seviyesi terminal-durum reddi ayrı bir testte
  kaldı); mevcut diğer testler değişmedi.
  `src/Host/Experience/Orders/OrderManagementContracts.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `VoidSentItemRequestV1`/`VoidSentItemResultV1`
  kayıtları.
  `src/Host/Experience/Orders/OrderManagementEndpoints.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `POST .../items/{itemId}/void-sent` uç noktası;
  `AddOrderManagementExperience`'a `IKitchenTicketRepository`,
  `IBillRepository`, `SentItemVoidStore` kayıtları eklendi (grant-akışının
  bağımlılıkları zaten `V1-BIL-005`'te eklenmişti); paylaşılan
  `OrderManagementExceptionFilter`'a üç yeni istisna eşlemesi eklendi.

## In scope

- `OrderItem.Cancel()`'ın `KitchenState is not KitchenState.NotSent` reddini
  gevşetmek: `Sent/Preparing/Ready`'den de iptale izin verir (yalnız
  `Served`'dan değil — o comp'un alanı).
- İptal, eşleşen `KitchenTicketItem`'ı da `Cancelled`'a taşır
  (`IKitchenTicketRepository.GetByOrderIdAsync` ile bulunup,
  `KitchenTicket.UpdateItemStatus` ile — eşleşen bir kalem yoksa, en iyi
  çaba: void yine de Order üzerinde başarıyla tamamlanır).
- Bir `Bill` zaten varsa (aynı sipariş kalemine sahip bir `BillItem`),
  kaldırır (`Bill.RemoveItem`, bugüne kadar hiç çağrılmayan mevcut metot) ve
  sıfır tutarlı bir `BillLineType.Waste` satırı ekler (müşteri hiçbir şey
  ödemez, ama israf kayda geçer). Bill `Open`/`Reopened` değilse (zaten
  `Allocated`/`Paid`/vb.), 409 `BILL_NOT_MODIFIABLE` ile reddedilir.
- `bills.void` iznine sahip değilse `IAuthorizationGrantService.RequestAsync`
  (aynı `V1-BIL-005` deseni: doğrudan izin varsa uygula, yoksa grant iste;
  `Refused`→403, `Pending`→202 + `GrantId`, `Authorized`→aynı istekte uygula).
- `kitchen.live_sync_enabled` kapalıyken bu yol hiç ulaşılamaz
  (`OrderItem.KitchenState` hep `NotSent` kalır) — mevcut duvar davranışı
  değişmeden sürer; testte doğrudan bir `NotSent` kalemle 409 `NOT_YET_SENT`
  olarak kanıtlanır (anahtar kontrolü ayrıca eklenmedi, mevcut muhafaza zaten
  aynı etkiyi üretiyor).
- **Bilinen, disclosure'lı sınır:** model §3'ün "own check" kuralı (bir
  garson yalnız kendi servis ettiği çeki void'leyebilir) `V1-BIL-005`'te
  olduğu gibi uygulanamıyor — sistemde hiçbir yerde garson/sipariş
  servis-atama modeli yok (`V1-WTR-009`'un da belgelediği gibi). Uç nokta
  `SubjectServingUserId: null` geçiyor; own-check muhafazası bu durumda hiç
  tetiklenmiyor. Sessizce atlanmadı — bir servis-atama modeli var olduğunda
  gerçek kısıtlamayı eklemek gelecek iş olarak kalır.
- **Atomiklik ödünü (V1-KIT-005 ile aynı sınıf, disclosure'lı):** Order,
  KitchenTicket ve Bill üç ayrı kısa işlemde yazılır, tek atomik işlemde
  değil — bir çökme, mutfak bilet kalemini veya hesap satırını bir adım
  geride bırakabilir. Order yazısı (parayı taşıyan) her zaman önce ve tek
  başına kararı verir; her tüketici zaten en-az-bir-kez/sıra-dışı
  teslimata dayanıklı.

## Out of scope

- Fiscal sonrası iptal (refund yolu, `V0-DOM-003`).
- Pre-send void (`V1-ORD-005`) ve comp (`V1-BIL-005`) — bu görev yalnız
  gönderildi-ama-servis-edilmedi durumunu kapsar.
- Own-check'in gerçek uygulanması (yukarıdaki disclosure'lı sınıra bakın).

## Dependencies

- V1-SET-002
- V1-KIT-005
- V1-ORD-005
- V1-BIL-005

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 hata.
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 7/7 (yeni proje —
  oturumsuz istek 401; geçersiz sebep kodu 400; hâlâ `NotSent` bir kalem 409
  `NOT_YET_SENT`; zaten `Served` bir kalem 409 `ALREADY_SERVED`; `bills.void`
  tutan rol 200 ile doğrudan uygular VE eşleşen mutfak bilet kalemini
  Cancelled'a taşır VE hesap satırını Waste'e çevirir; eşleşen bilet/hesap
  yokken de 200 ile uygular, iki bayrak da false; `bills.void` tutmayan rol
  202 `Pending` ile eskalasyon).
  `ALKAROS.Orders.OrderAggregate.Tests` 101/101 (97→101: `Cancel()`'ın
  gevşetilmiş reddini kanıtlayan `SentButUnservedActiveItemCanBeVoided`
  (Theory, üç durum) ve `ServedActiveItemCannotBeVoided`; `Order.CancelItem`
  için `SentButUnservedItemCanNowBeVoidedAtTheOrderLevel` ve
  `VoidOnATerminalOrderThrows`).
  `ALKAROS.Orders.ItemExceptions.Tests` 22/22 (V1-ORD-005'in ücretsiz yolu
  regresyonsuz — `LateVoidRejectedException` hâlâ `NotSent` dışı her şeyi
  `Cancel()`'a ulaşmadan reddediyor).
  `ALKAROS.Billing.BillFoundation.Tests` 40/40,
  `ALKAROS.Kitchen.TicketLifecycle.Tests` 18/18,
  `ALKAROS.Identity.Authorization.Tests` 185/185,
  `ALKAROS.Host.Experience.Orders.Void.Tests` 5/5,
  `ALKAROS.Host.Experience.Orders.Comp.Tests` 7/7,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz (Host→Kitchen/
  Billing/Identity.Authorization referansları zaten mevcut sınır ihlali
  değil).
- `python tools/consistency-audit/consistency_audit.py`: temiz (bir Türkçe
  karakter sızıntısı `OrderItem.cs` XML doc yorumunda bulunup düzeltildi).
- Ortam notu: bu oturumda birkaç kez, ilişkisiz projelerin (önce
  `ALKAROS.Settings.KitchenLiveSync.Tests`, şimdi `ALKAROS.Operations.dll`)
  taze inşa edilmiş DLL'lerinde geçici WDAC (Windows Defender Application
  Control) engeli görüldü — hem Debug hem Release'te, dosyaya/yapılandırmaya
  özgü değil, ortam kaynaklı (G1, önceden belgelenmiş). Etkilenen her proje
  ayrı ayrı temiz `bin`/`obj` silme + yeniden derleme ile ya da diğer
  yapılandırmaya geçilerek doğrulandı; hiçbiri gerçek bir regresyon değildi.

## Handoff

- V1-GOV-074
