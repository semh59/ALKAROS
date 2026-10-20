# V1-RMD-643 - Denetim modülü doküman sapmaları ve küçük düzeltmeler

- Task ID: V1-RMD-643
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Runbook ve mimari belgesindeki çelişkiler, eski yorumlar, görev yorumlarındaki PDF ve görev numaraları, V1-OPS-001 yüzey durumu, mimari test onaylı kenar listesi, kilit dosyası uyumsuzluğu ve gereksiz proje referansı düzeltilir; üçüncü taraf lisans politikası belgesi yazılır. Kaynak bulgular: F23, F25, F26 (V1-RMD-595); önem: Düşük.

## Owned surface

- `plan/v1/remediation/V1-RMD-643-audit-docs-and-small-drifts.md`
- `evidence/V1-RMD-643/**`
- Kod yolları başlangıç commit'inde Sınırlı ek olarak eklenir; bu görev başka görevin yüzeyini değiştiremez.

## In scope

- docs/compliance, docs/architecture, Audit kaynak yorumları, ilgili plan dosyası, mimari testler, kilit dosyaları.

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-643/` altındadır.

## Handoff

- None
