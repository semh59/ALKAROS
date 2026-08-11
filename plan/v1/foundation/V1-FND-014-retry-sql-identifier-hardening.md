# V1-FND-014 - Harden retry SQL identifier surface

- Task ID: V1-FND-014
- Status: Done
- Assignee: opencode-v1-fnd-014
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.11-I.15
- CORR:C42

## Goal

`RetryPolicy.RecordFailureAsync` içindeki serbest `tableName`
interpolasyonunu kaldırarak SQL yüzeyini yalnız kayıtlı sabit tablo
kimliklerine kapatmak.

## Owned surface

- `tests/BuildingBlocks/Idempotency/RetrySqlIdentifierTests.cs`
- `evidence/V1-FND-014/**`
- C52 fenced message-finalization surface is transferred to V1-FND-019; this historical task remains closed.

## In scope

- `tableName` parametresi için kayıtlı sabit tablo kimliği doğrulaması
  (`inbox_messages`, `outbox_messages`); kayıtlı olmayan değer fail-closed
  reddedilir.
- Mevcut çağrıcıların (InboxStore/OutboxStore) davranışı değişmez; identifier
  reddi ve izin testleri eklenir.

## Out of scope

- Retry zamanlama/backoff politikası, dead-letter eşiği, Inbox/Outbox store
  davranışı, schema değişikliği.

## Dependencies

- V0-ARC-003

## Deliverables

- Identifier allowlist'li RetryPolicy ve SQL yüzeyi testleri.
- Komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- Kayıtlı olmayan tablo kimliği fail-closed reddedilir; kayıtlı kimlikler
  mevcut davranışı korur; ilgili testler exit code `0` verir.

## Handoff

- V1-FND-002
