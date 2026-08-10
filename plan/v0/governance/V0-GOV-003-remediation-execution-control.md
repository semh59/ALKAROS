# V0-GOV-003 - Control evidence-based remediation execution

- Task ID: V0-GOV-003
- Status: Done
- Assignee: /root/v0_gov003
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31
- CORR:C32

## Goal

Planin V0 cikis kapisi acikken, yalniz kanitlanmis kritik bulgulari duzelten
sinirli remediation task'larinin fail-closed denetimle calismasini saglamak.

## Owned surface

- `evidence/V0-GOV-003/**`

2026-08-03 tarihli kullanıcı talimatıyla remediation yürütme kontrolünün aktif yüzeyleri V0-GOV-028'e devredildi.

## In scope

- GATES.md icindeki exact remediation exception kayitlarini task-scope aracinda
  parse etmek ve sadece kayitli task kimlikleri icin entry-gate kontrolunu
  atlamak.
- Exception yoksa veya kayit bozuksa entry gate'i fail-closed reddetmek.
- Exception'in V1 entry/exit kaniti olmadigini ve yeni product behavior icin
  kullanilamayacagini belgelemek.

## Out of scope

- V0 task durumlarini yapay olarak kapatmak, genel bir gate bypass eklemek,
  application behavior veya migration yazmak.

## Dependencies

- V0-GOV-001
- V0-GOV-002

## Deliverables

- Exact task kimlikli remediation exception parser'i, ret/izin testleri ve
  yeniden uretilebilir kanit kaydi.

## Acceptance evidence

- Kayitli bir remediation task kapali V0 entry gate'inde yalniz kendi
  allowlist'i ile preflight'i gecer.
- Kayitsiz V1 gorevi ayni kosulda non-zero exit verir ve reddedilir.
- Gecersiz veya yinelenen exception kaydi non-zero exit verir.

## Handoff

- V0-GOV-028
- V1-FND-011
- V1-FND-012
- V1-IAM-004
- V1-SEC-003
