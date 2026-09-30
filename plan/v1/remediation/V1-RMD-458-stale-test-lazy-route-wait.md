# V1-RMD-458 - stale.test.ts: tembel yüklenen ekran için sabit bekleme yerine koşula bağlı bekleme

- Task ID: V1-RMD-458
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

`src/Clients/PosTerminal/src/stale.test.ts` içindeki "gives the pairing dialog an accessible modal contract" testi, uygulamanın
tembel (React.lazy) yüklenen ekranını sabit sayıda 40 ms'lik turla (toplam 400 ms) bekliyor. Kod dönüştürme önbelleği soğukken
(testi tek başına çalıştırınca hep, tam takımda ise makine yüküne göre arada) ekran bu sürede gelmiyor ve test
"expected undefined to be defined" ile kırmızı oluyor; aynı test kod değişmeden yeşil de çıkıyor. Bu, gerçek bir hata değil,
bekleme stratejisi hatasıdır. Sabit tur sayısı yerine beklenen düğme görünene kadar (üst sınırlı) bekleme kullanılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-458-stale-test-lazy-route-wait.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/stale.test.ts

## In scope

- Yalnız `stale.test.ts` içindeki bekleme yardımcıları; testlerin doğruladığı davranış değişmez.

## Out of scope

- Uygulama kodu, diğer testler ve `App.tsx` yükleme stratejisi.

## Dependencies

- None

## Acceptance evidence

- `src/stale.test.ts` tek başına ve `pnpm test` (tam takım) art arda beş kez exit code 0; `pnpm run lint` ve `pnpm run typecheck`
  exit code 0. Çıktılar `evidence/V1-RMD-458/` altındadır.

## Handoff

- None
