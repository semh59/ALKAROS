# V0-GOV-016 - Refresh post-remediation audit integrity records

- Task ID: V0-GOV-016
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Kapatılmış remediation görevlerinden sonra audit report'un tarihsel ifadelerini
açıklaştırmak ve aktif Markdown envanterinin hash kayıtlarını yeniden üretmek.

## Owned surface

- `plan/v0/README.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `tools/plan-audit/plan_audit_tool.py`
- `evidence/V0-GOV-016/**`

## In scope

- Audit generator'indeki tarihsel durum anlatımı, report/manifest yenilemesi
  ve mekanik plan denetim kanıtı.

## Out of scope

- Product davranışı, kaynak PDF, task metadata'si dışındaki plan kuralları,
  gate durumu veya production deployment.

## Dependencies

- V0-GOV-012
- V0-GOV-013
- V0-GOV-014
- V0-GOV-015

## Deliverables

- Tarihsel audit bağlamını açıklayan generator değişikliği, güncel report ve
  manifest ile yeniden üretilebilir validation kanıtı.

## Acceptance evidence

- `validate`, `validate-coverage` ve `verify-manifest` exit code `0` verir.
- Audit report, kaynak PDF'yi başlangıç denetim baselini olarak tanımlar;
  mevcut Git veya uygulama durumunu tarihsel veri gibi sunmaz.

## Handoff

- GATE-V0-EXIT
