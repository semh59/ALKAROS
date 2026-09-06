# V11-GOV-001 - Migration collision fix record

- Task ID: V11-GOV-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-06

## Goal

`V11-RMD-001` ile kapatılan migration ID çakışması ve canlı kompozisyon
kablolaması düzeltmesini resmen kaydeder. `GATE-V11-EXIT` daha önce hiç
mühürlenmemişti (V1.1'in kendi görev matrisi 24/24 `Done` iddia etse de
formaliteyi hiç geçmemişti) — bu görev onu ilk kez, "1. aşama tamamlandı,
kalan mimari-kayıt (5 modül) ve `master`'a birleştirme ayrı görevlerde"
notuyla kaydeder.

## Owned surface

- `plan/v1.1/governance/V11-GOV-001-migration-collision-fix-record.md`
- `plan/v1.1/README.md`
- `plan/GATES.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `plan/v1.1/README.md` görev sayacını `V11-RMD-001` ile güncellemek.
- `plan/GATES.md`'deki `GATE-V11-EXIT` satırına bu düzeltmenin notunu
  eklemek (kapıyı henüz mühürlemeden — 5 modül kaydı ve cross-schema
  iddiaları hâlâ açık, `master`'a birleştirme henüz yapılmadı).

## Out of scope

- Kod değişikliği — `V11-RMD-001`'in kapsamındadır (zaten tamamlanmış).
- `GATE-V11-EXIT`'in kesin mühürlenmesi — 5 modül kaydı kararı ve
  `master`'a birleştirme bekleniyor.

## Dependencies

- V11-RMD-001

## Deliverables

- Güncel `plan/v1.1/README.md` ve `plan/GATES.md` notu.

## Acceptance evidence

- `V11-RMD-001` `Done`; migration çakışması ve canlı kompozisyon
  kablolaması düzeltmesi (task dosyasının kendi Acceptance evidence
  bölümüne bakın).
- `plan/v1.1/README.md` 25 görev dosyasını (24 özellik + 1 remediation)
  yansıtıyor.

## Handoff

- GATE-V11-EXIT
