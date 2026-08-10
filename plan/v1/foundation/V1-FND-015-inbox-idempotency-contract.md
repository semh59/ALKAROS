# V1-FND-015 - Mandate inbox side-effect idempotency contract

- Task ID: V1-FND-015
- Status: Done
- Assignee: opencode-v1-fnd-015
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.11-I.15
- CORR:C42

## Goal

Inbox handler sözleşmesinin at-least-once teslimde yeniden işlemeyi kabul
etmesini ve handler'ın tekrar-teslimde çift etki üretmemesini zorunlu
kılmak; lease expiry sonrası yeniden teslim yolunu testlerle kapatmak.

## Owned surface

- `src/BuildingBlocks/Messaging/IInboxHandler.cs` (V1-FND-002'den devredilmiştir, C42)
- `src/BuildingBlocks/Messaging/InboxMessage.cs` (V1-FND-002'den devredilmiştir, C42)
- `tests/BuildingBlocks/Idempotency/InboxRedeliveryContractTests.cs`
- `evidence/V1-FND-015/**`

## In scope

- Handler sözleşmesi: aynı mesaj idempotency anahtarı üzerinden birden fazla
  kez teslim edilebilir; handler uygulamaları tekrar-teslimde yan etki
  üretmemekle yükümlüdür (sözleşme dokümanı + arayüz açıklaması).
- InboxMessage üzerinde yeniden işleme/deneme bilgisi; lease expiry →
  yeniden teslim yolunda çift etki reddi testleri.
- Store davranışında sözleşmeyi destekleyen değişiklik gerekirse kapsam
  içindedir; şema değişikliği yalnız ayrı migration kaydıyla yapılır.

## Out of scope

- Webhook/order/payment tüketici davranışı, retry zamanlama politikası,
  mevcut Inbox/Outbox schema davranışının değiştirilmesi.

## Dependencies

- V0-ARC-003

## Deliverables

- İdempotency zorunlu handler sözleşmesi ve tekrar-teslim contract testleri.
- Komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- Tekrar-teslim senaryosunda çift etki üretilmediği contract testleri exit
  code `0` verir; tam çözüm testleri exit code `0` verir.

## Handoff

- V1-FND-002
