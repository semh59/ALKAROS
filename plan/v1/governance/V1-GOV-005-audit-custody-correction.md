# V1-GOV-005 - Correct audit custody and premature closure

- Task ID: V1-GOV-005
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: validation
- Surface state: Existing

## Goal

Tek bir assignee tarafından topluca ve doğrulanabilir kabul kanıtı olmadan `Done` yapılan full-audit/remediasyon
zincirini fail-closed duruma döndürmek; görevleri taze tek-ajan yürütmesine uygun exact sahiplik ve dependency sırasıyla
yeniden planlamak. Production kodu bu görevde değişmez.

## Owned surface

- `plan/v1/governance/V1-GOV-005-audit-custody-correction.md`
- `plan/v1/governance/V1-GOV-003-full-project-production-audit.md`
- `plan/v1/governance/V1-GOV-004-post-remediation-master-audit-reseal.md`
- `plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md`
- `plan/v1/remediation/V1-RMD-007-governance-manifest-and-build-provenance.md`
- `plan/v1/remediation/V1-RMD-008-host-api-auth-ratelimit-tls-and-telemetry.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/v1/remediation/V1-RMD-010-pos-terminal-ui-responsive-and-a11y.md`
- `plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_FINDINGS.json`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl`
- `evidence/V1-GOV-005/**`

## In scope

- Önceki tek-assignee `Done` durumlarını ve kanıtsız reseal iddialarını geçersizleştirmek.
- V1-GOV-003, V1-RMD-007..011 ve V1-GOV-004 için taze tek-ajan dependency zinciri ve exact write allowlist kurmak.
- Mevcut kanıt artifact'lerini silmeden hash'lemek ve doğrulanamayan sonuçlarını `REJECTED` olarak kaydetmek.
- Master rapor, findings ve ledger üzerinde doğrulanmamış `PASS`, `resolved` ve `resealed` hükümlerini fail-closed işaretlemek.

## Out of scope

- Production, test, migration, build, CI veya configuration dosyalarında remediasyon uygulamak.
- Önceki evidence artifact'lerini silmek, değiştirmek veya yeniden adlandırmak.
- Dış cihaz, sandbox, secret ya da imzalı go-live kanıtı olmadan risk waiver veya production-ready hükmü üretmek.

## Dependencies

- None

## Deliverables

- Taze tek-ajan yürütmesine hazır, exact owned surface ve sıralı dependency içeren audit/remediasyon görevleri.
- Önceki kapanış artifact'lerini SHA-256 ile kaydeden ve reddetme gerekçelerini açıklayan V1-GOV-005 kanıt paketi.
- Doğrulanmamış sonuçları açıkça `UNVERIFIED` veya `REJECTED` olarak işaretleyen master audit çıktıları.

## Acceptance evidence

- V1-GOV-003, V1-RMD-007..011 ve V1-GOV-004 `Done` değildir; her uygulama görevinin production/test/config yazma
  yüzeyi exact path ile tanımlıdır ve dependency zinciri `V1-GOV-003 -> V1-RMD-007 -> ... -> V1-GOV-004` sırasındadır.
- Önceki `evidence/V1-GOV-003`, `evidence/V1-RMD-007..011` ve `evidence/V1-GOV-004` dosyaları SHA-256 ile listelenir;
  ledger yapısal hataları, eksik davranış testleri ve başarısız temiz doğrulamalar gerekçeli olarak reddedilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate`, ilgili task-scope doğrulaması ve `git diff --check` gerçek
  exit code ile kaydedilir; geçmeyen kontrol fail-closed blocker olarak bırakılır.
- Semih, yeni bir ajanı önce V1-GOV-003'e atayıp ardından yalnız dependency'si `Done` olan bir sonraki görevi
  başlatabilir; hiçbir remediation görevi aynı ajan tarafından topluca kapatılamaz.

## Handoff

- V1-GOV-003
