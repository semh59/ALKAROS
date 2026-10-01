# V1-RMD-479 - Kasa sayımı isteği tekrarlanınca ikinci satır yazılmaması

- Task ID: V1-RMD-479
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`POST .../cash-sessions/{id}/counts` her çağrıda yeni bir `cash_counts` satırı yazıyor; ağ zaman aşımından sonra aynı isteğin tekrarı ikinci, aynı sayımı ekliyor.
Kasadaki tutarı etkilemez (beklenen nakit defterden gelir), yalnız denetim izini çoğaltır. Anahtar ve migration eklenmez: aynı oturum, aynı sayan, aynı tutar ve aynı notla
son 30 saniye içinde yazılmış bir sayım varsa yeni satır yazılmaz ve o satırın kimliği döner. Farklı tutarlı ya da 30 saniyeden sonraki yeniden sayım yine yeni satırdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-479-cash-count-retry-duplicate.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/SessionLifecycle/PostgresCashSessionRepository.cs - yalnız RecordCountAsync
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Cash/SessionLifecycle/CashSessionLifecycleServiceTests.cs - yalnız yeni davranışın testleri

## In scope

- Tek SQL ile "son 30 saniyedeki aynı sayım" kontrolü, testler: tekrar tek satır, farklı tutar iki satır, 30 saniye sonrası iki satır.

## Out of scope

- Sayım için idempotency anahtarı; sayımın kasa kapanışına etkisi.

## Dependencies

- None

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-479/` altındadır.

## Handoff

- None
