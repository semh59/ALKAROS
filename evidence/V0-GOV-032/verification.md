# V0-GOV-032 verification

Tarih: 2026-08-04
Yürütme: opencode-v0-gov-032

## Kaynak

- Kullanıcı onaylı plan değişikliği `TRACEABILITY.md` C44 (commit `0bc0193`).
- Yüzey devir kaydı `V0-GOV-031` dosyasında (commit `9c441d1`).

## Değişen yüzey (allowlist ile birebir)

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-032/**`
- Görev dosyasında yalnız `Status` metadata satırı (Planned -> InProgress).

Plan değişikliği kapsamındaki plan dosyaları (`GATES.md`, `TRACEABILITY.md`,
`EXECUTION_READY_PLAN.md`, `V0-GOV-031` transfer notu, `V0-GOV-032` görev
dosyası) ayrı kullanıcı onaylı plan commit'lerinde (`0bc0193`, `9c441d1`)
işlendi.

## Yapılan değişiklik

`task_scope_tool.py`:

- `_DEFERRED_TASKS_START/END/HEADER/SEPARATOR`, `_DEFERRED_TASKS_ROW`,
  `_DEFERRED_TASK_RECORDS` (11 kayıt, GATES.md ile birebir), `_DEFERRED_TASK_IDS`.
- `parse_v0_deferral_ids(plan_dir)`: fail-closed ayrıştırma — marker'lar
  tam bir kez, header/separator/satır biçimi strict, tekrar red, kayıt kümesi
  sabit 11 kayıtla exact eşleşme.
- `check_entry_gate`: istisna setine kayıtlı görevler için remediation
  exception yolu korunur (sıra değişmez). Diğer görevler için `GATE-V0-EXIT`
  türetiminde devir kimlikleri kapanma koşulundan muaf sayılır; devir
  tablosu bozuk/yinelenen/eksik ise `deferral table rejected` (fail-closed),
  `GATES.md` dosyası hiç yoksa gate açık listesiyle reddedilir (muafiyet
  uygulanmaz). Muafiyet yalnız `GATE-V0-EXIT` içindir (diğer aşama
  gate'leri test ile korunur).

`tests/Architecture/TaskScope/test_task_scope.py`:

- `_write_v0_deferrals` fixture helper, `DEFERRED_ROWS` (11 satır) ve
  `DEFERRED_TASK_IDS`.
- Yeni `TestDeferredV0EntryGate` sınıfı (7 test): geçerli tablo ile istisna
  seti dışı V1 görevi geçer; devirli olmayan V0 görevi gate'i açar; tablo
  yoksa fail-closed; yinelenen/eşleşmeyen/bozuk kayıt fail-closed;
  muafiyet `GATE-V1-EXIT`'e uygulanmaz.
- `test_unapproved_task_cannot_bypass_open_v0_entry_gate` assertion'ı yeni
  fail-closed mesajına göre güncellendi (GATES.md remediasyon tablosu varken
  devir marker'ı yoksa `GATE-V0-EXIT ... rejected`).

`docs/engineering/task-scope-contract.md` ve `plan/VALIDATION_CONTRACT.md`:
devir tablosu sözleşmesi (C44) eklendi.

## Kabul komutları ve sonuçlar

- `py -m pytest tests/Architecture/TaskScope -q` -> 80 passed, exit 0
  (3 bağımsız koşunun üçü de exit 0; transcript `pytest-task-scope.txt`).
- `py tools/plan-audit/plan_audit_tool.py validate` -> Validation errors: 0,
  exit 0. GOV-031/GOV-032 `SURFACE_DUPLICATE` için C42 desenli yüzey devri
  `V0-GOV-031` dosyasına işlendi (`9c441d1`).
- `py tools/plan-audit/plan_audit_tool.py validate-coverage` -> 0, exit 0.
- `generate-audit-report` -> 409 md / 198 added / 1827 findings, exit 0
  (son durum: GOV-032 Done + evidence dosyaları dahil).
- `generate-manifest` -> 409 md, 27867 satır, SHA-256
  `8B11A531527A084FA5E82435B7E81345ECDB464F90A788B7C5F3B5E9089A2A4E`, exit 0.
- `verify-manifest` -> Manifest errors: 0, exit 0.
- Kapanış scope check (GOV-031 deseni: audit regen'den önce, temiz audit
  dosyalarıyla): `py tools/task-scope/task_scope_tool.py --task-id
  V0-GOV-032 --format text` -> `OK: All changes within scope for V0-GOV-032`,
  exit 0. Sonraki audit regen'inde plan/AUDIT_REPORT.md + AUDIT_MANIFEST.json
  araçça yeniden üretildi ve aynı commit'te işlendi (V1-FND-008/V0-GOV-030
  nominal sahibi; FIND-IA-0046 / `78958da` emsal kaydı).
- `py tools/task-scope/task_scope_tool.py --task-id V1-FND-010 --format text`
  (worktree değişiklikleri varken): gate hatası artık yalnız devirli olmayan
  tek görevi (V0-GOV-032 InProgress) listeliyor; 11 devirli V0 görevi açık
  listesinden çıktı (C44 öncesi `V0-BKP-001 (Blocked), ..., V0-YSP-001
  (Blocked)` listesi tamamen kayboldu). V1-FND-003 için de aynı gözlem;
  transcript'ler `task-scope-v1-fnd-010.txt`, `task-scope-v1-fnd-003.txt`.
- Commit sonrası temiz worktree: `--task-id V1-FND-010` -> `OK: All changes
  within scope for V1-FND-010`, exit 0 (GOV-032 Done sonrası GATE-V0-EXIT
  makinece kapalı).

## Kalan durum

- `V0-GOV-032` Done olduğunda `GATE-V0-EXIT` türetilmiş kontrolü tüm V1
  görevleri için kapalı olur; bu, C41 kapanış kararını makinece doğrular.
- Devir kimlikleri gate kapanış kanıtı üretmez; kanıtlar ilgili aşamada
  (V12-V20) toplanır (C40).

## Denetim düzeltmesi (2026-08-04, bağımsız denetim)

- Kapanışta `verification.md` manifest üretiminden sonra düzenlendiği için
  `f56ead9` manifest'i bu dosyanın eski hâlini (82 satır) kaydediyordu;
  commit'teki son hâl 92 satırdı ve `verify-manifest` 5 hata veriyordu.
- Bu kayıt, manifest bu dosyanın son hâliyle yeniden üretildikten sonra
  commit'lenmiştir; `verify-manifest` artık 0 hata üretir.
- Ders: görev kapanışında manifest, `evidence/**` ve görev dosyası dahil tüm
  değişikliklerin son hâlinden SONRA üretilir; kapanıştan sonra hiçbir
  evidence dosyası düzenlenmez (düzenleme ayrı düzeltme commit'i + yeniden
  üretim gerektirir).
