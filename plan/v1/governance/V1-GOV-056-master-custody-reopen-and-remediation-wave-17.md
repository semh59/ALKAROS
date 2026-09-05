# V1-GOV-056 - Master custody reopen and remediation wave 17

- Task ID: V1-GOV-056
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Masaya oturtma akışında (`StartOrderAsync`), istemci güncelliğini yitirmiş bir `ExpectedTableRowVersion` gönderdiğinde sunucu masa hâlâ tamamen oturtulabilir olsa bile "Table row version is stale" çakışması fırlatıyor; garson sahada müşteri önünde anlamsız bir "yenile" hatası görüyor. Sunucuda zaten `FOR UPDATE` kilidi altında taze durum niyet doğrulaması var; sürüm eşitlik kapısı bu doğrulamanın önüne geçen tek yanlış-pozitif kaynağıdır. Bu friction'ı kaldırmak için `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-090`) ve kapanış görevi (`V1-GOV-057`) planlanır. `V1-RMD-078` NotApplicable gerekçesi de sektör pratiğine göre düzeltilir.

## Owned surface

- `plan/v1/governance/V1-GOV-056-master-custody-reopen-and-remediation-wave-17.md`
- `plan/v1/remediation/V1-RMD-090-seating-tolerates-stale-table-version.md`
- `plan/v1/governance/V1-GOV-057-wave17-master-audit-reseal-and-gate-closure.md`
- `plan/v1/remediation/V1-RMD-078-table-metadata-field-level-merge.md`
- `plan/GATES.md`
- `plan/v1/README.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 17. dalga görevleriyle (`V1-RMD-090`, `V1-GOV-057`) genişletmek ve sayımı güncellemek.
- Yüzey devirleri: `src/Host/DualScreen/DualScreenStore.cs` ve `tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs` yüzeyleri `V1-RMD-077`'den `V1-RMD-090`'a devredilir.
- `V1-RMD-078` NotApplicable gerekçesine sektör pratiği notu eklemek: generic column merge bu problemde kullanılmaz; doğru yaklaşım kilit altında taze durum niyet doğrulamasıdır ve `V1-RMD-090` bunu oturtma yoluna uygular.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- `/tables/{tableId}/status` durum geçişi ve zemin planı kaydetme gibi yapılandırma yollarında optimistic locking; bunlar nadir çakışır ve insan yenilemesi gerektirir, sert kalır.
- Reservations ve table transfer yollarında sürüm davranışını değiştirmek; ayrı değerlendirme.
- `table_mgmt.tables` şemasını bölmek (V1.5 yapısal seçenek).

## Dependencies

- V1-GOV-055

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-090`) ve kapanış görevi (`V1-GOV-057`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-090
