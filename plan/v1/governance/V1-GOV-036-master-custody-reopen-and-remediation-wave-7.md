# V1-GOV-036 - Master custody reopen and remediation wave 7

- Task ID: V1-GOV-036
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Derin mimari denetimde tespit edilen katalog fiyat `updated_at` sorgu çökmesi, masa taslaklarında mükerrer kalem birikmesi, mutfak bilet terfisi sonrası 409 durum çakışması, garson auth bearer başlık eksikliği ve docker/provenance uyumsuzlukları doğrultusunda `GATE-V1-EXIT` kapısını resmen yeniden açmak ve 5 adımlık 7. kurtarma dalgasını (`V1-RMD-063..067`, `V1-GOV-037`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-036-master-custody-reopen-and-remediation-wave-7.md`
- `plan/v1/remediation/V1-RMD-063-catalog-price-schema-alignment.md`
- `plan/v1/remediation/V1-RMD-064-order-items-orphan-deletion.md`
- `plan/v1/remediation/V1-RMD-065-kitchen-ticket-idempotency.md`
- `plan/v1/remediation/V1-RMD-066-order-management-auth-header.md`
- `plan/v1/remediation/V1-RMD-067-build-provenance-and-docker-hygiene.md`
- `plan/v1/governance/V1-GOV-037-wave7-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-036/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açmak.
- 193 görevlik güncel matrisi doğrulamak (187 Done, 4 NotApplicable, 5 Planned, 1 InProgress).
- `V1-RMD-063..067` ve `V1-GOV-037` görev dosyalarını oluşturmak.

## Out of scope

- Production kodunu bu governance görevi içinde doğrudan değiştirmek.

## Dependencies

- V1-GOV-035

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-063..067`, `V1-GOV-037`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` 0 hata ve 0 uyarı verir.
- `GATE-V1-EXIT` kapısı açık olarak belgelenir.

## Handoff

- V1-RMD-063
