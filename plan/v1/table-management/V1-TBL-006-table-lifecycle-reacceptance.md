# V1-TBL-006 - Reaccept the complete Table lifecycle baseline

- Task ID: V1-TBL-006
- Status: Done
- Assignee: Antigravity-v1-tbl-006
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Tables transition matrix, concurrent repository update ve migration lifecycle davranışını temiz candidate üzerinde
bağımsız doğrulamak.

## Owned surface

- `evidence/V1-TBL-006/**`

## In scope

- `CODE-013;GOV-010;GOV-011;GOV-012` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden
  üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile
  kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-039
- V1-FND-022

## Deliverables

- `evidence/V1-TBL-006/**` altında raw reproduction transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız
  `evidence/V1-TBL-006/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
