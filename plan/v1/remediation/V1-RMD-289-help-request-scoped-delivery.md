# V1-RMD-289 - Yardım çağrısı yalnız yetkili terminallere gider

- Task ID: V1-RMD-289
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`HelpRequestExperience.cs` çağrıyı `Clients.All` ile tüm bağlı istemcilere yayınlıyor; bağlanan her istemci başkasının masa çağrılarını alabilir. Yayın kasa rolündeki terminallerin grubuna daraltılır ve hub bağlantısı ilgili izinle yetkilendirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-289-help-request-scoped-delivery.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/HelpRequestExperience.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/HelpRequestHub.cs

## In scope

1. Grup tabanlı yayın, hub bağlantı yetkisi, testler.

## Out of scope

- İstemci bağlantı dayanıklılığı (`V1-RMD-286`).

## Dependencies

- V1-RMD-286

## Acceptance evidence

- Host testi: yetkisiz bağlantı olay almaz; kasa terminali alır.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
