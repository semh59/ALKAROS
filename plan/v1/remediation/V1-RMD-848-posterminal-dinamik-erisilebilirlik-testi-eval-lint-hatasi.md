# V1-RMD-848 - PosTerminal erişilebilirlik testi: eval lint hatası

- Task ID: V1-RMD-848
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (V1-RMD-739 kasa kanıt/test borcu): `src/Clients/PosTerminal/src/dynamic-a11y.test.ts` sayfa betiğini `(0, eval)` ile çalıştırdığı için `pnpm run lint` (oxlint, `no-eval`) exit 1 veriyor ve PosTerminal derleme zinciri (`build` = lint + tsc + vite) kırık. Betik, `eval` olmadan, her testte taze kapanışla çalışacak biçimde yüklenir; lint hatasız geçer ve test davranışı değişmez.

## Owned surface

- `evidence/V1-RMD-848/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/dynamic-a11y.test.ts — betik yükleme yöntemi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı: `oxlint -c .oxlintrc.json src` `no-eval` hatasıyla exit 1 (log).
2. Betiği `eval` yerine kabul edilen bir yolla (ör. `new Function` ya da jsdom betik öğesi) çalıştır; lint 0 hata, `dynamic-a11y.test.ts` aynı sayıda test ile yeşil.
3. Mutasyon: betik hiç çalıştırılmazsa testler düşer (yükleme gerçekten yapılıyor).

## Out of scope

- Lint uyarıları (warning) ve diğer test dosyaları.
- Uyarı sayısı bütçesini (`--max-warnings`) değiştirmek.

## Dependencies

- None

## Acceptance evidence

1. Lint exit 0 (hata sayısı 0), `dynamic-a11y.test.ts` ve tüm PosTerminal vitest koşusu exit 0; çıktı `evidence/V1-RMD-848/` altında.
2. Mutasyon cp+cmp ile geri alınmış kanıt.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
