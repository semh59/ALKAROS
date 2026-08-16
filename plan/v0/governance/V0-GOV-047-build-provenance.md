# V0-GOV-047 - Embed and verify build provenance

- Task ID: V0-GOV-047
- Status: Done
- Assignee: Antigravity-v0-gov-047
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

`V1-FND-001` tarafından Release artifact'larına gömülen candidate repository commit SHA'sını CI'da source candidate ile
exact karşılaştırmak.

## Owned surface

- `tools/build-provenance/verify_build_provenance.py`
- `tests/Architecture/BuildProvenance/test_build_provenance.py`
- `evidence/V0-GOV-047/**`

## In scope

- Supported MSBuild `RepositoryCommit`/assembly metadata sonucunu read-only doğrulamak.
- Temiz candidate Release build artifact'larında embedded SHA'yı okumak.
- Missing, stale ve dirty/unknown provenance fixtures'larını fail-closed reddetmek.

## Out of scope

- Version number, package publishing veya signing policy değiştirmek.
- Environment'tan doğrulanmamış SHA fallback'i kullanmak veya artifact'i build sonrası patchlemek.

## Dependencies

- V0-GOV-035
- V0-GOV-041
- V1-FND-001

## Deliverables

- Provenance verifier, automated negative tests ve raw artifact readback transcript'i.

## Acceptance evidence

- Her production Release assembly'sindeki repository commit candidate SHA ile exact eşleşir.
- Eksik, eski veya geçersiz provenance verifier'da non-zero verir.
- Locked Release build ve provenance testleri exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-047/**`
  altındadır.

## Handoff

- V0-GOV-045
