# V0-GOV-039 - Enforce reproducible closure evidence envelopes

- Task ID: V0-GOV-039
- Status: Done
- Assignee: /root/v0_gov_039_evidence
- Work type: implementation
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

Task closure kanıtında command, exit code, environment, candidate commit ve artifact hash alanlarını machine-readable ve
tamper-evident bir envelope ile zorunlu kılmak.

## Owned surface

- `evidence/V0-GOV-039/**`
- Post-closure envelope implementation surface was transferred to `V0-GOV-049` by `CORR:C53`; historical evidence
  remains immutable.

## In scope

- Raw/narrative evidence ayrımı ve zorunlu envelope schema'sı tanımlamak.
- Eksik exit code, stale commit, hash mismatch, secret ve narrative-only fixture'larını fail-closed reddetmek.
- `V1-IAM-005` kabul komutunu task executable olduğu tarihsel pre-close commit'te gerçek geçici worktree ile replay
  etmek.

## Out of scope

- Current `Done` task'ı executable gibi çalıştırmayı exit `0` beklemek.
- Secret değerini evidence'a yazmak, tarihsel evidence'ı değiştirmek veya ürün davranışı düzeltmek.

## Dependencies

- V0-GOV-035

## Deliverables

- Evidence envelope validator, negative fixtures, sözleşme ve tarihsel acceptance replay transcript'i.

## Acceptance evidence

- Geçerli envelope command/exit/environment/commit/hash alanlarının tamamını doğrular; her eksik/tampered fixture
  fail-closed olur.
- Tarihsel acceptance replay gerçek commit/worktree ile exit `0` verir; commit bulunamaz veya kurulamazsa task `Blocked`
  kalır ve exact neden yazılır.
- Evidence secret değer yerine yalnız redacted location ve fingerprint taşır.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-039/**`
  altındadır.

## Handoff

- V0-GOV-046
- V1-TBL-006
