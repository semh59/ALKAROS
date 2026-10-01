# V1-RMD-485 - QR siparişinde ekstra seçim ve zorunlu grup denetimi

- Task ID: V1-RMD-485
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Müşteri QR menüsünden sipariş verirken ekstra seçim yok ve zorunlu ekstra grubu denetlenmiyor (`V1-RMD-484` ile aynı boşluk). Bu görev QR isteğinin, `qr-ordering.order-submitted.v1`
olayının ve siparişe dönüştüren tüketicinin ekstra seçimini taşımasını, `QrPendingOrderStore` içinde grup kurallarının denetlenmesini ve müşteri sayfasında seçiciyi sağlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-485-qr-order-modifier-selection.md`
- `evidence/V1-RMD-485/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/QrOrdering/PendingOrders/QrOrderSubmissionContracts.cs ve src/Modules/QrOrdering/PendingOrders/QrPendingOrderStore.cs — yalnız ekstra seçimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/IntegrationContracts/QrOrderIntegrationEvents.cs — yalnız isteğe bağlı ekstra alanı (eski kuyruktaki iletiler çalışmaya devam eder)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/Integration/QrOrderSubmittedConsumer.cs — yalnız ekstraları siparişe taşıma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Menu/wwwroot/menu-app.js ve src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.js — yalnız ekstra seçicisi ve sepet satır kimliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Menu/wwwroot/menu-app.css ve src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.css — yalnız seçici paneli
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs, tests/Modules/QrOrdering/PendingOrders/QrPendingOrderStoreTests.cs, tests/Modules/Orders/OrderAggregate/QrOrderSubmittedConsumerTests.cs ve tests/Apps/CustomerWeb — yalnız ekstra testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sepet satırı kimliği ürün + seçilen ekstralar olur (aynı ürün farklı ekstralarla ayrı satır); istek ve olay ekstra kimliği/adedi taşır, ad ve fiyat sunucuda katalogdan çözülür.
- Zorunlu grup eksik, en çok seçim aşıldı veya ekstra ürüne ait değil: 400 `VALIDATION_FAILED`.
- Müşteri menüsünde (zaten dönen `modifierGroups` ile) seçici, Türkçe metinler.

## Out of scope

- NFC yolu (`V1-RMD-484`); reçete maliyeti ve rapor kırılımı; çevrimiçi platform ekstraları.

## Dependencies

- V1-RMD-484

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-485/` altındadır.

## Handoff

- None
