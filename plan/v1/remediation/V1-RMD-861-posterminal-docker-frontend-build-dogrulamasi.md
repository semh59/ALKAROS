# V1-RMD-861 - PosTerminal Docker frontend-build aşaması dist çıktısı doğrulanır

- Task ID: V1-RMD-861
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (h): Dockerfile frontend-build aşamasının (pnpm install --frozen-lockfile, pnpm build, build:nfc-standalone) bugünkü kaynakla sıfır çıkış koduyla bittiği ve /out/app ile /src/dist-nfc çıktılarının üretildiği gerçek docker build ile kanıtlanır. Compose yığınına dokunulmaz, dist git tarafından yok sayılır.

## Owned surface

- `evidence/V1-RMD-861/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - evidence/V1-RMD-861/** — docker build çıktısı özeti ve sonuç.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yalnız docker build --target frontend-build; compose ve çalışan konteynerlere dokunulmaz.
2. Hata çıkarsa kök neden bu görevde düzeltilmez, plan genişletmesiyle ayrı commit yapılır.

## Out of scope

- Kod, Dockerfile, compose, api ve web imajları.

## Dependencies

- None

## Acceptance evidence

1. docker build --target frontend-build çıkış kodu 0, günlük evidence/V1-RMD-861 altında özetlenir.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi.

## Handoff

- None
