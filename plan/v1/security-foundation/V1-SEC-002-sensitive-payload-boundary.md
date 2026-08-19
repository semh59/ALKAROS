# V1-SEC-002 - Implement sensitive payload boundary

- Task ID: V1-SEC-002
- Status: Done
- Assignee: opencode-v1-sec-002
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

Payment, fiscal, invoice ve webhook payload'larının saklama, şifreleme, maskeleme ve log sınırını uygulamak.

## Owned surface

- `src/BuildingBlocks/Security/SensitiveData/EnvelopeKeyCodec.cs`
- `src/BuildingBlocks/Security/SensitiveData/IPayloadRedactor.cs`
- `src/BuildingBlocks/Security/SensitiveData/IRetentionPolicyHook.cs`
- `src/BuildingBlocks/Security/SensitiveData/ISensitiveDataAccessPolicy.cs`
- `src/BuildingBlocks/Security/SensitiveData/MaxAgeRetentionPolicyHook.cs`
- `src/BuildingBlocks/Security/SensitiveData/PayloadRedactor.cs`
- `src/BuildingBlocks/Security/SensitiveData/SensitiveCategory.cs`
- `src/BuildingBlocks/Security/SensitiveData/SensitiveDataEncryptionException.cs`
- `src/BuildingBlocks/Security/SensitiveData/SensitiveDataException.cs`
- `src/BuildingBlocks/Security/SensitiveData/UnauthorizedSensitiveReadException.cs`
- `tests/BuildingBlocks/Security/SensitiveData/Authorization/UnauthorizedReadTests.cs`
- `tests/BuildingBlocks/Security/SensitiveData/Encryption/KeyFailureTests.cs`
- `tests/BuildingBlocks/Security/SensitiveData/Protection/EnvelopeProtectionTests.cs`
- `tests/BuildingBlocks/Security/SensitiveData/Retention/RetentionPolicyHookTests.cs`
- `tests/BuildingBlocks/Security/SensitiveData/ALKAROS.SensitiveData.Tests.csproj`
- V0-GOV-013 tarafindan remediated metadata-authentication dosyalari bu task'in
  yuzeyinden devredilmistir; V0-GOV-013 bu task'a dependency ile siralanir.
- Kapsam genişletme onayı (2026-07-31 kullanıcı talimatı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve
  `build/project-manifest.json` içine kaydı.
- Bu görev provider payload schema veya business retention süresi belirleyemez.
- C52 immutable SensitivePayload classification surface is transferred to V1-SEC-005; this historical task remains
  closed.

## In scope

- Field classification, envelope encryption contract, redaction, authorized read ve retention-policy hook.

## Out of scope

- Provider mapping, customer anonymization workflow ve production key rotation.

## Dependencies

- V1-SEC-001
- V0-CMP-003
- V0-SEC-001

## Deliverables

- `src/BuildingBlocks/Security/SensitiveData/**` altında data-protection production code'u.
- Plaintext persistence, log leakage, unauthorized read ve key failure testleri.

## Acceptance evidence

- Sınıflandırılmış payload hiçbir persistence/log yolunda plaintext kalmaz; authorized read dışında çözülmez.
- Encryption veya classification failure veri yazmadan fail-closed sonuç üretir.

## Handoff

- V1-FND-002
- V12-HUG-001
- V13-QNB-002
- V14-ONL-001
- V15-SEC-003
