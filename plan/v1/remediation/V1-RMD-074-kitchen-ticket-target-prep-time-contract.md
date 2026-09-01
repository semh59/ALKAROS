# V1-RMD-074 - Kitchen ticket target preparation time contract

- Task ID: V1-RMD-074
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Mutfak biletine hedef hazırlık süresini domain alanı olarak eklemek. Bilet oluşturulurken istasyon varsayılan sabitinden `TargetPrepMinutes` atanır ve kitchen operations okuma sözleşmesinde döndürülür; böylece istemci geçen süre eskalasyonunu sabit yerine sözleşmeden alır. Alan V1'de kalıcı sütun değildir; sonraki istasyon ayarları görevi bu değeri yapılandırmaya bağlayabilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-074-kitchen-ticket-target-prep-time-contract.md`
- `src/Modules/Kitchen/TicketLifecycle/**`
- PO:2026-09-01 kararıyla src/Host/Experience/KitchenOperations/** ve tests/Host/Experience/KitchenOperations/** yüzeyleri V1-RMD-082’ye devredildi; bu historical task closed kalır.
- `tests/Modules/Kitchen/TicketLifecycle/**`
- `evidence/V1-RMD-074/**`

## In scope

- `KitchenTicket` aggregate'inde `TargetPrepMinutes` alanı ve pozitif değer doğrulaması; oluşturma sırasında istasyon varsayılan sabitinden atanması; durum ve kalem geçişlerinde korunması.
- Kitchen operations okuma DTO'sunun `targetPrepMinutes` alanını döndürmesi.
- Domain davranışının xUnit testleri.

## Out of scope

- Şema veya migration eklemek; alan V1'de kalıcı sütun değildir.
- İstemci arayüzünü değiştirmek; tüketim `V1-RMD-075` görevine aittir.
- Ürün veya kategori bazlı hazırlık süresi modeli; bu alan istasyon varsayılanıdır.

## Dependencies

- V1-GOV-040

## Deliverables

- `target_prep_minutes` sütunu, domain alanı ve okuma sözleşmesi alanı ile mutfak bileti.

## Acceptance evidence

- `dotnet test` mutfak modülü ve host kitchen operations testleri sıfır hata verir.
- Semih; yeni bir bilet oluşturduğunda okuma yanıtında `targetPrepMinutes` alanının dolu geldiğini doğrular.

## Handoff

- V1-RMD-076
