# V1-GOV-016 - Zoom reflow scope recovery

- Task ID: V1-GOV-016
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-024 build output write-set ihlalini tarihsel olarak bloklu tutmak; iki exact shell source/test yolunu temiz
evidence-output sözleşmesi bulunan yeni bir remediation görevine devretmek.

## Owned surface

- `plan/v1/governance/V1-GOV-016-zoom-reflow-scope-recovery.md`
- `plan/v1/remediation/V1-RMD-010-pos-terminal-ui-responsive-and-a11y.md`
- `plan/v1/remediation/V1-RMD-024-production-shell-zoom-reflow.md`
- `plan/v1/remediation/V1-RMD-025-production-shell-zoom-reflow-recovery.md`
- `plan/v1/README.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-GOV-016/**`

## Dependencies

- V1-GOV-015

## Acceptance evidence

- V1-RMD-024 `Blocked` kalır, production source ownership'i bırakır ve write-set ihlalini kanıtıyla korur.
- V1-RMD-025 yalnız `shell.css`, `layout-contract.test.ts` ve kendi evidence dizisini sahiplenir; build output'unu
  yalnız kendi evidence dizinine yazan exact Vite komutunu acceptance koşulu yapar.
- V1-RMD-010 dependency listesi V1-RMD-024 yerine V1-RMD-025'i içerir.
- Plan validation, manifest verification, root markdownlint ve `git diff --check` exit code `0` verir.

## Handoff

- V1-RMD-025
