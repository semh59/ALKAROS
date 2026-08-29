# V1-RMD-039 - Table floor plan integration and reservations

- Task ID: V1-RMD-039
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

`TableRoute` üzerinden floor plan verilerini yükleyip `TableWorkspace`'e aktarmak; rezervasyon formuna kişi sayısı (party size) alanını eklemek; masa birleştirme/ayırma (merge/unmerge) akışında gerçek `mergeGroupId` ve row version kullanımını sağlamak.

## Owned surface

- PO:2026-08-29 kararıyla src/Clients/PosTerminal/src/features/tables/** yüzeyi V1-RMD-052'ye devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-039/**`

## Dependencies

- V1-RMD-037

## Acceptance evidence

- `TableRoute` floor plan verilerini yükler ve çalışma alanına bağlar.
- Rezervasyon oluşturulurken kişi sayısı doğru iletilir.
- Merge/unmerge işlemleri Host API üzerinde doğrulanır.

## Handoff

- V1-RMD-042
