# V1-RMD-044 - PosTerminal compile and floor plan freshness

- Task ID: V1-RMD-044
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`PosTerminal/App.tsx` içindeki `BillSplitWorkspace` prop uyumsuzluklarından kaynaklanan üç `TS2322` derleme hatasını düzeltmek; `TableRoute` bileşeninde `floorPlan` API çağrısını ve prop aktarımını tamamlamak; `ExperienceRoute` içindeki `freshness` hesaplamasını gerçekçi arka uç durumuna bağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-044-posterminal-compile-and-floor-plan-freshness.md`
- `src/Clients/PosTerminal/src/App.tsx`
- `evidence/V1-RMD-044/**`

## In scope

- `App.tsx` içinde `BillSplitWorkspace` bileşenine `design`, `onSave`, `onClear` prop'larını tip uyumlu hale getirerek TS2322 hatalarını çözmek.
- `TableRoute` içinde `client.getFloorPlan(zoneId)` çağrısı ekleyerek `TableWorkspace` bileşenine `floorPlan`, `floorPlanBusy`, `floorPlanError`, `onSaveFloorPlan` prop'larını aktarmak.
- `ExperienceRoute` içinde `freshness` nesnesini `backendStatus` ve veri güncelleme zamanına göre dinamik olarak hesaplamak.

## Out of scope

- C# backend veya Dockerfile bileşenlerini değiştirmek.

## Dependencies

- V1-GOV-026

## Deliverables

- Sıfır TypeScript hatası ile derlenen ve tip kontrolünden geçen `PosTerminal`.
- Floor plan harita görünümü çalışan `TableRoute`.
- Gerçekçi `freshness` göstergesi.

## Acceptance evidence

- `PosTerminal` `App.tsx` derleme ve tip kontrolü sıfır hata ile tamamlanır.

## Handoff

- V1-RMD-045
