# V15-GOV-001 - Record that GATE-V15-ENTRY's dependency on GATE-V14-EXIT was waived

- Task ID: V15-GOV-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-22

## Goal

Bir bağımsız denetim (6 sıfır-bağlamlı ajanla, Faz 0 + Faz 1'in tamamı
üzerinde) şunu buldu: `V15-SEC-001/002/003`, `V15-OBS-001`, `V15-BKP-001/002`,
`V15-SUP-001` (7 task) hepsi `GATE-V15-ENTRY` fiilen açıkken `Done` oldu —
`GATE-V15-ENTRY`, `GATE-V14-EXIT`'in kapanmasını gerektiriyor
(`plan/GATES.md`), o da v1.4'ün 25 görevinin tamamının `Done`/`NotApplicable`
olmasını gerektiriyor; v1.4 bu oturumda henüz ele alınmadı (Faz 4 planda
sonraki adım), 24 görev hâlâ `Planned`, `V14-QNB-005` kalıcı olarak dış
kanıt bekliyor.

`V14-GOV-002` (2026-09-18/22) yalnızca `GATE-V13-EXIT`in `GATE-V14-ENTRY`
üzerindeki etkisini kaldırmıştı — bu görev o kararın kapsamının
`GATE-V15-ENTRY`/`GATE-V14-EXIT` sıralamasına da uygulandığını, aynı
2026-09-22 Semih kararı ("Gate kural ben koydum, şimdi de kaldırıyorum")
temelinde, geriye dönük olarak kayıt altına alıyor.

## Owned surface

- `plan/v1.5/governance/V15-GOV-001-lift-gate-v15-entry-waiver.md`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `evidence/V15-GOV-001/**`

## In scope

1. `plan/GATES.md`'nin `GATE-V15-ENTRY` satırına, kararın kaynağını ve
   kapsamını kaydeden bir not.
2. `plan/TRACEABILITY.md`'ye `C101` kaydı.
3. **Bilinçli tasarım kararı — V13'ten farklı:** `V13-EXIT` waiver'ı
   (`V14-GOV-002`, `tools/task-scope/task_scope_tool.py`) mekanik, tekrar
   doğrulanabilir bir waiver tablosu kullandı çünkü orada KALICI (dış
   sözleşme bekleyen) bir engel vardı — 14 V13 görevi asla `Done` olamaz.
   Burada durum farklı: v1.4'ün 24 görevi (biri hariç, `V14-QNB-005`) sırf
   HENÜZ ELE ALINMADIKLARI için açık — Faz 4 ilerledikçe bu sayı azalıp
   sıfıra inecek ve `GATE-V14-EXIT` normal şekilde (`V14-QNB-005` için ayrı
   bir waiver'la, o zaman) kapanacak. Bu yüzden burada YENİ bir mekanik
   waiver tablosu/kod değişikliği YAPILMADI — yalnızca tarihsel bir karar
   kaydı. (CI zaten push-to-master akışında hiç çalışmıyor — bkz.
   `.github/workflows/task-scope.yml`'nin `if: github.event_name != 'push'`
   koşulu — bu yüzden bu tutarsızlık gerçek geliştirme akışını hiçbir zaman
   mekanik olarak engellemedi.)

## Out of scope

- v1.4'ün kendi 24 görevini `Done`/`NotApplicable` yapmak — bu Faz 4'ün işi.
- `V14-QNB-005` için kalıcı bir waiver mekanizması kurmak — v1.4 tamamlanma
  noktasına yaklaşınca, `V14-GOV-002`'nin V13 için yaptığı gibi, ayrı bir
  görevde ele alınacak.
- `tools/task-scope/task_scope_tool.py`'de kod değişikliği — bu turda
  gerekmedi (yukarıdaki gerekçe).

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-22 (`V14-GOV-002`'de
kaydedilen aynı karar: "Gate kural ben koydum, şimdi de kaldırıyorum").
Bu kayıt, o kararın `GATE-V15-ENTRY`/`GATE-V14-EXIT` sıralamasına da
uygulandığını netleştiriyor — bağımsız bir denetim bu boşluğu bulana kadar
ayrıca belgelenmemişti.

## Deliverables

- `plan/GATES.md`: `GATE-V15-ENTRY` satır notu.
- `plan/TRACEABILITY.md`: `C101`.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
