# V1-GOV-048 - Master custody reopen and remediation wave 13

- Task ID: V1-GOV-048
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Lokantanın tek başına canlıya alınabilmesi için en kritik eksik olan PostgreSQL yedekleme ve geri yükleme mekanizmasını üretim seviyesine taşımak amacıyla `GATE-V1-EXIT` kapısını yeniden açmak. `pg_dump` tabanlı yedek betiği, sağlama toplamı zorunlu geri yükleme betiği, Compose `ops` profili, atılabilir PostgreSQL 18 üzerinde uçtan uca round-trip kanıtı ve operatör runbook'u ile 1 kurtarma görevi (`V1-RMD-086`) ve kapanış görevi (`V1-GOV-049`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-048-master-custody-reopen-and-remediation-wave-13.md`
- `plan/v1/remediation/V1-RMD-086-postgresql-backup-restore-mechanism-and-runbook.md`
- `plan/v1/governance/V1-GOV-049-wave13-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 13. dalga görevleriyle (`V1-RMD-086`, `V1-GOV-049`) genişletmek ve sayımı güncellemek.
- `EXT:POSTGRESQL-18.4` tüketici listesine `plan/OFFICIAL_SOURCE_REGISTER.md` içinde `V1-RMD-086` eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Yük/performans testi, cihaz/tarayıcı matrisi ve KVKK saklama uygulaması; sonraki dalgalara aittir.
- `V0-BKP-001` ve `V0-BKP-002` görevlerinin durumunu değiştirmek; bunlar v0 governance sahipliğindedir. `V1-RMD-086` kanıtı bu görevlerin beklediği atılabilir PostgreSQL 18 round-trip'ini sağlar ve runbook içinde bu ilişki belgelenir.
- Tesis dışı (off-site) şifreli yedek ve otomatik zamanlanmış geri yükleme; `V15-BKP-001` ve `V15-BKP-002` kapsamındadır.

## Dependencies

- V1-GOV-047

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-086`) ve kapanış görevi (`V1-GOV-049`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-086
