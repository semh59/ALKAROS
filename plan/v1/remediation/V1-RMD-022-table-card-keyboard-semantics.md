# V1-RMD-022 - Table card keyboard semantics

- Task ID: V1-RMD-022
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Masa seçimi ile izinli hızlı masa komutunu ayrı, adlandırılmış ve klavyeyle erişilebilen kontroller olarak sunmak;
nested interactive markup'ı kaldırırken mevcut masa seçimi ve komut davranışını korumak.

## Owned surface

- `evidence/V1-RMD-022/**`
- PO:2026-08-28 desktop floor kararıyla tables.css V1-RMD-028'e devredildi; bu historical task closed kalır.

## Dependencies

- V1-RMD-020

## Acceptance evidence

- Her masa kartında masa seçimi ve varsa hızlı komut ayrı native `button` elemanlarıdır; bir interactive control başka
  bir interactive control içine yerleşmez ve her iki kontrol de açıklayıcı accessible name taşır.
- Tab sırası masa seçimini ve hızlı komutu bağımsız hedefler; Enter/Space mevcut `onSelectTable` ve `onAction`
  davranışlarını tam bir kez tetikler ve hızlı komut masa seçimini yanlışlıkla çalıştırmaz.
- Her iki kontrolün görünür focus stili vardır ve hızlı komutun dokunma hedefi en az 44x44 pikseldir; responsive kart
  düzeni 320 piksel genişlikte yatay taşma üretmez.
- İlgili table workspace testi, `pnpm test`, `pnpm run typecheck`, `pnpm run build` ve `git diff --check` exit code `0`
  verir.
- Semih Masalar ekranında Tab ile masa seçimi ile hızlı işlem arasında ilerler; Enter ile yalnız odaktaki işlemin
  çalıştığını ve görünür focus göstergesini doğrular.

## Handoff

- V1-RMD-010
