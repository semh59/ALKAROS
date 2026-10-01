# V1-RMD-484 - NFC siparişinde ekstra seçim ve zorunlu grup denetimi

- Task ID: V1-RMD-484
- Status: Done
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Müşteri NFC ile sipariş verirken `catalog.modifier_groups` kuralları (zorunlu/isteğe bağlı, en az/en çok seçim) hiç denetlenmiyor: zorunlu ekstra grubu olan bir ürün
seçimsiz sipariş edilip mutfağa gidebiliyor. Bu görev NFC isteğinin kalemlere ekstra seçimi taşımasını, sunucunun grupları personel yoluyla (`TableDraftService`) aynı kuralla
denetlemesini, fiyat farkının KDV dahil toplama yansımasını ve NFC sayfasında ekstra seçicisini sağlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-484-nfc-order-modifier-selection.md`
- `evidence/V1-RMD-484/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/NfcOrdering/NfcOrderingStore.cs, src/Host/Experience/NfcOrdering/NfcOrderingContracts.cs ve src/Host/Experience/NfcOrdering/NfcOrderingEndpoints.cs — yalnız ekstra seçimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/TableDraft/TableDraftService.cs — yalnız grup denetim yardımcısının Host içinde paylaşılabilir hale gelmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx, src/Clients/PosTerminal/src/api.ts, src/Clients/PosTerminal/src/contracts.ts — yalnız NFC ekstra seçicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/nfc-order.css — yalnız seçici paneli
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx, src/Clients/PosTerminal/src/api.test.ts — yalnız bu davranışın testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/NfcOrdering/NfcOrderingHttpTests.cs ve tests/Host/Experience/NfcOrdering/NfcOrderingTestDatabase.cs — yalnız ekstra testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `NfcOrderItemRequestDto` ekstra seçimi (`ModifierId`, `Quantity?`) taşır; sunucu ad/fiyatı katalogdan çözer, istemci fiyat gönderemez.
- Zorunlu grup eksikse, en çok seçim aşılırsa veya ürüne ait olmayan ekstra gelirse 400 `VALIDATION_FAILED`; tekrar gönderimde (replay) aynı sonuç.
- NFC sayfasında grup başına seçici, zorunlu grup seçilmeden sepete ekleme engeli, Türkçe mesajlar.

## Out of scope

- QR yolu (`V1-RMD-485`); reçete maliyeti ve rapor kırılımı; çevrimiçi platform ekstraları.

## Dependencies

- V1-RMD-161

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-484/` altındadır.

## Handoff

- V1-RMD-485
