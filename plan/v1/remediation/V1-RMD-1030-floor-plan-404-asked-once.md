# V1-RMD-1030 - Planı olmayan bölge için kat planı isteği sayfa başına bir kez sorulur

- Task ID: V1-RMD-1030
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa ekranı her yüklemede ilk bölgenin kat planını istiyor; planı çizilmemiş bölgede sunucu 404 döndüğü için oturum boyunca konsolda 18 kırmızı satır çıkıyordu. Planı olmadığı öğrenilen bölge sayfa yüklenene kadar tekrar sorulmaz; Yenile ve bölge seçimi yeniden sorar. cash-sessions/active 404 yanıtı kasa kapalı anlamında sunucu sözleşmesi olduğu için bu görevin dışındadır.

## Owned surface

- `evidence/V1-RMD-1030/**`
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

- V1-RMD-1029

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
