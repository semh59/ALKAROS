# V1-RMD-886 - DeployFilesTests eski .git beklentisi (Docker bağlamı .git'i bilerek tutuyor)

- Task ID: V1-RMD-886
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`DeployFilesTests.TheBuildContextExcludesTheEnvironmentFileAndGitHistory` `.dockerignore` içinde `.git` satırı arıyor ve bulamıyor. Kök neden ortam değil: V1-RMD-782 `.git` satırını bilerek kaldırdı (`Directory.Build.props` `git rev-parse HEAD` çalıştırır; `.git` bağlamda yoksa `dotnet publish` kırılır) ve bunu `tests/Deployment/test_container_contract.py` koruyor. Eski C# testi bu karara aykırı kaldı. Test yalnız hâlâ geçerli olan kuralı sınar: `.env` bağlamdan dışlanır; ad `.git` sınamasını artık iddia etmez.

## Owned surface

- `evidence/V1-RMD-886/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/MigrationComposition/Composition/DeployFilesTests.cs — eski beklenti
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testin `.git` beklentisinin kaldırılması, adın gerçek kurala çevrilmesi
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- `.dockerignore` ve Dockerfile içeriği, Python sözleşme testi

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
