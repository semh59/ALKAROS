# V1-RMD-642 - Denetim modülü test gücünün artırılması

- Task ID: V1-RMD-642
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Doğrulayıcının onayladığı test boşlukları kapanır: toplu ve metadata sanitize, alanların tam gidiş-dönüş okuması, sıralama, 5000 sınırı, tip filtresi, Validate çağrısı, atomik toplu yazma, anahtar ve desen listeleri, imha sınır doğrulaması, saat dilimi bağımsızlığı; store testleri bölümlü (173) şemada da koşar; okuma sorguları ikincil sıralama kazanır. Kaynak bulgular: F15, F16, F21 (V1-RMD-595); önem: Orta.

## Owned surface

- `plan/v1/remediation/V1-RMD-642-audit-test-strengthening.md`
- `evidence/V1-RMD-642/**`
- Kod yolları başlangıç commit'inde Sınırlı ek olarak eklenir; bu görev başka görevin yüzeyini değiştiremez.

## In scope

- tests/Modules/Audit ve tests/Host/MigrationComposition/Program/AuditDisposalTests.cs; gerekirse küçük üretim düzeltmeleri; mutasyon kanıtı (hayatta kalan mutant sayısı sıfıra yaklaşır).

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-642/` altındadır.

## Handoff

- None
