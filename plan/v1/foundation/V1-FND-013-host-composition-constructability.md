# V1-FND-013 - Prove host composition fail-closed constructability

- Task ID: V1-FND-013
- Status: Done
- Assignee: opencode-v1-fnd-013
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- CORR:C42

## Goal

Kayıtlı her DI servisinin constructor graph'ının build sırasında
doğrulanmasını ve kırık graph'ta kompozisyonun fail-closed reddedilmesini
gerçek test kanıtıyla göstermek.

## Owned surface

- `evidence/V1-FND-013/**`
- C52 Host module/data-source integration surface is transferred to V1-FND-017; this historical task remains closed.

## In scope

- `BuildServiceProvider()` üzerinde graph doğrulaması (ValidateOnBuild /
  ValidateScopes eşdeğeri) ve kayıtlı her servisin construct edilebilirliğini
  kanıtlayan test.
- Kırık constructor graph senaryosunda kompozisyonun fail-closed reddedildiği
  test.

## Out of scope

- Host executable davranışı, migration composition, DI container değişimi,
  modül kayıt sözleşmesi.

## Dependencies

- V0-ARC-001

## Deliverables

- Graph doğrulamalı HostComposition ve constructability testleri.
- Komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- Yeni testler ve tam çözüm testleri exit code `0` verir.
- Kırık graph senaryosu fail-closed reddedildiğini kanıtlar.

## Handoff

- V1-FND-004
