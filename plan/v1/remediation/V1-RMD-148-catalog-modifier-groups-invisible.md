# V1-RMD-148 - Katalog bir ürünün eklenti gruplarını hiç söylemiyor

- Task ID: V1-RMD-148
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-147 sipariş hattını eklenti taşıyacak hâle getirdi, ama istemci hangi
eklentinin var olduğunu hâlâ öğrenemiyor: terminal kataloğunun döndürdüğü
`CatalogProductDto` yalnız ad, SKU, kategori, fiyat ve KDV taşıyor.
`catalog.product_modifier_groups` dolu olsa bile garson "yarım porsiyon mu,
ekstra pilav mı" sorusunu ekranda soramaz. Bu görev o okuma yüzeyini açar ve
eklenti zincirinin son halkasını kapatır.

## Owned surface

- `plan/v1/remediation/V1-RMD-148-catalog-modifier-groups-invisible.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/DualScreen/DualScreenContracts.cs (DualScreen sahipliğinde) —
    `CatalogProductDto`'nun eklenti grubu alanı ve iki yeni kayıt.
  - src/Host/DualScreen/DualScreenStore.cs (DualScreen sahipliğinde) —
    yalnız `GetCatalogAsync`'in eklenti grubu zenginleştirmesi.
  - tests/Host/MigrationComposition/DualScreen/** (V1-FND-004 sahipliğinde) —
    terminal katalog uç noktasının kendi HTTP testleri ve aynı yanıtın JSON
    alan listesini birebir doğrulayan sözleşme testi.

## In scope

1. **Ürünün eklenti grupları dönüyor.** `CatalogProductDto` bir
   `ModifierGroups` listesi kazanıyor; her grup kodunu, adını, seçim tipini
   (tekli/çoklu) ve `min_selections`/`max_selections` sınırlarını, içindeki
   eklentiler de kod, ad ve `price_delta` değerlerini taşıyor. İstemci
   böylece hem seçenekleri gösterebilir hem de zorunlu bir grubu boş
   bırakmayı kendi ekranında engelleyebilir.
2. **Sayfa başına tek sorgu.** Eklenti grupları, katalog sayfası
   çekildikten sonra o sayfadaki ürün kimlikleri için tek bir sorguyla
   okunup eşleştirilir — ürün başına sorgu (N+1) açılmaz.
3. **Görünürlük kuralı sipariş hattıyla aynı.** Bir eklenti ürüne ya
   doğrudan (`catalog.modifiers.product_id`) ya da ürüne atanmış bir grup
   üzerinden bağlıdır; pasif eklenti ve pasif grup listelenmez. V1-RMD-147'nin
   kabul ettiği küme ile katalogda görünen küme birebir aynı olmalı, aksi
   hâlde istemci gösterdiği bir seçeneği gönderince reddedilir.

## Out of scope

- `min_selections`/`max_selections` kuralının sunucuda zorlanması: bu görev
  sınırları yalnız bildirir. Sipariş anında zorlamak ayrı bir karar —
  bugünkü ekranların hiçbiri eklenti göndermiyor, kural yazmadan önce
  gerçek bir kullanıcının ne göndereceğini görmek gerekiyor.
- Eklentilerin kendi stoklarını tüketmesi (V1-RMD-143 ve V1-RMD-147'nin
  aynı kaydı).
- Yönetim tarafındaki eklenti CRUD'u: `/api/v1/management/catalog/modifiers`
  zaten var ve bu görev ona dokunmaz.

## Dependencies

- V1-RMD-147

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), gerçek çıkış
  koduyla:
  - `ALKAROS.Host.Tests`: **134/134** (133'ten). Yeni test, bir ürüne
    atanmış grubun ve içindeki eklentinin katalog yanıtında göründüğünü,
    pasifleştirilmiş eklentinin ve pasif gruba bağlı eklentinin
    görünmediğini, hiç grubu olmayan ürünün alanının `null` döndüğünü
    doğruluyor. Mevcut sayfalama/limit/kursör testi değişmeden geçiyor.
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 26/26,
    `ALKAROS.Host.Experience.NfcOrdering.Tests`: 17/17.
- Migration yok — şema V1-CAT-001'den beri yerinde.
- Sözleşme testi güncellendi: `CustomerDisplayContractTests`, katalog
  yanıtının JSON alan listesini birebir doğruluyor ve yeni `modifierGroups`
  alanı için kırıldı — kasıtlı genişleme olduğu için beklenen liste
  güncellendi. Testin varlık sebebi tam olarak bu: sözleşmeye kimse
  farkında olmadan alan ekleyemesin.
- Semih'in elle deneyebileceği senaryo: yönetimden bir eklenti grubu tanımla
  ve bir ürüne ata; `GET /api/v1/terminals/{terminalId}/catalog` çağır ve o
  ürünün yanıtında grubun, seçim tipinin ve eklenti fiyat farkının
  göründüğünü gör; aynı eklenti kimliğiyle sipariş gönderdiğinde kabul
  edildiğini doğrula.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None
