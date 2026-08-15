# V1-FND-016 - Remediate root markdownlint findings

- Task ID: V1-FND-016
- Status: Done
- Assignee: opencode-V1-FND-016
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

2026-08-10 bağımsız denetiminin kök markdownlint bulgularını (origin-master 127,
head 162, worktree 173; toplam tekil 173) tek oturumda yalnız format/metadata
düzeltmeleriyle kapatmak. İçerik anlamı, kanıt değeri ve başka görevlerin
üretim yüzeyi değişmez; bulgu yalnızca Markdown biçim kurallarından kaynaklanır.

## Owned surface

- `evidence/ENV-001/env001-sdk-repair.md`
- `evidence/ENV-003/env003-test-matrix.md`
- `evidence/V0-GOV-030/verification.md`
- `evidence/V0-GOV-031/verification.md`
- `evidence/V1-FND-001/defect-1-closure.md`
- `evidence/V1-FND-001/verification.md`
- `evidence/V1-FND-002/defect-3-closure.md`
- `evidence/V1-FND-002/defect-4-closure.md`
- `evidence/V1-FND-002/defect-6-closure.md`
- `evidence/V1-FND-003/verification.md`
- `evidence/V1-FND-004/verification.md`
- `evidence/V1-FND-005/defect-5-closure.md`
- `evidence/V1-FND-005/verification.md`
- `evidence/V1-FND-009/closure-report-2026-08-05.md`
- `evidence/V1-FND-013/verification.md`
- `evidence/V1-FND-014/verification.md`
- `evidence/V1-FND-015/verification.md`
- `evidence/V1-IAM-001/closure-2026-08-05.md`
- `evidence/V1-IAM-001/defect-7-closure.md`
- `evidence/V1-IAM-002/closure-2026-08-08.md`
- `evidence/V1-IAM-003/audit-slnx-regression-2026-08-09.md`
- `evidence/V1-IAM-003/closure-2026-08-08.md`
- `evidence/V1-IAM-003/manifest-recover-2026-08-09.md`
- `evidence/V1-IAM-004/closure-2026-08-08.md`
- `evidence/V1-IAM-005/verification.md`
- `evidence/V1-SEC-003/closure-2026-08-05.md`
- `evidence/V1-SEC-003/defect-2-closure.md`
- `plan/TRACEABILITY.md` (C52 kayıt satırı + mevcut C45 satırının MD038/MD056/MD060 format düzeltmesi; içerik metni değişmez)
- `plan/v0/governance/V0-GOV-031-c42-entry-gate-approval.md` (yalnız MD013 satır sarması)
- `plan/AUDIT_REPORT.md` (yalnız araçla yeniden üretimi)
- `plan/AUDIT_MANIFEST.json` (yalnız araçla yeniden üretimi)
- `evidence/V1-FND-016/**`
- `plan/v1/foundation/V1-FND-016-markdownlint-format-remediation.md` (metadata; otomatik)

## In scope

- Kök markdownlint bulgularının 29 dosyada düzeltilmesi: fenced block dil
  etiketi (MD040), tablo sütun hizası (MD060), satır uzunluğu sarması (MD013),
  liste çevresi boş satır (MD032/MD031), dosya sonu yeni satır (MD047), kalın
  metin başlık (MD036), ul stili (MD004), başlık boşlukları (MD022), çift H1
  (MD025), tablo çevresi (MD058), çıplak URL (MD034), inline HTML (MD033),
  satır içi kod boşluğu/tablo sütun sayısı (MD038/MD056). Yalnız biçim
  düzeltmesi; cümle, sayı ve kanıt anlamı korunur.
- `plan/TRACEABILITY.md` C52 kayıt satırının eklenmesi ve C45 satırının
  MD038/MD056/MD060 kurallarına göre yeniden biçimlendirilmesi.
- Audit report ve manifestin araçla yeniden üretilmesi (report sonra manifest
  sırası; V0-GOV-030 ve C51 emsali).
- Tüm komut çıktıları ve exit code'ların `evidence/V1-FND-016/**` altına
  kaydedilmesi.

## Out of scope

- İçerik/metin anlam değişikliği, yeniden ifade, çeviri veya başlık metni
  değişikliği.
- Başka görevlerin `Status`/`Assignee` dışı metadata değişikliği; görev
  kapsamı genişletme; yeni product behavior.
- `.markdownlint-cli2.jsonc` yapılandırma değişikliği (kural eşiği
  gevşetilmez).
- `docs/**`, `AGENTS.md` ve bulgu listesinde yer almayan dosyalar (bu
  yollarda bulgu yoktur).
- Application kodu, migration ve başka görev kanıtlarının içerik düzeltmesi.

## Dependencies

- V1-FND-001
- V1-FND-002
- V1-FND-003
- V1-FND-004
- V1-FND-005
- V1-FND-009
- V1-FND-013
- V1-FND-014
- V1-FND-015
- V1-IAM-001
- V1-IAM-002
- V1-IAM-003
- V1-IAM-004
- V1-IAM-005
- V1-SEC-003
- V0-GOV-030
- V0-GOV-031

## Deliverables

- 29 dosyada yalnız Markdown biçim düzeltmeleri (denetim bulgu listesiyle
  birebir eşleşen; içerik anlamı korunur).
- `plan/TRACEABILITY.md` C52 kayıt satırı ve C45 satırının format düzeltmesi.
- Araçla yeniden üretilmiş `plan/AUDIT_REPORT.md` ve `plan/AUDIT_MANIFEST.json`.
- `evidence/V1-FND-016/**` altında komut, exit code ve sonuç kanıtları.

## Acceptance evidence

- `npx.cmd --yes markdownlint-cli2@0.23.2` repo kökünde exit code `0` ve 0
  hata (config globs: `plan/**`, `docs/**`, `evidence/**`, `AGENTS.md`).
- `python tools/plan-audit/plan_audit_tool.py validate` exit code `0`.
- `python tools/plan-audit/plan_audit_tool.py verify-manifest` exit code `0`.
- `git status --short` kapanış write-set'i allowlist ile birebir eşleşir.

## Handoff

- GATE-V1-EXIT
