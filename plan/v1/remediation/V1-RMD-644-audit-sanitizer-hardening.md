# V1-RMD-644 - Denetim temizleyicisinin (sanitizer) sağlamlaştırılması

- Task ID: V1-RMD-644
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Sanitizer yinelenen anahtarlı JSON, kök düz metin, Bearer ve sözcük sınırı durumlarında doğru çalışır; anahtar listesi çoğul ve eş anlamlıları (passwords, tokens, pwd, otp, iban, authorization, PINCode, JWTToken vb.) kapsar; Regex nesneleri bir kez oluşur; AuditEvent UTC dışı zamanı UTC yapar ve alan uzunluklarını doğrular. Kaynak: V1-RMD-595 bulguları F02, F03, F04, F05, F06, F07, F08, F19 (denetim görevinin kanıtında V1-RMD-640 olarak anılır).

## Owned surface

- `plan/v1/remediation/V1-RMD-644-audit-sanitizer-hardening.md`
- `evidence/V1-RMD-644/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/IAuditSanitizer.cs - yalnız sanitizer sağlamlaştırması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/AuditEvent.cs - yalnız UTC normalizasyonu ve uzunluk doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditSanitizerTests.cs - yalnız yeni sanitizer testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditTests.cs - yalnız AuditEvent testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-639-audit-write-atomic-with-business-transaction.md - yalnız silme (kapsamlı görevle değiştirilir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-640-audit-sanitizer-hardening.md - yalnız silme (bu görevle değiştirildi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-641-audit-append-only-and-disposal-hardening.md - yalnız silme (kapsamlı görevle değiştirilir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-642-audit-test-strengthening.md - yalnız silme (kapsamlı görevle değiştirilir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-643-audit-docs-and-small-drifts.md - yalnız silme (kapsamlı görevle değiştirilir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-595-audit-module-quality-audit.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Audit modülündeki sanitizer ve AuditEvent; her kural için test; mutasyon kanıtı.

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-644/` altındadır.

## Handoff

- None
