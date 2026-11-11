# V1-RMD-934 - Vardiya kapanış uyarısı çevrimiçi siparişleri de saysın

- Task ID: V1-RMD-934
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı (2026-11-11): vardiya kapanışındaki "ödenmemiş hesap" uyarısı çevrimiçi (Online) kaynaklı, gönderilmiş ama hesabı olmayan siparişleri de saysın. V1-RMD-926 bunları dışarıda bırakmıştı. Karar: canlı durumdaki (Submitted, Accepted, Preparing, Ready, Served) ve iptal edilmemiş hesabı olmayan her sipariş, kaynağı ne olursa olsun, açık hesap sayılır.

## Owned surface

- `evidence/V1-RMD-934/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashCloseStore.cs
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (çevrimiçi sipariş sayılır), düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Gün sonu raporu (ayrı görev)

## Dependencies

- V1-RMD-933

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
