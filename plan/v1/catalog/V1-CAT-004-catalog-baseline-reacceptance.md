# V1-CAT-004 - Reaccept the complete Catalog baseline

- Task ID: V1-CAT-004
- Status: Done
- Assignee: Antigravity-v1-cat-004
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Catalog domain, repository ve migration davranışını temiz candidate üzerinde üç ardışık test ve PostgreSQL
forward/down/forward akışıyla doğrulamak.

## Owned surface

- `evidence/V1-CAT-004/**`

## In scope

- `GOV-006;GOV-007` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile
  kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-040
- V1-RMD-001

## Deliverables

- `evidence/V1-CAT-004/**` altında raw reproduction transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız
  `evidence/V1-CAT-004/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
