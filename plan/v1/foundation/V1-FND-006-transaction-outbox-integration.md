# V1-FND-006 - Integrate transaction and Outbox execution

- Task ID: V1-FND-006
- Status: Done
- Assignee: opencode-v1-fnd-006
- Work type: integration
- Surface state: Existing

## Source basis

- PDF:I.7-I.15
- PDF:I.49
- PDF:II.4
- CORR:C28

## Goal

Transaction commit ile durable Outbox enqueue/dispatch sınırını tek crash-safe integration contract'ında birleştirmek.

## Owned surface

- Kapsam genişletme onayı (2026-08-01 kullanıcı onayı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve
  `build/project-manifest.json` içine kaydı (Npgsql zaten `Directory.Packages.props` merkezi paketindedir).
- Bu görev, transaction veya Inbox/Outbox owner surface'lerini değiştiremez.

## In scope

- Transactional enqueue, commit-before-dispatch, rollback suppression, post-commit wake-up, restart recovery,
  duplicate-dispatch idempotency ve typed failure propagation.

## Out of scope

- Transaction primitive, Outbox schema/dispatcher, domain event mapping ve provider transport.

## Dependencies

- V1-FND-005
- V1-FND-002

## Deliverables

- Transaction/Outbox integration production code'u ve her commit/crash penceresi için failure-injection tests.

## Acceptance evidence

- Rollback Outbox kaydı veya dispatch üretmez; commit edilen kayıt process crash sonrasında kaybolmadan dispatch edilir.
- Dispatcher aynı kaydı tekrar teslim etse domain consumer idempotency contract'ı ikinci business effect üretmez;
  post-commit iş commit öncesi görünmez.

## Handoff

- V1-ORD-002
- V1-KIT-003
- V14-ONL-001
- V14-QRT-001
- V12-PAY-004
- V12-MCD-004
- V13-ACC-006
