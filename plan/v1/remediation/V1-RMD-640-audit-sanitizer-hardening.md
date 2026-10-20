# V1-RMD-640 - Denetim temizleyicisinin (sanitizer) sağlamlaştırılması

- Task ID: V1-RMD-640
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Sanitizer yinelenen anahtarlı JSON, kök düz metin, Bearer ve sözcük sınırı durumlarında doğru çalışır; anahtar listesi çoğul ve eş anlamlıları (passwords, tokens, pwd, otp, iban, authorization, PINCode, JWTToken vb.) kapsar; derlenmiş Regex nesneleri bir kez oluşur; AuditEvent UTC dışı zamanı UTC yapar ve alan uzunluklarını doğrular. Kaynak bulgular: F02, F03, F04, F05, F06, F07, F08, F19 (V1-RMD-595); önem: Orta.

## Owned surface

- `plan/v1/remediation/V1-RMD-640-audit-sanitizer-hardening.md`
- `evidence/V1-RMD-640/**`
- Kod yolları başlangıç commit'inde Sınırlı ek olarak eklenir; bu görev başka görevin yüzeyini değiştiremez.

## In scope

- Audit modülündeki sanitizer, AuditEvent ve store; her kural için test; mutasyon kanıtı.

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-640/` altındadır.

## Handoff

- None
