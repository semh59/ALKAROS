# V1-RMD-395 - Production deneyimi kendi IUnitConverter bağımlılığını kaydetsin

- Task ID: V1-RMD-395
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-344, `ProductionStockEffectService`'e `IUnitConverter` bağımlılığı ekledi ama
`AddProductionManagementExperience` onu kaydetmiyor. Tam host'ta başka deneyimler kaydettiği için üretimde
çalışıyor; deneyimin kendi test host'unda ise parti tamamlama 500 dönüyor ve
`tests/Host/Experience/Production` o commit'ten beri kırmızı (V1-RMD-393 F-03). Deneyim, diğer
deneyimlerle aynı `TryAddSingleton<IUnitConverter, UnitConverter>()` kaydını yapar.

## Owned surface

- `plan/v1/remediation/V1-RMD-395-production-experience-registers-unit-converter.md`
- Sınırlı ek (V1-RMD-133 sahipliğinde kalır):
  src/Host/Experience/Production/ProductionManagementEndpoints.cs

## Dependencies

- V1-RMD-394

## Acceptance evidence

- `dotnet test tests/Host/Experience/Production` exit 0 (bugün 2 test 500 ile kırmızı).
- Senaryo: yönetim ekranından bir üretim partisi tamamlanır; stok tüketimi ve çıktı kaydedilir.
