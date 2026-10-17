# V1-RMD-591 - PosTerminal testlerinde zaman aşımı kararsızlığı

- Task ID: V1-RMD-591
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

PosTerminal test paketinde `Cashier.*` ekran testleri tek başlarına yaklaşık 4,5 saniye sürüyor ve varsayılan 5 saniyelik sınıra çok yakın; paket paralel koşunca her koşuda farklı 6-10 test zaman aşımıyla düşüyor (değişiklikler geri alınmış kodda da). Paket güvenilir biçimde yeşil olmalı.

## Owned surface

- `plan/v1/remediation/V1-RMD-591-posterminal-test-timeout.md`
- `evidence/V1-RMD-591/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/vite.config.ts - yalnız test zaman aşımı ayarı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-590-migration-manifest-and-printer-route-test.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Test başına ve kanca başına zaman aşımı sınırının, yük altında ekran testlerinin tamamlanacağı bir değere çıkarılması; üç ardışık tam koşuda sıfır hata kanıtı.

## Out of scope

- Ekran testlerinin kendisini hızlandırmak veya bölmek.

## Dependencies

- V1-RMD-590

## Acceptance evidence

1. Tam PosTerminal paketi üç ardışık koşuda sıfır hata.
2. Çıktılar `evidence/V1-RMD-591/` altındadır.

## Handoff

- V1-RMD-592
