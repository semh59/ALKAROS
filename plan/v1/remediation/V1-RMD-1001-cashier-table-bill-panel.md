# V1-RMD-1001 - Masa planının sağ paneli masanın hesabını ve eylemlerini taşır

- Task ID: V1-RMD-1001
- Status: Planned
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Seçili masanın hesabı sağ panelde görünür: boş masada Sipariş al, dolu masada Ürün ekle; Öde tek dolu düğmedir; Mutfağa gönder (n) ve Mutfağa göndermeden hesaba ekle; Böl, Masa taşı, İptal/iade. Hesabı açık masada ürün ekleme kapalıdır ve nedeni söylenir (995 ile uyumlu).

## Owned surface

- `evidence/V1-RMD-1001/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - Başlangıç commit'inde bu listeye gerçek dosya yolları yazılır (plan commit'i ayrı).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Eylemlerin her biri mevcut istemci işlevlerine bağlanır (api.startTableOrder, submitOrder, addWithoutKitchen, collectionHrefForOrder, Transfer, cancelBill); test her durum için.
2. Üçüncü deneme #6: mutfaksız ekle düğmesi sepette gönderilmemiş ürün varken de görünür ve yarım gizli değildir.
3. Üçüncü deneme #4: sağ panelde ve sepet başlığında masa adı görünür.

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Koyu tema

## Dependencies

- V1-RMD-1000

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
