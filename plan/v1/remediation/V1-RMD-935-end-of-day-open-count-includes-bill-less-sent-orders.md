# V1-RMD-935 - Gün sonu açık hesap sayısı hesapsız gönderilmiş siparişleri de saysın

- Task ID: V1-RMD-935
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-926 incelemesinden çıkan tutarsızlık ve Semih talimatı (2026-11-11): vardiya kapanışı hesapsız gönderilmiş siparişleri açık hesap sayarken gün sonu kapanışı ve önizlemesi yalnız hesap kayıtlarını sayıyor. Karar: gün sonu açık hesap sayısı (önizleme, kapanış kaydı ve denetim) hesabı olmayan canlı durumdaki (Submitted, Accepted, Preparing, Ready, Served) her siparişi, kaynağı ne olursa olsun, ek olarak sayar; sayım vardiya kapanışındakiyle aynı koşulu kullanır.

## Owned surface

- `evidence/V1-RMD-935/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs
  - tests/Host/Experience/Reporting/EndOfDayServerFiguresTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (önizleme ve kapanış sayısı), düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Gün sonu ekranındaki metin değişikliği

## Dependencies

- V1-RMD-934

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
