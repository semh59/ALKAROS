# V1-FND-003 - Enforce Codex task write boundaries

- Task ID: V1-FND-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10

## Goal

Aktif tek `Task ID` için izin verilen write-set dışındaki her dosya değişikliğini local ve CI ortamında fail-closed
olarak reddetmek.

## Owned surface

- `tools/task-scope/**`, `tests/Architecture/TaskScope/**`
- `.github/workflows/task-scope.yml`, `docs/engineering/task-scope-contract.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Görev Markdown'ından `Task ID`, status, assignee, dependency ve `Owned surface` değerlerini strict parser ile okuma.
- Staged, unstaged, untracked, deleted ve renamed Git yollarını görev allowlist'iyle karşılaştırma.
- Windows path case/separator normalizasyonu, glob traversal reddi ve rename için eski/yeni yol kontrolü.
- Aktif görev metadata dosyası ile `evidence/<Task-ID>/**` için sınırlı evrensel izin.
- Local preflight komutu, CI required check ve fail-closed makine tarafından okunabilir sonuç sözleşmesi.

## Out of scope

- Product iş mantığı, UI, database schema, migration, dependency upgrade ve repository-wide formatting.
- Branch protection yönetimi, kullanıcı değişikliklerini geri alma veya kapsam dışı dosyayı otomatik düzeltme.
- Görev scope'unu execution sırasında değiştirme ya da birden fazla `Task ID` kabul etme.

## Dependencies

- V0-ARC-001
- V1-FND-001

## Deliverables

- `tools/task-scope/**` altında strict task parser ve write-set doğrulama komutu.
- `.github/workflows/task-scope.yml` required check tanımı ve local/CI aynı sonuç contract'ı.
- `tests/Architecture/TaskScope/**` altında allow/deny, dirty-worktree, untracked, delete, rename, path traversal ve
  Windows normalization testleri.
- `docs/engineering/task-scope-contract.md` içinde exit code, input, output ve failure recovery sözleşmesi.

## Acceptance evidence

- Geçerli görev, kendi `Owned surface`, kendi metadata satırları ve `evidence/<Task-ID>/**` değişiklikleriyle zero exit
  üretir.
- Başka görev dosyası, shared/root dosya, önceki migration, lockfile veya izin dışı generated file değişikliği non-zero
  exit üretir ve kesin path'i bildirir.
- Rename testinde eski ya da yeni yoldan yalnız biri izinliyse işlem reddedilir; untracked ve deleted yollar atlanmaz.
- Eksik/çoklu task kimliği, tamamlanmamış dependency, yanlış status/assignee, bozuk Markdown veya path traversal için
  validator fail-closed davranır.
- Aynı fixture seti local komut ve CI üzerinde aynı exit code ve sıralı finding listesini üretir.

## Handoff

- V1-FND-004
