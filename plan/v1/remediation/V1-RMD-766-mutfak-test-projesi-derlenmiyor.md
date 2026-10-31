# V1-RMD-766 - KitchenOperations test projesi derlenir

- Task ID: V1-RMD-766
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: test
- Surface state: Existing

## Goal

`IOrderRepository` iki `ReassignServingUserAsync` aşırı yüklemesi taşır (biri çağıranın bağlantı ve transaction'ını alır, V1-RMD-722). `tests/Host/Experience/KitchenOperations/KitchenOperationsStore.NotificationDispatchTests.cs` içindeki `FakeOrderRepository` yalnız ilkini uygular; sonuç: test projesi HEAD'de CS0535 ile derlenmez ve içindeki hiçbir test çalışmaz.

Sahte sınıfa eksik aşırı yükleme eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-766-mutfak-test-projesi-derlenmiyor.md`
- `evidence/V1-RMD-766/**`
- Sınırlı ek: tests/Host/Experience/KitchenOperations/KitchenOperationsStore.NotificationDispatchTests.cs

## In scope

- `FakeOrderRepository`'ye bağlantı ve transaction alan `ReassignServingUserAsync` aşırı yüklemesi (diğerleri gibi `NotSupportedException`).
- Test projesinin derlenmesi ve mevcut testlerinin koşulması; sonuç kaydedilir.

## Out of scope

- Üretim kodu ve diğer test projelerinin derleme denetimi.
- Başarısız çıkan mevcut testlerin düzeltilmesi (varsa ayrı görev).

## Dependencies

- V1-RMD-722

## Acceptance evidence

1. Düzeltmeden önce `dotnet build` CS0535 ile kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra proje 0 uyarı 0 hata derlenir ve test paketi koşulur; sonuç (geçen/düşen) kaydedilir.
3. Kapsam aracı InProgress baseline'a karşı exit 0 verir.

## Handoff

- None
