# V12-FSC-004 - Implement selected T300 adisyon adapter

- Task ID: V12-FSC-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.5.4
- EXT:GIB-HUGIN-T300
- CORR:C25

## Goal

Yalnız `V0-CMP-001` T300 adisyon lifecycle'ını seçtiğinde, doğrulanmış V0-HUG-001 contract'ındaki
open/update/close command mapping'ini uygulamak.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/HuginT300/**`, `tests/Modules/Fiscal/AdisyonStrategy/HuginT300/**`
- Bu görev, Hugin payment transport veya ortak composition surface'ini değiştiremez.

## In scope

- Verified command mapping, document reference correlation, retry/idempotency, sanitized evidence ve typed provider
  failure.

## Out of scope

- Applicability kararı, QNB/e-Adisyon adapter, Hugin payment request ve final Bill closure.

## Dependencies

- GATE-V12-FSC-STRATEGY
- V12-FSC-001

## Deliverables

- T300 adisyon adapter production code'u ve gerçek contract/device transcript'e bağlı automated contract tests.

## Acceptance evidence

- T300 branch seçildiyse open/update/close reference zinciri doğrulanmış contract ve gerçek cihaz/sandbox transkriptiyle
  geçer; retry ikinci fiscal document oluşturmaz.
- T300 seçilmediyse görev `V0-CMP-001` tarihli/onaylı kararıyla `NotApplicable` olur; adapter/stub oluşturulmaz.

## Handoff

- V12-FSC-003
- V20-INT-001
