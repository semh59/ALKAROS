# V1-RMD-543 - Performans ölçümleri ve yüzdelikler

- Task ID: V1-RMD-543
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Her eylem ve uç nokta için istek sayısı, hata oranı ve süre dağılımı (histogram) dakikalık özetlerle saklanır; yüzdelikler (p50, p95, p99) histogramdan hesaplanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-543-performance-metrics.md`
- `evidence/V1-RMD-543/**`
- `src/Modules/Observability/PerformanceMetrics/**`
- `tests/Modules/Observability/PerformanceMetrics/**`
- `database/migrations/V1/V1-RMD-543/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Dakikalık histogram özet tablosu; yüzdelikler dakikalık yüzdeliklerin ortalamasından değil histogramın toplanmasından hesaplanır.
- Ölçümler: istek, veritabanı komutu, kuyruk derinliği, ekran etkileşimi, hata oranı.

## Out of scope

- Bütçe ve alarm kuralları.

## Dependencies

- V1-RMD-533
- V1-RMD-542

## Acceptance evidence

1. Bilinen dağılımlı 10.000 örnekle p95 hatası en çok %5.
2. Yazım hacmi: dakikada en çok 600 özet satırı.
3. Çıktılar `evidence/V1-RMD-543/` altındadır.

## Handoff

- None
