# V1-RMD-494 - Host test tabanı onarımı

İki Host testi temiz commit'te de kırmızıydı (V1-RMD-493 kapanışında görüldü); ürün davranışı değişmeden kök nedenleri giderildi.

## Kök nedenler

- `ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog`: `PrivacyAnonymizationModule` serve kapsayıcısında `IAnonymizationPlans` istiyor, ama bu hizmet yalnız KVKK komut satırı yolunda elle oluşturuluyordu. Serve kapsayıcısı modül hizmetini çözemiyordu; `DualScreenApplication` artık `KvkkAnonymizationPlans`'ı kaydediyor.
- `AcceptingAQrOrderLocksEveryStockRowInTheGlobalOrderBeforeTheHoldTakesAny`: test şeması 174 numaralı migration'ı (`theoretical_consumption_records.modifier_id`, V1-RMD-486) içermiyordu; modifier'lı QR siparişi onayı sütun hatasıyla 503 veriyordu. Migration test şemasına eklendi.

## Kanıt

- `tests-Composition.log` 10/10, `tests-Orders.Confirmation.log` 30/30 (önce 9/10 ve 29/30).
- `tests-host.log`: tüm Host test projeleri; notlar dosyanın sonunda.
