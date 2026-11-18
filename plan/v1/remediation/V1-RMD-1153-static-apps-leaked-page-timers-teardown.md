# V1-RMD-1153 - StaticApps testleri: bir önceki testten kalan sayfanın 5 saniyelik denetim sayacı sıradaki testi bozmasın

- Task ID: V1-RMD-1153
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

`split-payment-discount-pending.test.js` tam dosya koşusunda her seferinde sonuncu testte kırılıyor (30/31). Kök neden ürün değil test düzeneği: bekleyen onay bekleyen bir sayfa 5 saniyelik denetim sayacı (`setInterval`) kurar; test bitince DOM silinir ama sayaç ve eski sayfanın kapanışı yaşar. Dosya 5 saniyeyi aşınca eski sayfa ortak `fetch` taklidine ve ortak `document`'e dokunur: sıradaki testin indirim cevabını tüketir ve ekranı eski durumla çizer. Gerçek tarayıcıda sayfa kapanınca sayaç ölür.

1. Her test bitince o testin başlattığı tüm `setInterval` sayaçları temizlenir (ortak kurulum dosyasında).
2. Bu davranış kendi testiyle kilitlenir.
3. İlgili iki dosya (`split-payment-discount-pending`, `split-payment-approval-restore`) tam paket koşusunda kırmızı olmaz.

## Owned surface

- `evidence/V1-RMD-1153/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/support/setup.js
  - tests/Clients/StaticApps/support/page-timer-teardown.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı kanıt (tam dosya koşusu), düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Tam `tests/Clients/StaticApps` koşusu

## Out of scope

- Ürün kodu (split-payment.js) değişikliği; testlerin gevşetilmesi veya silinmesi

## Dependencies

- V1-RMD-1146

## Acceptance evidence

1. Kırmızı ve yeşil çıktılar, mutant listesi
2. İki ardışık bağımsız qa-* incelemesi
3. Tam StaticApps koşusu 0 kırmızı, exit code 0

## Handoff

- None
