# V1-RMD-641 - Denetim tablosunun değiştirilemezlik ve imha sağlamlaştırması

- Task ID: V1-RMD-641
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim tablosu tabloyu toptan boşaltan komuta karşı ifade düzeyi tetikleyiciyle korunur ve imha için ayrı yetkili rol tasarlanır; audit-disposal operatör kimliği alır ve olaya yazar, eşzamanlı koşuda güvenli davranır, default bölümde satır varsa uyarı koduyla biter, tek satır sayımı kullanır, Türkçe hata verir; kurtarma ve yedek runbook adımları (ana anahtar, kurtarma sonrası imha, WAL budama, uyarı yolu) yazılır. Kaynak bulgular: F11, F12, F13, F14, F17, F24, Y2 (V1-RMD-595); önem: Orta.

## Owned surface

- `plan/v1/remediation/V1-RMD-641-audit-append-only-and-disposal-hardening.md`
- `evidence/V1-RMD-641/**`
- Kod yolları başlangıç commit'inde Sınırlı ek olarak eklenir; bu görev başka görevin yüzeyini değiştiremez.

## In scope

- Audit modülü imha sınıfı, ilgili göç (yeni, geri alınabilir), Program.cs audit-disposal kolu, compose.ops.yaml, docs/recovery ve docs/compliance runbook dosyaları; gerçek PostgreSQL testleri.

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-641/` altındadır.

## Handoff

- None
