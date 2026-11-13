# V1-RMD-1055 - Kasa arayüz kuralları belgesi yazılır ve bir testle koda bağlanır

- Task ID: V1-RMD-1055
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Planned

## Goal

Kasa ekranlarında punto, dokunma hedefi, durum renkleri, düğme hiyerarşisi, para biçimi, başlık düzeni ve yıkıcı işlem onayı için yazılı kural yoktu (docs/UI_STYLE_GUIDE.md yalnız dil ve hata metnini kapsar); iki bağımsız denetçi aynı durumun iki renkte, ödeme ekranında üç başlığın ve düğme renklerinin tutarsız olduğunu buldu. Yeni docs/CASHIER_UI_RULES.md mevcut --ds-* (PosTerminal) ve --color-* (Cashier vanilla) jetonlarına dayanan kuralları ve tek durum tablosunu yazar; bir test tablodaki durum anahtarlarını, etiketlerini, ikonlarını ve jeton adlarını gerçek koda karşı doğrular. Kural ihlalini ekranlar sonraki görevlerde (V1-RMD-1056..1059) kapatır; bu görev yeni renk eklemez.

## Owned surface

- `docs/CASHIER_UI_RULES.md`
- `src/Clients/PosTerminal/src/design-system/cashierUiRules.test.ts`
- `evidence/V1-RMD-1055/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Belge, ona bağlı test, kırmızı test, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- docs/UI_STYLE_GUIDE.md ve tokens.css değişikliği; ekranların kurallara uyarlanması

## Dependencies

- V1-RMD-1053

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
