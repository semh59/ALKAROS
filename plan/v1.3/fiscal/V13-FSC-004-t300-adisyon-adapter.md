# V13-FSC-004 - Implement selected Token/Beko adisyon adapter

- Task ID: V13-FSC-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.5.4
- EXT:TOKEN-DEVELOPER-PORTAL
- CORR:C25
- CORR:C98

## Goal

**Retarget notu (2026-09-17, `V0-GOV-064`/CORR:C98):** hedef cihaz Hugin
T300 değil, Token/Beko (300 TR / X30 TR) — bu Beko modelleri de gerçek bir
YN ÖKC/e-Adisyon cihazıdır. Task ID değişmedi.

Yalnız `V0-CMP-001` Token/Beko adisyon lifecycle'ını seçtiğinde, doğrulanmış V0-HUG-001 contract'ındaki
open/update/close command mapping'ini uygulamak.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/TokenBeko/**`, `tests/Modules/Fiscal/AdisyonStrategy/TokenBeko/**`
- Bu görev, Token payment transport veya ortak composition surface'ini değiştiremez.

## In scope

- Verified command mapping, document reference correlation, retry/idempotency, sanitized evidence ve typed provider
  failure.

## Out of scope

- Applicability kararı, QNB/e-Adisyon adapter, Token payment request ve final Bill closure.

## Dependencies

- GATE-V13-FSC-STRATEGY
- V13-FSC-001
- V0-HUG-001

## Deliverables

- Token/Beko adisyon adapter production code'u ve gerçek contract/device transcript'e bağlı automated contract tests.

## Acceptance evidence

- Token/Beko branch seçildiyse open/update/close reference zinciri doğrulanmış contract ve gerçek cihaz/sandbox transkriptiyle
  geçer; retry ikinci fiscal document oluşturmaz.
- Token/Beko seçilmediyse görev `V0-CMP-001` tarihli/onaylı kararıyla `NotApplicable` olur; adapter/stub oluşturulmaz.

## Handoff

- V13-FSC-003
- V20-INT-001
