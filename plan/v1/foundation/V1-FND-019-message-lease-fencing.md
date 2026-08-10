# V1-FND-019 - Independently verify message lease fencing

- Task ID: V1-FND-019
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

inbox/outbox stale lease worker'ının acknowledgement, retry veya terminal success yazamadığını generation/fencing interleaving'iyle bağımsız doğrulamak.

## Owned surface

- `evidence/V1-FND-019/**`

## In scope

- `CODE-004;CODE-005` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V1-FND-002
- V1-FND-012
- V1-FND-014
- V1-FND-015

## Deliverables

- `evidence/V1-FND-019/**` altında raw reproduction transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız `evidence/V1-FND-019/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
