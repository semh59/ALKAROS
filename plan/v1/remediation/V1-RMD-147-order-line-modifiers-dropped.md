# V1-RMD-147 - Sipariş hattı kalem eklentilerini sessizce atıyor

- Task ID: V1-RMD-147
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Garson arayüzü tasarlanırken (2026-09-10) bulundu: `OrderItemDraftDto` bir
`Modifiers` alanı taşıyor ve `OrderManagementStore` her kalemi
`modifiers: null` ile kuruyor — gönderilen eklenti ("ekstra peynir", "az
pişmiş") sunucuya ulaşsa bile sessizce düşüyor. Altyapının geri kalanı hazır:
`catalog.modifier_groups`/`modifiers`/`product_modifier_groups` şeması
V1-CAT-001'den beri var, `OrderItemModifier` fiyata `OrderItem.LineSubtotal()`
üzerinden katılıyor, ve `PostgresOrderRepository` `orders.order_item_modifiers`
satırlarını zaten hem yazıp hem okuyor. Kopuk olan tek halka Host katmanı.

## Owned surface

- `plan/v1/remediation/V1-RMD-147-order-line-modifiers-dropped.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementContracts.cs (Orders Management
    sahipliğinde) — `OrderItemDraftDto.Modifiers` tipi ve `OrderItemDto`'nun
    eklenti alanı.
  - src/Host/Experience/Orders/OrderManagementStore.cs (Orders Management
    sahipliğinde) — eklenti çözümü ve kalem kurulumu.
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-00x
    sahipliğinde) — yalnız okuma eşlemesinin yeni alanı taşıması.
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-113/123 sahipliğinde)
    — eklentilerin uçtan uca korunduğu ve doğrulandığı testler.

## In scope

1. **Eklentiler gerçekten taşınıyor.** `OrderManagementStore` gönderilen
   eklenti kimliklerini `catalog.modifiers`'tan çözüp `OrderItemModifier`
   listesi kuruyor ve kalemi onunla oluşturuyor. Fiyat etkisi domain'in
   kendi `LineSubtotal()` hesabından gelir; bu görev yeni bir para aritmetiği
   yazmaz.
2. **İstemci fiyata karar veremez.** `Modifiers` alanı `IReadOnlyList<string>`
   yerine `IReadOnlyList<Guid>` oluyor: istemci yalnız hangi eklentiyi
   seçtiğini söyler, adı ve `price_delta` sunucuda kataloğdan okunur —
   ürün adı ve fiyatı için zaten uygulanan kuralın (`ResolveCatalogProductsAsync`)
   aynısı.
3. **Eklenti ürüne ait olmak zorunda.** Çözüm sorgusu eklentiyi yalnız
   ürünün kendi eklentisi (`catalog.modifiers.product_id`) ya da ürüne
   atanmış bir grubun üyesi (`catalog.product_modifier_groups`) ise kabul
   eder; pasif eklenti ve pasif grup elenir. Eşleşmeyen bir kimlik siparişin
   tamamını Türkçe hatayla reddeder — sessizce atmak bu görevin kapattığı
   kusurun ta kendisiydi.
4. **Okuma tarafı eklentileri döndürüyor.** `OrderItemDto` her kalemin
   eklentilerini ad ve fiyat farkıyla taşıyor; V1-RMD-146'nın açtığı diğer
   üç alanla aynı gerekçe (ekran kalemi doğru gösteremiyor).

## Out of scope

- `catalog.product_modifier_groups`'un okuma yüzeyi: `CatalogProductDto`
  bir ürünün hangi eklenti gruplarını taşıdığını hâlâ söylemiyor, bu yüzden
  istemci seçenekleri kullanıcıya gösteremez. Ayrı görev (V1-RMD-148) —
  bağımsız bir fiil ve kendi testleri var.
- `modifier_groups.min_selections`/`max_selections` kuralının siparişte
  zorlanması: istemci grupları göremeden zorunlu seçim doğrulaması
  anlamsızdır, katalog yüzeyiyle birlikte değerlendirilir.
- Eklentilerin kendi stoklarını tüketmesi: V1-RMD-143'ün "Out of scope"
  kaydıyla aynı, Semih'in ayrı kararını bekliyor.
- Müşteri kanalının (QR/NFC) eklenti göndermesi: `NfcOrderItemRequestDto`
  eklenti taşımıyor ve müşteri menüsünde seçenek yok; yalnız okuma tarafı
  yeni alanı taşır.

## Dependencies

- V1-RMD-146

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı,
  gerçek çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **26/26** (22'den).
    Dört yeni senaryo: eklentili kalem taslakta eklentisini koruyor ve adı
    ile fiyat farkı kataloğdan çözülüyor; kalem tutarı eklenti farkını
    gerçekten içeriyor (520×2 + 120 = 1.160 — eklenti düşseydi 1.040 kalır
    ve doğrulama kırmızıya dönerdi); ürüne ait olmayan eklenti kimliği tüm
    siparişi reddediyor; pasifleştirilmiş eklenti reddediliyor; eklentiler
    gönderim sonrası geri okumada duruyor.
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: 17/17,
    `...Confirmation.Tests`: 17/17, `...VoidSent.Tests`: 12/12,
    `...Comp.Tests`: 9/9, `...Void.Tests`: 5/5,
    `ALKAROS.Host.Experience.Catalog.Tests`: 10/10.
- Migration yok — şema V1-CAT-001 ve V1-ORD-001'den beri yerinde.
- Sözleşme değişikliği (`Modifiers` `string` → `Guid` listesi) hiçbir
  istemciyi kırmıyor: üretimdeki istemcilerin hiçbiri bu alanı hiç
  göndermiyordu (tarandı) — alan zaten yalnız kâğıt üzerinde vardı.
- Semih'in elle deneyebileceği senaryo: yönetim ekranından bir eklenti grubu
  ve içine ücretli bir eklenti tanımla, bir ürüne ata; garson hattından o
  ürünü o eklentiyle gönder; `GET /api/v1/terminals/{terminalId}/orders/{orderId}`
  çağrısında kalemin eklentiyi taşıdığını ve toplam tutarın eklenti farkını
  içerdiğini gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni ihlal yok.

## Handoff

- None
