# V1-RMD-665 - Mutfak kalem durumu ve olayı aynı işlemde

- Task ID: V1-RMD-665
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulgusu B010 kapanır. Mutfakta kalem durumu değişince (hazırlanıyor, hazır, servis edildi, geri alma) bilet kaydı ayrı bağlantıyla, ona bağlı "kalem durumu değişti" olayı ise bundan sonra başka bir bağlantıyla yazılıyordu; ikisi arasında süreç çökerse ya da olay yazımı hata verirse bilet değişir, garsona giden durum olayı kaybolurdu. Bilet kaydı ve olay artık aynı işlemde yazılır: biri başarısız olursa ikisi de geri alınır. Garson bildirimi işlem tamamlandıktan sonra gönderilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-665-kitchen-item-event-atomicity.md`
- `evidence/V1-RMD-665/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Kitchen/OrderItemStateSync/KitchenOrderItemStateSyncPublisher.cs - yalnız işlem içi yayınlama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs - yalnız kalem geçişi ve geri alma
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Kitchen/OrderItemStateSync/KitchenOrderItemStateSyncPublisherTests.cs - yalnız yeni imzaya uyum ve geri alma testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-664-typed-envelope-failures.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yayıncının bağlantı ve işlem alan hâli, iki çağrı noktasının tek işleme alınması, eski ayrı bağlantılı yayın imzasının kaldırılması, testler.

## Out of scope

- Bilet seviyesi geçişleri ve diğer mutfak olay yayıncıları; garson bildiriminin kendisi (en iyi çaba olarak kalır).

## Dependencies

- V1-RMD-664

## Acceptance evidence

1. Önce kırmızı test (olay yazımı hata verince bilet değişmiş kalıyor), sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-665/` altında çıktılar; Kitchen OrderItemStateSync ve KitchenOperations testleri yeşil.

## Handoff

- None
