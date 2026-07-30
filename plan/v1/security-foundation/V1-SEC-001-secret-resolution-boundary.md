# V1-SEC-001 - Implement secret resolution boundary

- Task ID: V1-SEC-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

İlk dış entegrasyondan önce provider credential, encryption key ve signing secret erişimini güvenli sınırda uygulamak.

## Owned surface

- `src/BuildingBlocks/Security/Secrets/**`, `tests/BuildingBlocks/Security/Secrets/**`
- Bu görev provider-specific credential adı veya production secret değeri oluşturamaz.

## In scope

- Typed secret reference, least-privilege read, startup validation, redacted failure ve test secret provider.

## Out of scope

- Production secret provisioning, provider contract ve rotation operasyonu.

## Dependencies

- V1-FND-005
- V0-ARC-005
- V0-SEC-001

## Deliverables

- `src/BuildingBlocks/Security/Secrets/**` altında secret resolution production code'u.
- Missing, denied, malformed ve redaction testleri.

## Acceptance evidence

- Secret değeri source, settings, database, exception, log veya test snapshot içinde görünmez.
- Eksik ya da yetkisiz secret, ilgili integration başlamadan typed failure üretir.

## Handoff

- V1-SEC-002
- V12-HUG-001
- V13-QNB-001
- V14-ONL-001
