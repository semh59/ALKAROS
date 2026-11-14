# V1-RMD-1090 - İki kişiye eşit bölmede ödeme ekranı en çok 8 dokunuşta biter (ölçüm testi)

- Task ID: V1-RMD-1090
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-14: bölme ödemede en sık akış (2 kişi eşit bölme, biri nakit biri havale) en çok 8 dokunuşta bitmeli. Kasiyer denemesi 9-10 dokunuş saymıştı; bu sayıya hesabı bulma adımları da dahildi. Ödeme ekranının kendi payı koddan 6 dokunuştur: Eşit bölüştür bölümünü aç, Hesapla, Ödemeyi Ekle (nakit), Havale/EFT yöntemi, banka hareketi onay kutusu, Ödemeyi Ekle. Bekleyen hesaplar listesinden ekrana varmak 2 dokunuş daha ekler (Bekleyen hesaplar, hesap kartı), toplam 8. Bu görev sayıyı gerçek ekranda ölçen bir test ekler ve sınırı 8'e bağlar; üretim kodu değişmez, çünkü sınır zaten sağlanıyor.

## Owned surface

- `evidence/V1-RMD-1090/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-equal-split-taps.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dokunuş sayısını ölçen test, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu; hesabı bulma ekranları; ürüne göre bölme

## Dependencies

- V1-RMD-1089

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
