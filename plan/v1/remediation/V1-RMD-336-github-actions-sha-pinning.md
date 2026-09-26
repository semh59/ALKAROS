# V1-RMD-336 - GitHub Actions bağımlılıkları artık değişebilir etiket değil gerçek commit SHA'sına sabit

- Task ID: V1-RMD-336
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "GitHub Actions bağımlılıkları SHA değil
değişebilir tag ile pinlenmiş." Doğrulandı: `.github/workflows/task-scope.yml`'nin kullandığı yedi eylemin
(`actions/checkout@v6`, `actions/setup-dotnet@v5`, `actions/setup-node@v6`, `actions/setup-python@v6`,
`actions/upload-artifact@v4`, `astral-sh/setup-uv@v7`, `pnpm/action-setup@v5`) TÜMÜ değişebilir bir major-version
etiketiyle referanslanıyordu — bu etiketler, o eylemin gerçek maintainer'ı tarafından herhangi bir zamanda farklı
bir commit'e yeniden işaretlenebilir (tedarik zinciri saldırısı riski: bir eylemin deposu ele geçirilirse, `@v6`
etiketi kötü niyetli bir commit'e taşınıp bu workflow'u sessizce çalıştırabilir).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): .github/workflows/task-scope.yml
- `plan/v1/remediation/V1-RMD-336-github-actions-sha-pinning.md`

## In scope

1. Yedi eylemin her biri, GitHub API'den (`gh api repos/<owner>/<repo>/git/refs/tags/<tag>`, sonra
   `astral-sh/setup-uv`/`pnpm/action-setup` için ek olarak annotated tag nesnesinin kendisini de çözerek gerçek
   commit'e ulaşıldı) o anda o etiketin işaret ettiği GERÇEK commit SHA'sına sabitlendi: `owner/repo@<sha> # vN`
   biçimi, hem güvenli hem de hangi sürümün kastedildiğini okunabilir tutuyor.
2. Her SHA'nın gerçekten o eylemin geçerli bir `action.yml`'ini içerdiği GitHub API üzerinden doğrulandı (kör bir
   metin değişikliği değil).

## Out of scope

1. Dependabot/Renovate gibi otomatik bir SHA güncelleme mekanizması kurmak — bu, ayrı bir CI altyapısı kararı;
   bu görev yalnızca denetimin isim verdiği mevcut sabitlemesiz durumu kapatıyor.
2. Depodaki diğer workflow dosyaları — grep ile doğrulandı, bu yedi eylemin TÜMÜ yalnızca `task-scope.yml`
   içinde kullanılıyor; başka bir workflow dosyasında hiç eylem çağrısı yok.

## Dependencies

- None

## Acceptance evidence

- `gh api repos/<owner>/<repo>/git/refs/tags/<tag>` ile her yedi etiketin GERÇEK, o anki commit SHA'sı çözüldü
  (iki tanesi — `astral-sh/setup-uv@v7`, `pnpm/action-setup@v5` — annotated tag olduğu için tag nesnesinin
  kendisi bir kez daha çözülerek asıl commit'e ulaşıldı, aksi halde tag NESNESİNİN sha'sı yanlışlıkla
  pinlenecekti).
- `gh api repos/<owner>/<repo>/contents/action.yml?ref=<sha>`: her pinlenen SHA için gerçekten bir `action.yml`
  döndüğü doğrulandı (örnek: `actions/checkout`, `pnpm/action-setup`).
- `python3 -c "import yaml; yaml.safe_load(...)"`: dosya geçerli YAML olarak kalıyor.
- `grep -n "uses:"` çıktısı: 13 satırın TAMAMI artık `@<40 karakterlik hex sha> # vN` biçiminde.

## Handoff

- None
