# V1-RMD-1056 - Masa planında durum tek yerde tanımlanır, dikkat gerektirmeyen masada desen olmaz, ham terminal kodu başlıkta görünmez

- Task ID: V1-RMD-1056
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Masa planında aynı durum kartta bir, panelde başka renkte görünüyor (Ödeme bekliyor kartta mor, panelde şeftali; Dolu da şeftali), bütün ödeme bekleyen masalar aynı çizgili desenli olduğundan uzun süre açık kalan masa öne çıkmıyor, üstteki dikkat şeridi kartları aynen tekrarlıyor, başlıkta ham terminal kodu (TERMİNAL 2FD67369) var ve durum rozetleri 11 piksel. Durum rengi ve rozeti docs/CASHIER_UI_RULES.md tablosuna göre tek sınıftan (plan anahtarı) çıkar, kart ve panel aynı rozeti kullanır; desen yalnız dikkat gerektiren masaya (hesap isteyen, 45 dakikayı aşan) uygulanır; uzun süre açık masa renge ek olarak "Uzun süre açık" yazısı taşır; dikkat şeridi tıklanmaz yalnız sayı gösterir; adı olmayan terminalde ham kod yerine Terminal alanı hiç gösterilmez; masa planı yazıları en az 14 piksel olur. Salon planı kurulum tuvali (floorPlan.css) bu görevde değişmez.

## Owned surface

- `evidence/V1-RMD-1056/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/tablePlanModel.ts
  - src/Clients/PosTerminal/src/features/tables/tablePlanModel.test.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/shell/models.ts
  - src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; salon planı kurulum tuvali (floorPlan.css)

## Dependencies

- V1-RMD-1055

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
