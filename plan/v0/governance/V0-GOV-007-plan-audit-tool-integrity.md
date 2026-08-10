# V0-GOV-007 - Correct plan-audit tool integrity

- Task ID: V0-GOV-007
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Plan denetim aracindaki kanitlanan kapsam, coverage ve manifest hatalarini
duzeltmek; kullanilmayan yardimci fonksiyonlari kaldirmak.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `evidence/V0-GOV-007/**`

## In scope

- Aktif Markdown envanteri, PDF heading sayimi ve audit report kapanis metni.
- Kullanilmayan fonksiyonlari silmek ve mevcut komutlarla sonucu dogrulamak.

## Out of scope

- Task davranisi, product code, kaynak PDF veya gate sonucunu degistirmek.

## Dependencies

- V0-GOV-005

## Deliverables

- Tekrar calistirilabilir dogru audit araci ve denetim kaniti.

## Acceptance evidence

- `py tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- Manifest ve audit raporu aktif Markdown envanterini kapsar.
- Vulture tarafindan isaretlenen iki kullanilmayan fonksiyon kalmaz.

## Handoff

- GATE-V0-EXIT
