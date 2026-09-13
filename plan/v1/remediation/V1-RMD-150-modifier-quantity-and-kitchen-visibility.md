# V1-RMD-150 - Eklenti adedi hep 1 yazılıyor ve mutfak onu göremiyor

- Task ID: V1-RMD-150
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in sorusu (2026-09-10): iki porsiyon Adana'ya eklenen ekstra pilav bir
kez mi iki kez mi ücretlendirilmeli? V1-RMD-147 eklentileri taşımaya başladı
ama adedini her zaman 1 yazıyor, yani iki tabaklık siparişte tek pilav
ücreti alınıyor ve mutfak da tek pilav görüyor. Soru yalnız fiyat sorusu
değil: aynı sayı hem ücreti hem mutfağın hazırlayacağı miktarı belirliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-150-modifier-quantity-and-kitchen-visibility.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementContracts.cs,
    src/Host/Experience/Orders/OrderManagementStore.cs (Orders Management
    sahipliğinde) — eklenti seçiminin adedi ve okuma tarafı.
  - src/Modules/Kitchen/TicketLifecycle/KitchenTicket.cs (V1-KIT-001
    sahipliğinde) — bilet özeti.
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-113/123 sahipliğinde),
    tests/Modules/Kitchen/TicketLifecycle/** (V1-KIT-001 sahipliğinde).

## In scope

1. **Adet siparişin kendi girdisi olur, katalog ayarı değil.**
   `OrderItemModifier.Quantity` alanı V1-ORD-001'den beri var ve
   `Total() = PriceDelta × Quantity` zaten onu kullanıyor — eksik olan tek
   şey birinin doldurması. İstemci eklenti seçerken adet de bildirebilir;
   `OrderItemDraftDto.Modifiers` bu yüzden düz `Guid` listesinden
   `OrderItemModifierSelectionDto` listesine geçer.
2. **Varsayılanı sunucu hesaplar: `Ceiling(kalem miktarı)`.** İstemci adet
   bildirmezse sunucu kalemin miktarını yukarı yuvarlar. İki porsiyon iki
   pilav; yarım porsiyon yine bir pilav, çünkü kesirli bir porsiyon da en az
   bir tabaktır ve o tabağa çıkan pilav yarım değildir. Frontend'in bu
   hesabı tekrar üretmesi gerekmez (`docs/design/foundations.md` §0).
3. **Mutfak adedi görür.** `KitchenTicket` bugün eklentileri yalnız isim
   listesi olarak özetliyor ("Ekstra pilav"); adet 1'den büyükse
   "2× Ekstra pilav" yazar. Adet ücreti belirleyip mutfağın gördüğü metni
   belirlememesi, ödenen ile hazırlanan arasında sessiz bir fark bırakırdı.
4. **Okuma tarafı adedi döndürür.** `OrderItemModifierDto` adedi taşır ki
   ekran ne ödendiğini gösterebilsin.

## Out of scope

- Eklenti grubuna "miktarla ölçeklenir mi" bayrağı eklemek (migration):
  gerekmiyor. Ölçeklenme kararı sipariş anında zaten belli — garson iki
  tabak mı bir tabak mı istediğini biliyor ve adedi görüp değiştirebiliyor.
  Katalogda sabitlemek, aynı eklentinin iki farklı siparişte farklı
  davranması gereken durumu ifade edemezdi.
- Eklentilerin kendi stoklarını tüketmesi. Bu görevde araştırıldı:
  `ProductType.Modifier` enum'da tanımlı ama ne kodda ne veride
  kullanılıyor, ve `catalog.modifiers` bağımsız bir tablo —
  `inventory.product_stock_mappings` yalnız `catalog.products` satırlarına
  bağlanabildiği için bir eklentinin bugünkü şemayla stok eşlemesi
  kurulamaz. Gerçek bir malzeme tüketen eklenti (ekstra peynir) için ayrı
  bir eşleme gerekir; Semih'in ayrı kararı ve kendi görevi.
- `min_selections`/`max_selections` doğrulaması (V1-RMD-148'in aynı kaydı).

## Dependencies

- V1-RMD-147

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı,
  gerçek çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **29/29** (26'dan).
    Üç yeni senaryo: iki porsiyonluk kalemde eklenti adedi 2 ve tutar
    520×2 + 120×2 = 1.280 (adet 1'e sabitken 1.160'ta kalırdı); yarım
    porsiyonda adet 1; istemcinin açıkça bildirdiği adet (3) varsayılanı
    eziyor.
  - `ALKAROS.Kitchen.TicketLifecycle.Tests`: **20/20** (19'dan). Yeni test
    özetin "2× Ekstra pilav, Az acılı" ürettiğini — yani adedi 1'den büyük
    olanın işaretlenip diğerinin eski biçimini koruduğunu — doğruluyor.
  - `ALKAROS.Kitchen.PrintQueue.Tests`: 27/27,
    `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: 19/19,
    `...NfcOrdering.Tests`: 17/17, `ALKAROS.Host.Tests`: 134/134.
- Migration yok — `orders.order_item_modifiers.quantity` NUMERIC(18,3)
  olarak V1-ORD-001'den beri yerinde.
- Semih'in elle deneyebileceği senaryo: ücretli bir eklentisi olan bir
  üründen iki porsiyon gönder; mutfak biletinde "2× `<eklenti>`" yazdığını ve
  kalem tutarının eklenti farkını iki kez içerdiğini gör; sonra aynı üründen
  yarım porsiyon gönder ve eklentinin bir kez ücretlendirildiğini gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None
