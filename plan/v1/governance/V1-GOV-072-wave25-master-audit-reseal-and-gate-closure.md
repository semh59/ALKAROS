# V1-GOV-072 - Wave 25 master audit reseal and gate closure

- Task ID: V1-GOV-072
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-04

## Goal

`V1-IAM-025` (25. dalga sağlamlaştırma + istemci tarafı kablolama)
tamamlandıktan sonra tüm test süitlerinin (C#, Vitest), tutarlılık denetim
betiğinin, plan bütünlüğünün ve manifest hash'lerinin doğrulanması ve
`GATE-V1-EXIT` kapısının 25. dalga (`V1-IAM-016..025`) için kesin olarak
yeniden mühürlenmesi.

## Owned surface

- `plan/v1/governance/V1-GOV-072-wave25-master-audit-reseal-and-gate-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `dotnet build -c Release` ve `-c Debug` sıfır uyarı / sıfır hata.
- Bu dalgada dokunulan her projenin izole `dotnet test` çalıştırması yeşil.
- `python tools/consistency-audit/consistency_audit.py` sıfır hata.
- `python tools/plan-audit/plan_audit_tool.py validate`, `validate-coverage`
  ve `verify-manifest` sıfır hata; `plan/AUDIT_REPORT.md` ve
  `plan/AUDIT_MANIFEST.json` mevcut ağaç durumuna göre yeniden üretilir.
- `GATE-V1-EXIT` kapısını `plan/GATES.md` (25. dalga reseal notu) ve
  `plan/v1/README.md` (görev matrisi ve sayaç) üzerinde kesin olarak
  mühürlemek.

## Out of scope

- Production kodunda yeni özellik eklemek veya `V1-IAM-025` kapsamını
  genişletmek.
- `V1-IAM-026`'nın (grant-class bill adjustment yüzeyi) implementasyonu —
  Semih'in tasarım kararını bekleyen, ayrıca planlanmış bir görev; bu
  görevin kapanışını bloklamaz (aşağıdaki not).

## Dependencies

- V1-IAM-025

## Deliverables

- Mühürlü `GATE-V1-EXIT` kapısı ve doğrulanmış audit manifesti.

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- Bu dalgada değişen veya eklenen her projenin izole `dotnet test` koşusu
  yeşil (yerel Postgres 18, `alkaros-test-pg` konteyneri, port 55432):
  `ALKAROS.Identity.Authorization.Tests` 185/185,
  `ALKAROS.Host.Experience.Authorization.Tests` 5/5,
  `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 4/4 (yeni proje),
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Host.Experience.Billing.Tests` 3/3,
  `ALKAROS.Architecture.Tests` 8/8,
  `ALKAROS.Host.Tests` `Manifest.ManifestTests` 16/17 (17. test,
  `Execution.MigrationExecutionTests`, yerelde `psql` PATH'te olmadığı için
  atlandı — G2, CI'da geçer; migration 050-052 zinciri ayrıca
  `docker exec alkaros-test-pg psql` ile 001..047'den sonra ayrı ayrı ileri
  ve tam geri doğrulandı).
- `dotnet test ALKAROS.slnx` (tüm paket, paralel): kalan projeler yeşil;
  paralel Postgres çekişmesi `ALKAROS.Identity.DeviceSessions.Tests`,
  `ALKAROS.Host.Experience.Tables.Tests` ve
  `ALKAROS.Identity.Authentication.Tests`'te geçici `DATABASE_UNAVAILABLE`
  flake'i verdi (G3, önceden belgeli); üçü de izole çalıştırıldığında
  sırasıyla 20/20, 8/8, 54/54 geçti — bu dalganın değişikliğiyle ilgisi yok.
  Tam seri paket koşusu için test otoritesi CI'dır (V1-GOV-070 deseni).
- Frontend: `tsc --noEmit` temiz; `vitest run` 109/109 (PosTerminal).
- `python tools/consistency-audit/consistency_audit.py`: temiz (yol boyunca
  bulunan 2 önceden var olan ihlal — V1-IAM-020/021'in dosyalarında, bu
  dalgadan önce landed — ayrı bir `fix` commit'iyle kapatıldı).
- `python tools/plan-audit/plan_audit_tool.py validate` / `validate-coverage`
  / `verify-manifest`: sıfır hata; `plan/AUDIT_MANIFEST.json` ve
  `plan/AUDIT_REPORT.md` yeniden üretildi.
- `plan/GATES.md` `GATE-V1-EXIT` satırı 25. dalganın kesin mühürlendiğini
  kaydeder. `plan/v1/README.md` görev sayacı: 274 görev — 267 `Done`, 5
  onaylı `NotApplicable`, 1 `Planned` (`V1-RMD-100`), 1 `Blocked`
  (`V1-IAM-026`).
- **Not (kapıyı bloklamayan, kaydedilen kalıntı):** `V1-IAM-026` (grant-class
  bill adjustment yüzeyi — bills.void/comp/discount) Semih'in tasarım
  kararını bekliyor; bağımsız denetimin önceden bilinen **B1** bulgusuyla
  aynı kök. `GATE-V1-EXIT`'in önceki kapanışları (`V1-GOV-070` dahil) B1'i
  zaten bilinen, izlenen, kapıyı bloklamayan bir kalıntı olarak taşıyordu;
  bu reseal aynı emsali izler — `V1-IAM-026` yeni bir bulgu değil, var olan
  B1'in bu dalgada netleşmiş kapsamıdır.

## Handoff

- GATE-V11-ENTRY
