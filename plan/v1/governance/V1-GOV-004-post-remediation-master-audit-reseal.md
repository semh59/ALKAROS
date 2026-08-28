# V1-GOV-004 - Post-remediation master audit and gate closure reseal

- Task ID: V1-GOV-004
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-007..021 remediasyon ve bağımsız designer acceptance zinciri tamamlandıktan sonra tüm çözümü, testleri,
migrasyonları ve yönetişim manifestini taze ortamda yeniden denetleyerek nihai mühürleme raporunu üretmek.

## Owned surface

- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_FINDINGS.json`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl`
- `evidence/V1-GOV-004/**`

## In scope

- Tüm repository'yi (1.727+ dosya) taze bağımsız denetim oturumunda yeniden doğrulamak.
- V1-RMD-007..021 zincirindeki repository bulgularının ve production experience acceptance şartlarının kapandığını
  doğrulamak.
- `AUDIT_MANIFEST.json` ve `GATES.md` kapanışlarını mühürlemek.
- Nihai üretim hazırlık durumunu fail-closed ilkelerle raporlamak.

## Out of scope

- Kod değişikliği yapmak (yalnızca doğrulama ve yönetişim mühürlemesi).
- Dış donanım/sandbox gerektiren maddeler için onaysız feragat vermek.

## Dependencies

- V1-RMD-021

## Deliverables

- Nihai mühürlenmiş master audit raporu belgesi.
- Sıfır hata veren manifest ve gate kapanış kanıtı.

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` 0 hata ile exit code 0 verir.
- Candidate HEAD/tree ve bütün tracked dosyalar remediation sonrası yeni ledger ile birebir kapsanır; önceki ledger
  veya evidence yeniden kullanılmaz ve reviewer hiçbir implementation agent'ı değildir.
- Fresh PostgreSQL 18, gerçek HTTPS Host, iki bağımsız browser storage alanı, dokuz viewport/a11y matrisi, locked build,
  full .NET/Node/Python testleri, provenance, dependency/SBOM/license/secret scan gerçek exit code 0 verir.
- Repository bulguları kapansa bile mali cihaz, provider sandbox, lisans, backup/RPO-RTO, security assessment ve imzalı
  go-live kanıtları yoksa sonuç açıkça `NOT PRODUCTION READY` kalır; waiver üretilmez.
- `evidence/V1-GOV-004/**` altında nihai kapanış kanıt paketi kaydedilir.
- Semih final reseal öncesinde gerçek production shell'de masa oluşturma, ürün oluşturma ve table-to-kitchen akışını
  tekrarlar; designer acceptance'ın varlığı donanım/provider/go-live blocker'larını kapatmaz.

## Handoff

- V20-REL-001
