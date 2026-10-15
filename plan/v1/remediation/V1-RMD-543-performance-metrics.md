# V1-RMD-543 - Performans ölçümleri ve yüzdelikler

- Task ID: V1-RMD-543
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Her eylem ve uç nokta için istek sayısı, hata oranı ve süre yüzdelikleri (p50, p95, p99) dakikalık özetlerle saklanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-543-performance-metrics.md`
- `evidence/V1-RMD-543/**`
- `src/Modules/Observability/PerformanceMetrics/**`
- `tests/Modules/Observability/PerformanceMetrics/**`
- `database/migrations/V1/V1-RMD-543/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Dakikalık özet tablosu (`metric_rollups`); ham veri kısa, özetler uzun saklanır.
- Ölçümler: istek süresi, veritabanı komut süresi, kuyruk derinliği, ekran etkileşim süresi, hata oranı.

## Out of scope

- Bütçe ve alarm kuralları.

## Dependencies

- V1-RMD-533
- V1-RMD-542

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-543/` altındadır.

## Handoff

- None
