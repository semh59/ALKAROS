# V1-GOV-068 - Master custody reopen and remediation wave 23

- Task ID: V1-GOV-068
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Semih onayıyla (2026-09-01), sahaya çıkmadan önce kalan tek risk kapatılır: Host yalnız `http://0.0.0.0:5080` dinliyordu ve garson telefonlarının güvenli bağlam (service worker / çevrimdışı kuyruk) elde etmesi tamamen ters proxy'ye (Caddy) ve `--trusted-network` eşleşmesine bağlıydı. `V1-RMD-096` Host'un kendisinin de kendinden imzalı bir sertifikayla `https://0.0.0.0:5443` üzerinde TLS sonlandırmasını ekler; proxy yolu birincil kalır ama artık tek nokta değildir. `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-096`) ve kapanış görevi (`V1-GOV-069`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-068-master-custody-reopen-and-remediation-wave-23.md`
- `plan/v1/remediation/V1-RMD-096-host-terminated-https.md`
- `plan/v1/governance/V1-GOV-069-wave23-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-068/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 23. dalga görevleriyle (`V1-RMD-096`, `V1-GOV-069`) genişletmek ve sayımı güncellemek.
- Yüzey devirleri: `src/Host/DualScreen/DualScreenOptions.cs` ve `tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs` V1-RMD-008'den, `src/Host/DualScreen/DualScreenApplication.cs` V1-RMD-077'den, `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs` V1-RMD-034'ten, `deploy/docker/README.md` V1-RMD-079'dan V1-RMD-096'ya devredilir.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Ödeme, fiscal veya yazıcı hazırlık iddiaları.
- Genel amaçlı ters proxy'nin kaldırılması; Caddy birincil TLS yolu olarak kalır.

## Dependencies

- V1-GOV-067

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-096`) ve kapanış görevi (`V1-GOV-069`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-096
