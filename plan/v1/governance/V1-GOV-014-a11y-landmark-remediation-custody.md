# V1-GOV-014 - Accessibility landmark remediation custody

- Task ID: V1-GOV-014
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-010 canlı production DOM axe taramasında doğrulanan prohibited ARIA ve nested landmark bulgularını,
non-overlapping exact source ownership ve yeniden kabul zinciri bulunan tek bir remediation görevine dönüştürmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-016-production-shell-and-design-system.md`
- `plan/v1/remediation/V1-RMD-022-table-card-keyboard-semantics.md`
- `plan/v1/remediation/V1-RMD-023-production-landmark-and-aria-semantics.md`
- `plan/v1/README.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-GOV-014/**`

## Dependencies

- V1-GOV-012

## Acceptance evidence

- V1-RMD-016 shell wildcard'ı, V1-RMD-023'e devredilmeyen mevcut shell dosyalarına daraltılır.
- V1-RMD-022 yalnız `tables.css` ve kendi evidence dizisini korur; TableWorkspace component/test ownership'i
  V1-RMD-023'e exact olarak aktarılır.
- V1-RMD-023 yalnız ProductionShell/TableWorkspace component ve test dosyalarını sahiplenir; source finding'leri
  kapandıktan sonra V1-RMD-010 yeniden kabulüne devredilir.
- Plan validation, manifest verification, root markdownlint ve `git diff --check` exit code `0` verir.

## Handoff

- V1-RMD-023
