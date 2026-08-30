# V1-RMD-053 - Order management store compilation alignment

- Task ID: V1-RMD-053
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`OrderManagementStore.cs` içerisindeki `order.Submit` çağrısının `Order` aggregate imza ve değişmezlik (immutability) sözleşmesiyle uyumlu hale getirilmesi, CS1503 derleme hatasının giderilmesi ve tüm çözümün derlenmesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-053-order-management-store-compilation-alignment.md`
- PO:2026-08-29 kararıyla src/Host/Experience/Orders/OrderManagementStore.cs yüzeyi V1-RMD-054'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-053/**`

## In scope

- `OrderManagementStore.cs` içinde `order = order.Submit(changedAt: DateTimeOffset.UtcNow);` dönüşümünü gerçekleştirmek.
- `dotnet build ALKAROS.slnx` komutunun 0 hata ve 0 uyarı ile tamamlandığını doğrulamak.

## Out of scope

- Diğer modüllerdeki domain kurallarını değiştirmek.

## Dependencies

- V1-GOV-030

## Deliverables

- Derlenen ve domain aggregate sözleşmesine tam uyan `OrderManagementStore.cs`.

## Acceptance evidence

- `dotnet build ALKAROS.slnx` exit code 0 verir.

## Handoff

- V1-GOV-031
