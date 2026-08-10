# V1-IAM-013 - Independently verify issued-token redaction

- Task ID: V1-IAM-013
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

sentinel bearer token'ın object formatting, exception, assertion ve transcript çıktılarında görünmediğini ve credential lifecycle'ın korunmasını bağımsız doğrulamak.

## Owned surface

- `evidence/V1-IAM-013/**`

## In scope

- `CODE-009;CODE-016` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V1-IAM-001
- V1-IAM-011

## Deliverables

- `evidence/V1-IAM-013/**` altında ham yeniden üretim transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız `evidence/V1-IAM-013/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
