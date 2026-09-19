# V1-RMD-240 - Document RelaySettings' implicit dependency on QrOrderingModule's DI chain

- Task ID: V1-RMD-240
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`RelaySettingsEndpoints.AddRelaySettingsExperience` resolves
`IRelayCredentialStore` (`PostgresRelayCredentialStore`), whose constructor
needs `SensitivePayloadProtector`/`ISecretResolver`/`IEnvelopeCipher`/
`ISecretProvider`/`ISecretAccessPolicy`/`ISensitiveDataAccessPolicy` — none
of which this method registers. It only works because the real Host always
also loads `QrOrderingModule` (which registers that whole chain first).
`QnbCredentialSettingsEndpoints.cs`/`TokenTerminalSettingsEndpoints.cs`
document their own equivalent cross-module dependencies explicitly;
`RelaySettingsEndpoints.cs` did not. A bağımsız denetim ajanı (2026-09-18,
tüm proje kod denetimi, Invoicing/QrOrdering alanı) bunu tespit etti:
gelecekte modül sırası değişirse veya bu extension metodu farklı bir
kompozisyonda tek başına kullanılırsa sessiz bir `InvalidOperationException`
riski var.

## Owned surface

- `evidence/V1-RMD-240/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/RelaySettings/RelaySettingsEndpoints.cs
  (V12-QRT-003 sahipliğinde kalır) — yalnız bir doc-comment eklenir, hiçbir
  davranış değişmez.

## In scope

- `AddRelaySettingsExperience`'ın üstüne, `IRelayCredentialStore`'un
  şifreleme zincirinin `QrOrderingModule` tarafından ayrıca kaydedildiğini
  ve gerçek Host'ta her zaman birlikte yüklendiğini; standalone bir
  kompozisyonun bu zinciri elle kaydetmesi gerektiğini (bkz.
  `tests/Host/Experience/RelaySettings/RelaySettingsHttpTests.cs`'in kendi
  fixture'ı) açıklayan bir yorum.

## Out of scope

- Zincirin kendisini burada tekrar kaydetmek (bu, `TokenTerminalCredentialAccessPolicy`'nin
  kendi doc-comment'inin bilerek kaçındığı global singleton çakışma riskini
  geri getirir).

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (davranış değişmedi,
  yalnız yorum).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
