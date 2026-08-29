# V1-RMD-035 - Deep code audit remediation

- Task ID: V1-RMD-035
- Status: Done
- Assignee: d3a90b4c-de70-40cc-8994-3b08e50b07ae
- Work type: implementation
- Surface state: Existing

## Goal

Statik kod denetiminde tespit edilen 7 kritik kusuru (DualScreen indirimli ürün güncelleme kısıt ihlali, Reopened hesap durum kilitlenmesi, BillItem modifiyeli indirim validasyonu, TableTransfer eşzamanlı deadlock riski, Audit bozuk JSON maskeleme ihlali, Idempotency eşzamanlı rollback kayıp hatası ve Table rezervasyon/temizlik durum geçiş eksiklikleri) düzeltmek ve ilgili domain/entegrasyon testleriyle doğrulamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md`
- PO:2026-08-29 kararıyla src/Host/DualScreen/DualScreenStore.cs yüzeyi V1-RMD-050'ye devredildi; bu historical task closed kalır.
- `src/Modules/Billing/BillFoundation/Bill.cs`
- `src/Modules/Billing/BillFoundation/BillItem.cs`
- `src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs`
- PO:2026-08-29 kararıyla IAuditSanitizer.cs yüzeyi V1-RMD-037'ye devredildi; bu historical task closed kalır.
- `src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs`
- `src/Modules/Tables/TableLifecycle/Table.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs`
- `tests/Modules/Billing/BillFoundation/BillDomainTests.cs`
- `tests/Modules/Tables/TableLifecycle/TableDomainTests.cs`
- `tests/Modules/Tables/TableTransfer/TableTransferDomainTests.cs`
- `tests/Modules/Audit/EventStore/AuditTests.cs`
- `tests/BuildingBlocks/Idempotency/IdempotencyKeyStoreTests.cs`
- `evidence/V1-RMD-035/**`

## In scope

- DualScreen'de indirimli ürün kalem miktar değişiminde `net_amount` hesaplamasının `gross_amount` ve DB CHECK kısıtı ile tam senkronizasyonu.
- Bill durum makinesinde `Reopened` faturanın `PartiallyAllocated`, `Allocated` ve `Cancelled` durumlarına geçebilmesinin sağlanması.
- BillItem içinde modifiye tutarlarının alt toplama dahil edilerek indirim aşım kontrolünün ve net/gross tutarların doğru hesaplanması.
- TableTransfer içinde kaynak ve hedef masaların deterministik GUID sırasıyla kilitlenerek eşzamanlı ters transferlerde deadlock (40P01) oluşumunun engellenmesi.
- AuditSanitizer'da geçersiz JSON payload'larında regex tabanlı güvenli maskeleme ve geçerli JSON string fallback'inin sağlanması.
- IdempotencyKeyStore'da eşzamanlı ilk işlemin rollback olması durumunda ikinci isteğin hata fırlatmak yerine işlemi sahiplenerek devam etmesi.
- Table durum makinesinde `Reserved -> Occupied` ve `Occupied -> Cleaning` geçişlerinin eklenmesi.

## Out of scope

- Yeni veritabanı şema/migration eklemesi veya tablo yapısal değişiklikleri.
- UI tasarım/tema değişiklikleri.

## Dependencies

- V1-RMD-034

## Acceptance evidence

- DualScreenStore indirimli ürün miktar güncellemesinde `gross_amount = net_amount + tax_amount` DB kısıtını ihlal etmez; ara toplam indirimi iki kez eklemez.
- Reopened faturaya tahsisat yapılabilir, ödenebilir ve iptal edilebilir.
- Modifiyeli ve indirimli sipariş kalemi faturaya hatasız aktarılır.
- Eşzamanlı ters masa transferleri kilitlenmez.
- Bozuk veya ham JSON audit kayıtları maskelenir ve JSONB'ye hatasız yazılır.
- İptal edilen eşzamanlı istek sonrası gelen istek başarısız olmaz.
- Rezervasyonlu masa `Occupied`, dolu masa `Cleaning` yapılabilir.

## Handoff

- V1-GOV-004
