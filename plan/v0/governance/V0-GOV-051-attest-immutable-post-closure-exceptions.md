# V0-GOV-051 - Attest immutable post-closure exceptions

- Task ID: V0-GOV-051
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53

## Goal

Kapanmış görevlerde bulunan trailer, pre-Done evidence ve generated-CRLF
kusurlarını geçmişi/evidence'ı değiştirmeden immutable exception olarak tam ve
yeniden doğrulanabilir biçimde atteste etmek.

## Owned surface

- `docs/engineering/immutable-closure-exceptions.md`
- `evidence/V0-GOV-051/**`

## In scope

- `V0-GOV-035`, `V0-GOV-037`, `V0-GOV-038`, `V0-GOV-039` ve `V1-IAM-005`
  için observed commit, verdict, source location, command/exit ve immutable
  reason satırlarını üretmek.
- `git show --check e5d7011` exit `2` ve CRLF diagnostic'lerini lossless
  compressed raw artifact olarak hash'lemek; aynı CRLF output'u yeniden Git
  text artifactı haline getirmemek.
- Historical exception ile current remediation success verdict'lerini ayrı
  tutmak.

## Out of scope

- Historical task status/assignee/evidence/commit değişikliği, amend, rebase
  veya force-push.
- Secret değerini raw artifact veya dokümana yazmak.

## Dependencies

- V0-GOV-049
- V0-GOV-035
- V0-GOV-037
- V0-GOV-038
- V1-IAM-005

## Deliverables

- Immutable exception register, hash'li compressed raw diagnostics ve current
  remediation ayrımı yapan engineering karar kaydı.

## Acceptance evidence

- Her exception satırı exact historical commit/source/command/exit ile bağlıdır.
- `e5d7011` CRLF failure raw diagnostic hash'i ve decompression readback'i
  doğrulanır; non-CR whitespace kontrolü ayrı kaydedilir.
- Exception register geçmişi düzeltilmiş veya güncel başarı gibi sunmaz.
- Plan validation, pre-Done task-scope ve diff check exit code `0` verir;
  kanıtlar `evidence/V0-GOV-051/**` altındadır.

## Handoff

- V0-GOV-045
- V0-GOV-048
