# V1-RMD-1192 - Seçenek adları ve ek ücretleri kasiyer ekranlarında görünür

- Task ID: V1-RMD-1192
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 1): seçenek (modifier) adları kasiyer ekranlarında görünmez. Sepet satırında, masa detay panelinde, ön hesap fişinde ve ödenmiş hesap fişinde seçenekli Hamburger yalnız "₺165,00" yazar; "Sade" ile "Az acılı" birbirinin aynısı iki satırdır ve 15 TL fark açıklamasızdır. Sunucu seçenekleri zaten kaydeder (`orders.order_item_modifiers`: ad ve ücret kopyası); mutfak ve NFC görür, kasiyer görmez.

1. Sunucu: `LineOptionV1(Name, Price)` (adet 1'den büyükse ad "2× ad" öneki alır, `Price` = ek ücret x adet) sözleşmesi. Kasiyer anlık görüntüsü satırı (`CustomerDisplayLineDto`) isteğe bağlı `Options` taşır; yalnız kasiyer görünümünde (`includeLineIds`) ve seçeneği olan satırda yazılır, müşteri ekranına sızmaz. Ön hesap fişi satırı (`BillPreReceiptLineV1`, hesap yokken sipariş kaleminden, hesap varken fatura kaleminin sipariş kaleminden; birleşik hesapta kalemin kendi siparişinden) ve ödenmiş hesap fişi satırı (`BillReceiptItemV1`) aynı alanı taşır. Sunucu eski alanları değiştirmez; alan yoksa JSON'da yazılmaz.
2. İstemci: her yerde seçenek adı + varsa ek ücret (örn. "Ekstra peynir +₺15,00"), 14 px, kısa, kalemin altında; ad metin olarak yazılır (XSS yok). Yerler: PosTerminal sepet satırı (`Cashier.tsx`), masa detay paneli (`TableOrderBridge.tsx`, sunucu `modifiers` zaten döner), ön hesap fişi (`preReceipt.ts`), ödenmiş hesap fişi (`split-payment.js` `receiptHtml`).
3. Birleştirme kuralı: sunucu yalnız seçeneksiz ve notsuz çıplak kalemi aynı ürünle birleştirir (`DualScreenStore.Orders.cs`); farklı seçenekli aynı ürün ayrı satır kalır. Bu kural testle kilitlenir (kod değişmez; değişirse görev durur).
4. `docs/domain/bill-pre-receipt.md` güncellenir.

## Owned surface

- `evidence/V1-RMD-1192/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenContracts.cs
  - src/Host/DualScreen/DualScreenStore.Display.cs
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.line-options.test.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.ts
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.test.ts
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-receipt.test.js
  - docs/domain/bill-pre-receipt.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu: kasiyer anlık görüntüsü, ön fiş hesap yokken/varken, ödenmiş fiş; istemci: dört yerde seçenek adı ve ücret, ücretsiz seçenek, farklı seçenekli iki satır ayrı), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. PaymentTender ve DualScreen ilgili HTTP/store testleri, ApiConventions, PosTerminal ilgili vitest dosyaları, StaticApps ilgili testler ve istemci üretim derlemesi

## Out of scope

- Mutfak sözleşmesi (V1-RMD-1193); müşteri ekranı; garson PWA; iade paneli kalem listesi; mutfak fişi; varsayılan seçenek kavramı

## Dependencies

- V1-RMD-1191

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
