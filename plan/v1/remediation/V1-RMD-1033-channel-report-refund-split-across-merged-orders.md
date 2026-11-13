# V1-RMD-1033 - Birleşik hesaptaki iade kanal raporunda siparişlere tutarı oranında dağıtılır

- Task ID: V1-RMD-1033
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kanal raporu iadeyi yalnız `billing.bills.order_id` ile siparişe bağlıyordu; birden çok siparişten birleştirilen hesapta (order_id boş) iade hiçbir siparişe düşmüyor, QR/Online siparişi başka siparişle birleşmişse payı kayboluyordu. İade artık hesaba bağlı bütün siparişlere (hesap kalemlerinden gelen siparişler dahil) sipariş tutarı oranında dağıtılır; kuruş yuvarlaması toplamı bozmaz (kümülatif yuvarlama: paylar toplamı tam iade tutarıdır). PO kararı 2026-11-13 (A).

## Owned surface

- `evidence/V1-RMD-1033/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/Channels/ChannelReportService.cs
  - tests/Modules/Reporting/Channels/ChannelReportTests.cs
  - tests/Modules/Reporting/Channels/Fixtures/ChannelReportTestDatabase.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1032

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
