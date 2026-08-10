# V1-FND-008 - Remediate boundary audit round 2

- Task ID: V1-FND-008
- Status: Done
- Assignee: opencode-v1-fnd-008
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C31

## Goal

V1-FND-007 kapanışının ardından yapılan bağımsız sınır denetiminde (2026-08-01) kanıtlanan bulguları, kullanıcı
talimatı "DÜZELT HEPSİNİ" (2026-08-01) kapsamında düzeltmek: (1) Owned surface ayrıştırıcılarında `-` bullet devam
satırlarının (çok satırlı backtick) sessizce düşmesi sonucu oluşan sahipsiz dosyalar, (2) push edilmemiş commit'lerde
eksik `Task:` footer'ları, (3) plan-audit aracındaki hard-code Markdown sayısı ve orphan denetimi eksikliği,
(4) sahiplik devri ve governance kayıtları.

## Owned surface

- `plan/v1/foundation/V1-FND-008-audit-remediation-round2.md` (kendi metadata dosyası, otomatik)
- `plan/v1/foundation/V1-FND-001-module-skeleton.md`
- `plan/v1/foundation/V1-FND-003-codex-task-scope-enforcement.md`
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md`
- `plan/v1/foundation/V1-FND-007-audit-remediation.md`
- `plan/v0/domain-contracts/V0-DOM-001-lifecycle-transition-contracts.md`
- `plan/TRACEABILITY.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope_continuation.py` (faz 1'de oluşturulur; faz 2'de sahiplik V1-FND-003'e
  devredilir)
- `Directory.Build.props`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez; aşağıdaki yüzey düzeltmeleri 2026-08-01 kullanıcı
  onaylı plan değişikliğidir.

## In scope

- Owned surface düzeltmeleri (sahipsiz dosyaların orijinal sahiplerine devri):
  - V1-FND-001: `src/BuildingBlocks/ModuleComposition/ModuleCompositionRoot.cs`,
    `tests/Architecture/ModuleBoundaries/**`,
    `src/BuildingBlocks/**/ALKAROS.*.csproj`, `src/**/packages.lock.json`, `tests/**/packages.lock.json` yüzeye eklenir.
  - V1-FND-004: `src/Host/Composition/Migrations/PsqlScriptRunner.cs` eklenir; devam satırı formatı düzeltilir.
  - V1-FND-003: `tests/Architecture/TaskScope/test_task_scope_diff.py` yüzeye eklenir;
    `test_task_scope_continuation.py` sahipliği faz 1'de V1-FND-008'dedir, faz 2 devri ayrı plan değişikliğiyle yapılır.
  - V1-FND-007: devredilen dosyalar (ModuleCompositionRoot.cs, PsqlScriptRunner.cs, test_task_scope_diff.py,
    ALKAROS.slnx) kendi yüzeyinden çıkarılır; sahiplik orijinal görevlerine döner.
  - V0-DOM-001: `docs/versioning-strategy.md` eklenir (655d0b2 tarihsel ihlalinin sahiplik kaydı).
- Ayrıştırıcı kök neden düzeltmesi: `task_scope_tool.py` ve `plan_audit_tool.py` Owned surface ayrıştırıcıları
  `-` bullet'inin devam satırlarını (başında `-` olmayan backtickli satırlar) okur; path-şekil filtresi korunur.
- `plan_audit_tool.py`: (a) yeni `UNOWNED_PRODUCTION_FILE` denetimi — izlenen `src/`, `tests/`, `database/`
  dosyalarının en az bir görev yüzeyiyle eşleşme zorunluluğu; (b) `verify-manifest` Markdown sayısı diskten
  türetilir, 248 hard-code'u kaldırılır; (c) AUDIT_REPORT üretecindeki sabit 247 ifadesi dinamikleşir.
- Yeni test dosyası `test_task_scope_continuation.py`: devam satırı ayrıştırma ve orphan tarama davranışı testleri.
- Commit footer düzeltmesi: push edilmemiş 14 commit (`e19eb6a..HEAD`) `Task:` footer'ıyla yeniden yazılır;
  `Directory.Build.props` RepositoryCommit pin'i yeni HEAD commit'ine güncellenir.
- Governance: `GATES.md`/`VALIDATION_CONTRACT.md` FND-008 notu, `TRACEABILITY.md` FIND-IA-0037..0043 kayıtları,
  `evidence/V1-FND-008/**` altındaki tarihli düzeltme notları, `AUDIT_REPORT.md`/`AUDIT_MANIFEST.json` yeniden üretimi.

## Out of scope

- Push edilmiş commit'lerin (`fc5ae22..8374fc3`) yeniden yazımı: force-push kararı kullanıcıdadır; kayıt düşülür.
- Sahibi belirsiz commit'ler için kurgusal Task ID atfı (`fc5ae22` pre-convention baseline, `8374fc3` infra chore,
  V0 batch commit'leri): kayıtla istisna, atıf yapılmaz.
- `67ebaf8` (FND-005 oturumu, FND-004 yüzeyi), `655d0b2` (V0-DOM-001, docs yüzeyi), `1784dc5` (FND-003 oturumu,
  ModuleComposition dosyası) tarihsel ihlalleri: geçmişe dönük değiştirilmez, TRACEABILITY kaydı düşülür.
- `docs/`, `tools/`, `build/`, `.github/`, `plan/` governance dosyaları (AGENTS.md, README'ler, lock dosyaları,
  OWNERSHIP.md vb.) orphan denetiminin kapsamı dışındadır; merkezi governance yüzeyi sayılır ve TRACEABILITY'de
  sınıflandırılır.

## Dependencies

- V1-FND-001
- V1-FND-003
- V1-FND-004
- V1-FND-007
- V0-DOM-001

## Deliverables

- Düzeltilmiş Owned surface bölümleri (FND-001/003/004/007, V0-DOM-001) — sahipsiz dosya kalmadığı `validate`
  çıktısıyla kanıtlanır.
- Devam satırı okuyan ayrıştırıcılar (iki araçta), orphan denetimi, diskten türetilen Markdown sayısı.
- `tests/Architecture/TaskScope/test_task_scope_continuation.py` test seti.
- `Task:` footer'lı push edilmemiş commit geçmişi ve güncel RepositoryCommit pin'i.
- `evidence/V1-FND-008/**` altında tüm komut çıktıları, exit code'lar ve yeniden yazım kanıtları.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata (UNOWNED_PRODUCTION_FILE kontrolüyle birlikte).
- `verify-manifest`: 0 hata; Markdown sayısı diskten türetilir.
- Task-scope pytest seti (mevcut 48 + yeni dosya) yeşil; `--task-id` ve `--diff-base` modlarında aynı kontrat.
- `dotnet build ALKAROS.slnx` ve çözüm testleri 0 hata; kök markdownlint 0 sorun.
- Sınır denetimi scripti: push edilmemiş aralıkta footer'sız commit 0; izlenen `src/`/`tests/`/`database/`
  sahipsiz dosya 0.
- Task-scope validator: FND-008 worktree ve diff modu `valid: true`.
- Kapanış write-set'i allowlist ile birebir eşleşir; commit'ler `Task: V1-FND-008` footer'ı taşır.

## Handoff

- V1-FND-002
- GATE-V1-EXIT
