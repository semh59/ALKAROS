# V1-RMD-1002 - Ödeme ekranı masa adı, kasada eklendi notu ve kısa ilk ekran

- Task ID: V1-RMD-1002
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Ödeme, bölme ve Bekleyen hesaplar ekranlarında ve fişte masa adı görünür; mutfağa gitmeden eklenen kalemler bu ekranlarda ve fişte kasada eklendi notuyla işaretlenir; ödeme ekranında Ödemeyi Ekle ilk ekranda görünür (ekran kısaltılır).

## Owned surface

- `evidence/V1-RMD-1002/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - Başlangıç commit'inde bu listeye gerçek dosya yolları yazılır (plan commit'i ayrı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Üçüncü deneme #4: ödeme ekranında masa adı.
2. Üçüncü deneme #5: kasada eklendi notu ödeme, bölme, Bekleyen hesaplar ekranında ve fişte.
3. Üçüncü deneme #7: 768x1024 ve 1366x768 çözünürlükte Ödemeyi Ekle kaydırmadan görünür.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-1001

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
