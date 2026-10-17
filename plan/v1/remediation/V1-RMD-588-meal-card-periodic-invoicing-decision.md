# V1-RMD-588 - Yemek kartı firmalarına dönemlik faturalama kararı

- Task ID: V1-RMD-588
- Status: Planned
- Assignee: Unassigned
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Yemek kartıyla tahsil edilen satışlar için işletme dönem sonunda her kart firmasına ayrı fatura keser; komisyon en çok %6 ve ödeme fatura tarihinden itibaren en çok 30 gündür (Perakende Ticarette Uygulanacak İlke ve Kurallar Hakkında Yönetmelik m.12/A). Planda bu dönemlik faturalamayı ve firma bazlı alacak takibini üstlenen görev yoktur. Bu görev kapsamı, fatura türünü (e-Fatura veya e-Arşiv), KDV ve komisyon kurallarını ve alacak mutabakatını mali müşavirle netleştirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-588-meal-card-periodic-invoicing-decision.md`
- `docs/domain/meal-card-periodic-invoicing-decision.md`
- `evidence/V1-RMD-588/**`

## In scope

- Her kart firması için dönem (ör. haftalık/aylık), fatura türü, KDV ve komisyon hesabı, tahsil edilmeyen tutarın izlenmesi ve mali müşavir onayı kararı.

## Out of scope

- Kod yazmak: yemek kartı ödeme türü bugün kasada kayıtlı değildir (`TenderHandlerRegistryFactory`), faturalanacak veri V0-MCD-001 çözülene kadar oluşmaz.

## Dependencies

- V0-MCD-001

## Acceptance evidence

1. Mali müşavir onaylı karar belgesi.
2. Çıktılar `evidence/V1-RMD-588/` altındadır.

## Handoff

- None
