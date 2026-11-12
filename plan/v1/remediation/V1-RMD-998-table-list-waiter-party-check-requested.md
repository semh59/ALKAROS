# V1-RMD-998 - Masa listesi garson adı ve kişi sayısını taşır

- Task ID: V1-RMD-998
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa listesi sunucu yanıtı, yeni ana ekranın masa kartı için masanın güncel siparişini servis eden garsonun adını ve kişi sayısını taşır. Bölge adı mevcut bölge listesinden istemcide eşlenir. Hesap istedi durumu için ayrı sunucu alanı gerekmez: kasaya gönderilmiş ve henüz faturalanmamış hesap, mevcut Bekleyen hesaplar listesinden masa kimliğiyle eşlenir (ekran görevi 999).

## Owned surface

- `evidence/V1-RMD-998/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Tables/TableManagementContracts.cs
  - src/Host/Experience/Tables/TableManagementStore.cs
  - src/Host/Experience/Tables/TableManagementApplication.cs
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Masa listesi (GET tables) ServingWaiterName ve PartySize döndürür; siparişi olmayan masada ikisi de boştur.
2. Kırmızı test, düzeltme, mutasyon; iki bağımsız qa incelemesi.

## Out of scope

- Ekran değişikliği, hesap istedi sunucu alanı

## Dependencies

- V1-RMD-997

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
