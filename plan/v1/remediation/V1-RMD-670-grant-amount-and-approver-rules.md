# V1-RMD-670 - Yetki talebinde gerçek tutar, onaylayan yetkisi, kısıtlı kullanıcı ve çevrimdışı tekrar kuralları

- Task ID: V1-RMD-670
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B009 (Yüksek), B008, B013 ve B015 kapanır. Garsonun gönderilmiş kalem iptali artık iptal edilen kalemin gerçek tutarıyla (eskiden 0) talep açar; yüzde indirimi, o hesapta indireceği TL tutarına çevrilerek talep edilir (eskiden yüzdenin kendisi TL limitiyle karşılaştırılıyordu). Böylece politika limiti, yetki devri sınırı ve günlük rapor aynı parayı görür. Bir talebi onaylayan ya da reddeden kişi, talep edilen işlemin iznine (iptal, ikram, indirim) kendisi de sahip olmalıdır; sahip değilse 403 `APPROVER_LACKS_PERMISSION`. Davranışsal kısıtlama açıkken talep, kişisel bütçe ve yetki devri yoluyla da otomatik onaylanmaz, doğrudan yöneticiye gider. Çevrimdışı uzlaştırmada aynı anahtarla gelen ama başka kullanıcı, izin, tutar, sebep ya da konu taşıyan eylem, eski kaydı devralmaz; 409 `IDEMPOTENCY_KEY_REUSED` döner.

## Owned surface

- `plan/v1/remediation/V1-RMD-670-grant-amount-and-approver-rules.md`
- `evidence/V1-RMD-670/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs - yalnız iptal talebinin tutarı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs - yalnız indirim talebinin tutarı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionStore.cs - yalnız onaylayan yetkisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/ApiErrorCatalog.cs - yalnız onaylayan yetkisi hatası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs - yalnız anahtar çakışması hatası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs - yalnız kısıtlıda çözücüleri atlama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs - yalnız tekrar eşleştirme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Grants/AuthorizationGrantServiceTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-668-role-management-audit-and-guard.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B009: iptal talebi tutarı = kalem brüt tutarı; yüzde indirimi tutarı = hesabın ödenecek tutarı × yüzde / 100 (kuruşa yuvarlanmış).
- B008: onaylayan/reddeden için talep edilen iznin sahipliği; tutar kademesi eklenmez (ilk yanıtlayan kazanır kuralı belgede böyle).
- B015: davranışsal kısıtlamada çözücüler çalışmaz.
- B013: çevrimdışı tekrar ve eşzamanlı kazanan kayıt, çevrimiçi yoldaki alan karşılaştırmasından geçer.

## Out of scope

- Hibe satırına yetki devri kaydı (delegation_id), onaylayan≠talep eden CHECK, değişmez alanlar tetikleyicisi ve yeni yabancı anahtarlar (B010, B017, B018: veritabanı göçü gerektirir; V1-RMD-673). Yetki devrinde kullanım anında devredenin aktifliği ve kümülatif tavan (ürün kararı belirsiz). Bekleyen eski iptal talepleri: tutar 0'dan gerçek tutara geçince aynı anahtarla yeniden gönderilen talep çakışma verir; bekleyen talepler geçicidir.

## Dependencies

- V1-RMD-668

## Acceptance evidence

1. Her kural için test; Yüksek bulgu (B009) için mutasyon kanıtı.
2. `evidence/V1-RMD-670/` altında çıktılar; Billing, VoidSent, AuthorizationDecision, OfflineReconciliation ve Grants testleri yeşil.

## Handoff

- V1-RMD-669
