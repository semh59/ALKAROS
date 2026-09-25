# V1-RMD-295 - Karar: çevrimdışı yetki mutabakatı uç noktasının istemcisi

- Task ID: V1-RMD-295
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`/api/v1/terminals/{id}/offline-reconciliation` (`OfflineReconciliationEndpoints.cs`) uç noktasını hiçbir istemci çağırmıyor. Ya kasa/garson bağlantı geri geldiğinde bunu çağırmalı (istemci eksik) ya da uç nokta artık gerekmiyor. Bu görev karar kaydıdır: `IOfflineGrantReconciler` akışını okuyup istemcinin çağırıp çağırmaması gerektiğini, gerekiyorsa hangi istemcinin ne zaman çağıracağını yazar; kod değişikliği ayrı görev olarak açılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-295-offline-reconciliation-client-decision.md`

## In scope

1. Kod okuması, karar kaydı ve gerekiyorsa uygulama görevinin açılması.

## Out of scope

- Uygulama.

## Dependencies

- V1-RMD-285

## Acceptance evidence

- Karar kaydı: çağıran ve zamanlama ya da uç noktanın gerekçeli kaldırılması; uygulama görevi ya da kaldırma görevi açıldı.

## Handoff

- None
