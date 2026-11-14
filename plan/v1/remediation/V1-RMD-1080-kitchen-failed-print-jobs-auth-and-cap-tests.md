# V1-RMD-1080 - Başarısız yazdırma işi okumasının oturum reddi ve 500 kayıt sınırı testleri

- Task ID: V1-RMD-1080
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1054'ün açık riskleri kapatılır: print-jobs/failed uç noktası süresi dolmuş oturumu ve müşteri ekranı oturumunu reddeder (401 ve 403), ve sorgunun 500 kayıt sınırı davranış testiyle sabitlenir (en yeni 500 iş döner, en eski dışarıda kalır). Üretim kodu değişmez; sınırın sessiz kesme sayılıp sayılmayacağı gerekçeyle sonuç dosyasında belgelenir.

## Owned surface

- `evidence/V1-RMD-1080/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yeni testler ve mutasyonla kırmızı kanıtı
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Sorgunun modül deposuna taşınması

## Dependencies

- V1-RMD-1054

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
