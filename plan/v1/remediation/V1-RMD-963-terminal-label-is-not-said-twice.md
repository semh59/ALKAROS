# V1-RMD-963 - Başlıkta 'Terminal  Terminal D60B3DF9' tekrarı kalksın

- Task ID: V1-RMD-963
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: üst çubukta Terminal etiketinin değeri yine 'Terminal D60B3DF9' yazıyor (kelime iki kez, kimlik teknik). Terminal kaydında ad alanı yok; karar: değer yalnız kısa terminal kimliği olur (D60B3DF9) ve etiket kelimeyi taşır. Terminale ad verme ürün kararıdır, bu görevde yapılmaz.

## Owned surface

- `evidence/V1-RMD-963/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-962

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
