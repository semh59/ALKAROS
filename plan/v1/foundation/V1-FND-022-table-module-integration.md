# V1-FND-022 - Independently verify the Tables candidate integration

- Task ID: V1-FND-022
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

dondurulmuş candidate custody hash'leriyle temiz dala alınan Tables module, project, lock ve migration setinin eksiksizliğini ve transition invariant'ını bağımsız doğrulamak.

## Owned surface

- `evidence/V1-FND-022/**`

## In scope

- `CODE-013;GOV-010` bulgu zincirini dependency tasklerinin committed candidate'ı üzerinde yeniden üretmek.
- Source, test, migration ve runtime sonucunu read-only inceleyip command, exit code, environment ve commit SHA ile kaydetmek.
- Sonucu `VERIFIED`, `UNPROVEN`, `PARTIAL` veya `CANDIDATE` olarak fail-closed sınıflandırmak.

## Out of scope

- Production, test, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- Focused test veya static inspection sonucunu bütün baseline için yeterli kanıt saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-037
- V1-FND-001
- V1-FND-012
- V1-TBL-001

## Deliverables

- `evidence/V1-FND-022/**` altında raw reproduction transcript'i, hash'ler ve terminal verdict.

## Acceptance evidence

- Her command exact candidate SHA, environment ve gerçek exit code ile kaydedilir.
- Bulguya özgü başarı ve negatif yol bağımsız olarak yeniden üretilir; belirsiz sonuç `VERIFIED` olmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız `evidence/V1-FND-022/**` ve task metadata'sıdır.

## Handoff

- V0-GOV-045
