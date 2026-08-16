# V1-SEC-006 - Independently verify sanitized handler errors

- Task ID: V1-SEC-006
- Status: Done
- Assignee: Antigravity-v1-sec-006
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

handler exception'ındaki sentinel secret/PII'nin PostgreSQL row, log ve transcript'e ham taşınmadığını; persisted
error'ın bounded ve allowlisted olduğunu bağımsız doğrulamak.

## Owned surface

- `evidence/V1-SEC-006/**`

## In scope

- `CODE-018` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile
  kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V1-FND-019
- V1-SEC-005

## Deliverables

- `evidence/V1-SEC-006/**` altında raw reproduction transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız
  `evidence/V1-SEC-006/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
