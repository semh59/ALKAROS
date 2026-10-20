# V1-RMD-656 - Ortak denetim yazıcısı ve modüllerdeki elle yazılmış kayıt satırları

- Task ID: V1-RMD-656
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20). Orders ve OnlineOrdering modüllerinde üç yerde denetim satırı ham SQL ile yazılıyordu ve ortak temizleyiciyi ve doğrulamayı atlıyordu (kalem iptali/ikram işleyicisi, çevrimiçi platform kimlik bilgisi deposu, çevrimiçi mağaza durumu servisi). Bu iki modül Audit modülüne bağımlılık kenarı kazanır (Security, Support ve CustomerData'daki aynı kenar gibi; Audit yaprak modüldür), belgeye ve onaylı kenar listesine birlikte işlenir; üç nokta ortak yazıcıya geçer, olay adları ve alanlar aynı kalır. Ortak yazıcı dosyası (V1-RMD-655'te eklendi) plan sahipliği için bu görevin sahip yüzeyine alınır. Kaynak: V1-RMD-595 bulgusu F09 (modül noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-656-audit-shared-writer-module-sites.md`
- `evidence/V1-RMD-656/**`
- `src/Modules/Audit/EventStore/AuditEventWriter.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-655-audit-shared-writer-host-sites.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/ALKAROS.Orders.csproj - yalnız Audit proje başvurusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/packages.lock.json - yalnız Audit proje girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/OrdersModule.cs - yalnız DependsOn
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/ALKAROS.OnlineOrdering.csproj - yalnız Audit proje başvurusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/packages.lock.json - yalnız Audit proje girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/OnlineOrderingModule.cs - yalnız DependsOn
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/Credentials/PostgresOnlinePlatformCredentialStore.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/StoreStatus/OnlineStoreStatusService.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs - yalnız onaylı kenar listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md - yalnız Order ve Online Ordering satırlarının bağımlılık hücresi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Orders ve OnlineOrdering için Audit kenarı (proje başvurusu, DependsOn, belge satırı, onaylı kenar listesi); üç ham yazımın `AuditEventWriter`a geçmesi.

## Out of scope

- Başka modüllerin bağımlılık değişiklikleri.

## Dependencies

- V1-RMD-655

## Acceptance evidence

1. Modül sınırı testleri başarılı; üç noktanın mevcut testleri başarılı (olay adları ve alanlar aynı); ham `INSERT INTO audit.audit_events` başka hiçbir yerde kalmadı (arama kanıtı).
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-656/` altındadır.

## Handoff

- V1-RMD-657
