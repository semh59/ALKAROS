# V0-SEC-001 - Define security verification baseline

- Task ID: V0-SEC-001
- Status: Done
- Assignee: codex-v0-sec-001
- Work type: decision
- Surface state: Existing

## Source basis

- EXT:OWASP-ASVS-5.0.0
- EXT:OWASP-AUTH
- EXT:OWASP-SESSION
- EXT:OWASP-LOGGING
- CORR:C18

## Goal

ALKAROS için ASVS hedef seviyesini ve authentication, session, logging, SAST, SCA ve secret test kapsamını belirlemek.

## Owned surface

- `docs/security/security-verification-baseline.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Versioned ASVS requirement IDs, applicable/N/A gerekçesi, automated/manual test ayrımı ve severity gate.

## Out of scope

- Security control implementation, risk acceptance ve bağımsız assessment execution.

## Dependencies

- V0-ARC-004
- V0-ARC-005

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen security requirement matrix ve hedef seviye.

## Acceptance evidence

- Her seçilen security control tek requirement ID, test yöntemi ve downstream owner taşır.

## Handoff

- V15-SEC-001
- V15-SEC-002
- V15-SEC-003
- V20-SEC-001
