# V1-IAM-016 - Authorization model decision

- Task ID: V1-IAM-016
- Status: Planned
- Assignee: Unassigned
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:II.2.1
- PDF:III.3
- PO:2026-09-04

## Goal

`pos.cashier.mutate` tek kaba iznini bir yetkilendirme modeliyle degistiren,
rakip POS'larin senkron "yonetici PIN" desenini asan karari kayit altina almak:
granuler izin sozlugu, gercek `waiter` rolu, politika motoru, asenkron baglamsal
yetki akisi, sureli devir, sinirli cevrimdisi yetki butcesi ve davranissal
sikilastirma.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-016-authorization-model-decision.md`
- `docs/domain/authorization-model.md`
- `evidence/V1-IAM-016/**`
- `docs/domain/authorization-model.md` yeni dosyadir; V1-IAM-016 sahibidir.
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## Deliverables

- `docs/domain/authorization-model.md`: kaynaklar, erisim tarihi, onaylayan,
  secilen sonuc, reddedilen alternatif (Toast/Square/Lightspeed senkron PIN
  deseni), etkilenen kesin task kimlikleri (V1-IAM-017..024, V1-RMD-100,
  V1-RMD-097). Izin sozlugu, rol matrisi ve invariant listesi ektir.

## Acceptance evidence

- `docs/domain/authorization-model.md` karar-kaydi formatina uyar ve
  `markdownlint-cli2` (kok config globlari) temizdir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest`
  exit 0.
- Semih dokumani okur ve `Approver` satirini onaylar; rol matrisindeki uc urun
  karari (garson oz-void, indirim merdiveni yok, supervisor = sef garson) 3.
  bolumdeki "Resolved" notuyla ortusur.

## Dependencies

- V1-IAM-002
- V1-RMD-097

## Handoff

- V1-IAM-017
