# V1-FND-007 - Remediate independent audit findings

- Task ID: V1-FND-007
- Status: Done
- Assignee: opencode-v1-fnd-007
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10

## Goal

2026-07-31 tarihli sıfır-context bağımsız denetimde kanıtlanan foundation bulgularını, 2026-08-01 kullanıcı onaylı
plan değişikliği kapsamında düzeltmek: task-scope CI etkisizliği, self-allowlist ayrıştırma açığı, build manifest
git izlenebilirliği, ölü kod, sessiz catch, commit footer ihlalleri ve V0-CMP-003 kanıt metadata tutarsızlığı.

## Owned surface

- `.github/workflows/task-scope.yml`, `docs/engineering/task-scope-contract.md`
- `.gitignore`, `build/project-manifest.json`
- `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`
- `plan/TRACEABILITY.md`, `plan/GATES.md`, `plan/VALIDATION_CONTRACT.md`
- `plan/AUDIT_REPORT.md`, `plan/AUDIT_MANIFEST.json`
- `plan/v1/foundation/V1-FND-001-module-skeleton.md` (yalnız Owned surface daraltma satırı)
- `plan/v1/foundation/V1-FND-003-codex-task-scope-enforcement.md` (yalnız Owned surface daraltma satırı)
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md` (yalnız Owned surface daraltma satırı)
- `plan/v1/foundation/V1-FND-007-audit-remediation.md` (metadata; otomatik)
- `tools/plan-audit/**` ve `tools/task-scope/**` yüzeyleri V1-FND-008'e devredilmiştir; bu görev artık bu path'leri
  yazamaz.

## In scope

- Commit footer sözleşmesi: `V1-SEC-002` teslimatının `Task: V1-SEC-002` footer'lı commit'i ve `e2c9e3a` commit'ine
  `Task: V1-SEC-001` footer amend'i (2026-08-01 kullanıcı onayı).
- `task_scope_tool.py`'ye `--diff-base` modu: worktree yerine `git diff --name-status <base> HEAD` ile değişen yol
  seti; `.github/workflows/task-scope.yml` PR base SHA'sı üzerinden diff tabanlı çalışır; contract güncellenir;
  aynı fixture seti local ve CI'da aynı exit code/finding listesini üretir.
- `Owned surface` ayrıştırma sıkılaştırması: yalnız path şekilli backtick parçaları (`/`, `\`, `.`, `*`, `?`
  içeren) allowlist ögesi sayılır; serbest metin ve task ID parçaları yok sayılır.
- `build/project-manifest.json` git izlemeye alınır; `.gitignore`'a tek dosya istisnası eklenir;
  `ALKAROS.slnx` dosya sonu yeni satırı eklenir.
- Ölü kod silme: `Primitives/**` (Guard, Result, Entity, DomainEvent, ValueObject) ve
  `ModuleCompositionRoot.Modules` property'si; FND-001/FND-004/FND-003 yüzey daraltmaları plan değişikliği olarak
  `TRACEABILITY.md`'ye işlenir.
- `PsqlScriptRunner.KillProcessTree` sessiz catch'i yalnız `process.HasExited` guard'ı altında no-op olur; diğer
  durumda fail-closed yeniden fırlatır.
- V0-CMP-003 evidence metadata `Status: Done` düzeltmesi (kanıt içeriği değişmez); plan dosyası zaten Done.
- Governance kayıtları: `GATES.md` ve `VALIDATION_CONTRACT.md` zincir notu, `TRACEABILITY.md` FIND-IA kayıtları,
  `AUDIT_REPORT.md` ve `AUDIT_MANIFEST.json` yeniden üretimi; plan-audit tool'daki hard-code Markdown sayısının
  `V1-FND-007` dosyası için güncellenmesi (`tools/plan-audit/plan_audit_tool.py`).

## Out of scope

- Product iş mantığı, schema, migration, UI ve başka görevlerin Status/Assignee dışı metadata değişikliği.
- Yeniden üretilemeyen denetim iddialarının kurgusal düzeltmesi (örn. handoff karşılıklılık eksikliği sayısı;
  mekanik kurallarla sıfır eksik doğrulandı, kanıt `evidence/V1-FND-007/**` altındadır).
- FND-002/FND-006 görev içeriği; kapı sırası bu görevde kullanıcı onayıyla V1-FND-007'ye öncelik verir, diğer
  application görevleri için kural değişmez.

## Dependencies

- V1-FND-001
- V1-FND-003
- V1-FND-004
- V1-SEC-001
- V1-SEC-002
- V0-CMP-003

## Deliverables

- `tools/task-scope/**` altında `--diff-base` modu ve sıkılaştırılmış Owned surface ayrıştırıcısı; CI workflow'u
  PR diff tabanlı; `docs/engineering/task-scope-contract.md` güncel input/output sözleşmesi.
- `tests/Architecture/TaskScope/test_task_scope_diff.py` altında diff modu ve allowlist şekil filtresi testleri.
- `src/BuildingBlocks/ModuleComposition/Primitives/**` silinir; `ModuleCompositionRoot.cs` yalnız gerekli üyeleri
  tutar; `PsqlScriptRunner.cs` guard'lı kill davranışı.
- `.gitignore` istisnası ve izlenen `build/project-manifest.json`; `ALKAROS.slnx` newline düzeltmesi.
- `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md` metadata düzeltmesi.
- `plan/` governance kayıtları ve yeniden üretilen `AUDIT_REPORT.md` / `AUDIT_MANIFEST.json`.
- `evidence/V1-FND-007/**` altında tüm komut çıktıları ve exit code'lar.

## Acceptance evidence

- `dotnet build ALKAROS.slnx` ve çözüm testleri (Transactions 25/25, Secrets 21/21, SensitiveData 20/20) 0 hata.
- Task-scope pytest seti yeşil; `--task-id` ve `--diff-base` modlarında aynı JSON result contract'ı.
- `python tools/plan-audit/plan_audit_tool.py validate`, `verify-manifest` ve kök markdownlint 0 hata.
- Commit geçmişi her commit'te `Task: <TASK-ID>` footer'ı içerir.
- `git status --short` ile kapanış write-set'i allowlist ile birebir eşleşir.

## Handoff

- V1-FND-002
- GATE-V1-EXIT
