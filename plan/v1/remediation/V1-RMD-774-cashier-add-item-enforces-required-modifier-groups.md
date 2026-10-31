# V1-RMD-774 - Ana kasa ekranı zorunlu seçenek grubu olan ürünü seçimsiz satamasın

- Task ID: V1-RMD-774
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V1-RMD-380, Yüksek). PosTerminal ana kasa ekranı (`Cashier.tsx`) ürün kartına basınca `api.addItem` yalnız `productId`, `quantity` ve `expectedRevision` yolluyor. Sunucudaki `DualScreenStore.AddItemAsync` ürünün seçenek (modifier) gruplarına hiç bakmıyor. Sonuç: zorunlu seçenek grubu olan bir ürün (ör. "porsiyon boyu seçin") ana kasadan seçimsiz satılıp mutfağa gidebiliyor; isteğe bağlı ekstralar hiç seçilemiyor. Garson yolu (V1-RMD-147/161), vanilla kasa (V1-RMD-376) ve NFC yolu (V1-RMD-484) bunu zaten yapıyor; ana kasa React ekranı kalmış tek yol.

Çözüm: `AddOrderItemRequest` seçilen seçenekleri taşır; sunucu seçenekleri katalogdan çözer ve grup kurallarını garson yoluyla aynı Host yardımcılarıyla denetler (zorunlu grup eksikse 400); ürünün seçenek grubu varsa kasa ekranı seçim penceresi açar ve seçimle birlikte gönderir. Seçenekli satır bare satırla birleşmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-774-cashier-add-item-enforces-required-modifier-groups.md`
- `evidence/V1-RMD-774/**`
- `src/Clients/PosTerminal/src/routes/CashierModifierPicker.tsx`
- `src/Clients/PosTerminal/src/routes/Cashier.modifier-picker.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenStore.Orders.cs (V1-RMD-120) — seçenekleri çöz ve grup kuralını denetle.
  - src/Host/DualScreen/DualScreenContracts.cs (V1-RMD-148) — istek `Modifiers` taşır; bayat "sunucu denetlemiyor" notu düzelir.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-364) — seçenekli ürün için seçim penceresi.
  - src/Clients/PosTerminal/src/api.ts (V1-RMD-031) — `addItem` seçimi yollar.
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs (V1-RMD-090) — sunucu testleri.
  - src/Host/Experience/Orders/TableDraft/TableDraftService.cs (V1-WTR-034) — yalnız iki sınır sabiti Host içinde paylaşılır.
  - src/Modules/Orders/OrderAggregate/OrderItem.cs (V1-RMD-120) — satır adedi değişince seçenek adetleri orantılanır.
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs (V1-RMD-120) — orantılama testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `AddOrderItemRequest` isteğe bağlı `Modifiers` (id + isteğe bağlı adet) taşır; ad ve fiyat farkı katalogdan çözülür, istemci fiyat gönderemez.
2. Sunucu, ürünün gruplarının min/max kuralını garson yoluyla aynı yardımcılarla denetler: zorunlu grup seçimsizse veya en çok seçim aşılırsa 400; ürüne ait olmayan seçenek reddedilir.
3. Seçenekli istek her zaman yeni satır açar; seçimsiz bare satır birleşmesi korunur.
4. Seçenek grubu olan ürüne basılınca kasa ekranı Türkçe seçim penceresi açar (zorunlu/isteğe bağlı, tek/çoklu seçim, fiyat farkı), zorunlu grup tamamlanmadan onaylanamaz, seçimle birlikte gönderir.
5. Bağımsız inceleme bulgusu: kasa `+` düğmesi seçenekli satırın adedini değiştirince seçenek ücreti çarpılmıyordu (3 adet pide + büyük boy: 305 yerine 315 TL). `OrderItem.ChangeQuantity` seçenek adetlerini satırın yukarı yuvarlanmış adet oranıyla orantılar (seçenek her porsiyona gider, `BuildModifiers` varsayılanıyla aynı kural).
6. Aynı incelemeden: kasa yolu seçenek adedi ve sayısı için garson yolundaki sınırı (`MaximumOrderQuantity`, `MaximumModifiersPerItem`) uygulamıyordu; aynı sınırlar eklenir.

## Out of scope

- NFC, QR, çevrimiçi yolları; WaiterPwa ve vanilla kasa; şema/migration; seçenek fiyatının sepet gösterimi (ekran zaten satır toplamını sunucu anlık görüntüsünden alır).

## Dependencies

- V1-RMD-161
- V1-RMD-380

## Acceptance evidence

1. Sunucu testleri gerçek Postgres'te: zorunlu grup seçimsiz reddedilir, geçerli seçimle satır ve seçenek kaydı oluşur, ürüne ait olmayan seçenek reddedilir; düzeltmeden önce kırmızı, sonra yeşil.
2. Ön yüz testi: seçenek grubu olan ürüne basınca pencere açılır, zorunlu grup tamamlanmadan onay kapalı, onayda istek `modifiers` ile gider; seçenekli olmayan ürün doğrudan eklenir. Kırmızı sonra yeşil.
3. Mutasyon: sunucu grup denetimi çağrısını kaldırmak ve ön yüzde pencere koşulunu kaldırmak ilgili testleri kırmızıya çevirir; kaynaklar `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0; PosTerminal typecheck ve tüm vitest, DualScreen test projeleri yeşil.

## Handoff

- None
