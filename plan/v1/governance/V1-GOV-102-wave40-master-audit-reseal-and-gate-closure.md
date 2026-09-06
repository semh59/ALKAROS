# V1-GOV-102 - Wave 40 master audit reseal and gate closure

- Task ID: V1-GOV-102
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: validation
- Surface state: Existing

## Source basis

- PO:2026-09-06

## Goal

`V1-RMD-112` tamamlandıktan sonra ilgili test süitlerinin, migration
up/down kanıtının ve plan bütünlüğünün doğrulanması ve `GATE-V1-EXIT`
kapısının 40. dalga için kesin olarak yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-102-wave40-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `docker compose -f compose.yaml -f compose.test.yaml run --rm test`:
  79/79 test projesi, sıfır başarısız.
- `dotnet build -c Debug` sıfır uyarı / sıfır hata.
- Revert-and-confirm ile discount idempotency düzeltmesinin gerçekten
  gerekli olduğu doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage` sıfır hata.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (40. dalga reseal notu) ve
  `plan/v1/README.md` üzerinde kesin olarak mühürlemek.

## Out of scope

- Kod değişikliği — `V1-RMD-112`'nin kapsamındadır (zaten tamamlanmış).
- Bağımsız denetim raporunun kalan bulguları — sıradaki dalgalarda.

## Dependencies

- V1-RMD-112

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı.

## Acceptance evidence

- `V1-RMD-112` `Done`; inbox indeksi, denial-events RESTRICT ve discount
  idempotency düzeltmesi (task dosyasının kendi Acceptance evidence
  bölümüne bakın).
- `python tools/plan-audit/plan_audit_tool.py validate`,
  `validate-coverage`: sıfır hata.
- `GATE-V1-EXIT` `plan/GATES.md` üzerinde (özet hücre + 40. dalga kesin
  reseal tarihli bölümü) ve `plan/v1/README.md` üzerinde kesin olarak
  mühürlendi.

## Handoff

- GATE-V1-EXIT
