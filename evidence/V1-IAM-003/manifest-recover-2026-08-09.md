# verify-manifest düzeltme kaydı (2026-08-09)

Kullanıcı talimatı: "tümünü düzelt" — plan-audit `verify-manifest` sapmalarının
tamamının düzeltilmesi onaylandı.

## Durum (düzeltme öncesi)

`verify-manifest` 44 hata veriyordu:

- `plan/AUDIT_MANIFEST.json` (araç üretimi, nominal sahip V1-FND-008 /
  V0-GOV-030) eski snapshot içeriyordu: 6 path kayıtsızdı (V0-BKP proof,
  TRACEABILITY, CAT-001, IAM-001, IAM-002, IAM-003, IAM-004, SEC-003 vb.
  güncel hash/line/bytes değerleri yoktu).
- `plan/AUDIT_REPORT.md` (araç üretimi) baseline satır tablosu ve added-rows
  listesi güncel olmayan final hash'ler içeriyordu (AUDIT_FINAL_HASH/LINES,
  AUDIT_ADDED_HASH, AUDIT_ADDED_ROWS).
- PDF_HASH ve BASELINE_HASH uyumluydu; manifest PDF/baseline blokları
  değişmemişti.

## Uygulanan düzeltme (yalnız araç yeniden üretimi)

```
> python tools/plan-audit/plan_audit_tool.py generate-audit-report
Baseline audit records: 211 | Added Markdown records including report: 209
Audit findings recorded: 1827 | Audit report lines: 1696

> python tools/plan-audit/plan_audit_tool.py generate-manifest
Manifest Markdown files: 420
Manifest Markdown lines: 28486
Manifest Markdown bytes: 1913437
Manifest SHA-256: F337F2E14A6282347D047BF6F7A0C93A3E2F992618308A227B48DE6792BEAFDC
```

## Sonuç

```
> python3 tools/plan-audit/plan_audit_tool.py verify-manifest
Manifest errors: 0          (exit 0)

> python3 tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0 | Validation warnings: 0

> python3 tools/plan-audit/plan_audit_tool.py validate-coverage
Coverage errors: 0
```

Değişen dosyal: `plan/AUDIT_MANIFEST.json`, `plan/AUDIT_REPORT.md` — her ikisi
araç yeniden üretimi; kullanıcı onayı bu kayıtta belgelenmiştir. Başka dosya
değişmedi; PDF ve baseline hash'leri stabil kaldı.