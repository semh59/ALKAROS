# V1-RMD-1042 - Başka kasanın açık hesabı olan masayı taşımayı sunucu da reddeder; ekran Türkçe nedenini gösterir

- Task ID: V1-RMD-1042
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa taşıma uç noktası (table-management/transfers), kaynak masanın açık hesabı başka bir kasaya (terminale) aitse ve çağıran cash.session.override yetkisine sahip değilse işlemi "BILL_OTHER_TERMINAL" koduyla ve "Bu hesap başka bir kasada açık; yalnızca o kasa veya bir yönetici işlem yapabilir." Türkçe iletisiyle 409 olarak reddeder; masa taşınmaz. Şimdiye dek yalnız ekran düğmesi pasifti (V1-RMD-1039). Kasiyer ekranı sunucunun Türkçe iletisini pencerede olduğu gibi gösterir; testle kilitlenir. Semih kararı 2026-11-13.

## Owned surface

- `evidence/V1-RMD-1042/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Tables/TableManagementApplication.cs
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1041

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
