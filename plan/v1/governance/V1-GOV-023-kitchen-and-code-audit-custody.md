# V1-GOV-023 - Kitchen and code audit custody correction

- Task ID: V1-GOV-023
- Status: Done
- Assignee: 2814a35e-cc66-4d6c-882c-1b1809271ab4
- Work type: documentation
- Surface state: Existing

## Goal

`V1-RMD-034` ve `V1-RMD-035` sonrası oluşan sahipsiz kitchen operations UI dosyalarını ve derin kod denetimi yüzey çakışmalarını historical görevlerden exact custody transferi ile uzlaştırmak; plan doğrulamasındaki performans darboğazını çözerek V1 matrisini tam tutarlı ve doğrulanabilir hale getirmek.

## Owned surface

- `plan/v1/governance/V1-GOV-023-kitchen-and-code-audit-custody.md`
- `plan/v1/foundation/V1-FND-018-atomic-idempotency-execution.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/v1/remediation/V1-RMD-002-deep-audit-remediation.md`
- `plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md`
- `plan/v1/table-management/V1-TBL-001-table-lifecycle.md`
- `plan/v1/table-management/V1-TBL-007-fail-closed-table-audit.md`
- `plan/v1/operations/V1-OPS-001-audit-foundation.md`
- `plan/v1/remediation/V1-RMD-034-authoritative-kitchen-station-contract.md`
- `plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md`
- `tools/plan-audit/plan_audit_tool.py`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `.gitignore`
- `evidence/V1-GOV-023/**`

## In scope

- `src/Clients/PosTerminal/src/features/kitchen-operations/**` yüzeyinin `V1-RMD-034`'e bağlanması.
- `V1-RMD-035` tarafından değiştirilen 13 dosyanın historical owner görevlerden devrinin exact transfer notuyla yapılması.
- `plan_audit_tool.py` içindeki DAG döngü optimizasyonunun (memoization) uygulanması.
- `GATES.md` ve `plan/v1/README.md` içindeki V1 görev sayımlarının güncellenmesi.
- `.gitignore` dosyasına geçici test ve çıktı loglarının eklenmesi.

## Out of scope

- Production C# veya TypeScript kaynak kodunda davranış değişikliği yapmak.
- Kapatılmış görevlerin acceptance iddialarını veya kanıtlarını değiştirmek.

## Dependencies

- V1-RMD-034
- V1-RMD-035

## Deliverables

- Çakışmasız ve sahipsiz dosya içermeyen güncel plan dosyaları.
- Sıfır hata ile anında tamamlanan plan audit doğrulama çıktısı.
- `evidence/V1-GOV-023/**` altında doğrulama ve sayım kanıtları.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` komutu sıfır hata ve sıfır uyarı ile exit code `0` verir.
- Repository'de hiçbir sahipsiz production/test dosyası (`UNOWNED_PRODUCTION_FILE`) veya yüzey çakışması (`SURFACE_DUPLICATE`, `SURFACE_PREFIX_OVERLAP`) kalmaz.
- `GATES.md` ve `plan/v1/README.md` sayımları plan dosyalarının gerçek sayısıyla (145 V1 görevi) tam uyumlu kalır.
- `git diff --check` temiz çıkar; allowlist dışı yol değişmez.

## Handoff

- V1-RMD-010
