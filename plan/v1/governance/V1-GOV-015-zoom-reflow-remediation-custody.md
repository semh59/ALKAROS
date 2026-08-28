# V1-GOV-015 - Zoom reflow remediation custody

- Task ID: V1-GOV-015
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-010 gerçek Chrome %200/%400 denetiminde doğrulanan header clipping ve fixed shell overlap bulgularını,
çakışmasız exact source ownership ve zorunlu browser yeniden kabulü bulunan tek bir remediation görevine dönüştürmek.

## Owned surface

- `plan/v1/governance/V1-GOV-015-zoom-reflow-remediation-custody.md`
- `plan/v1/remediation/V1-RMD-010-pos-terminal-ui-responsive-and-a11y.md`
- `plan/v1/remediation/V1-RMD-016-production-shell-and-design-system.md`
- `plan/v1/remediation/V1-RMD-024-production-shell-zoom-reflow.md`
- `plan/v1/README.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-GOV-015/**`

## Dependencies

- V1-GOV-014

## Acceptance evidence

- V1-RMD-016'nın `shell.css` ve `layout-contract.test.ts` sahipliği exact olarak V1-RMD-024'e aktarılır; başka
  design-system veya component yüzeyi devredilmez.
- V1-RMD-024 yalnız iki source/test dosyası ve kendi evidence dizisini sahiplenir; V1-RMD-023'e bağımlı olur ve
  V1-RMD-010 gerçek Chrome yeniden kabulüne devreder.
- V1-RMD-010 dependency listesi V1-RMD-024'ü içerir ve kaynak düzeltmesi kanıtlanana kadar `Blocked` kalır.
- Plan validation, manifest verification, root markdownlint ve `git diff --check` exit code `0` verir.

## Handoff

- V1-RMD-024
