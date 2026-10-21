# V1-RMD-678 - Gönderilmemiş sipariş turları kendi garsonuna bağlı kalır

- Task ID: V1-RMD-678
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-671 sonrası açık kalan riski kapatır (Ürün Sahibi kararı, 2026-10-21): cihazda bekleyen bir sipariş turu, onu kuyruğa alan garsonun kimliğiyle etiketlenir; başka bir garson aynı cihazda giriş yapınca o tur silinmez ama o garsonun adına da gönderilmez; yalnız sahibi yeniden giriş yapınca gönderilir. Etiketi olmayan eski turlar eskisi gibi gönderilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-678-pending-rounds-belong-to-their-waiter.md`
- `evidence/V1-RMD-678/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/offline-queue.js - yalnız tur sahibi etiketi ve gönderim süzgeci
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/offline-queue.test.js - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-677-authorization-limits-screen.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kuyruğa alınırken `ownerUserId` etiketi; gönderimde ve yeniden deneme zamanlayıcısında yalnız oturumdaki kullanıcının (ya da etiketsiz) turları.

## Out of scope

- Başka kullanıcının bekleyen turunu ekranda gösterme ya da yönetici devri; tarayıcıda elle deneme (doğrulanamadı).

## Dependencies

- V1-RMD-677

## Acceptance evidence

1. Her kural için vitest testi; mutasyon kanıtı.
2. `evidence/V1-RMD-678/` altında çıktılar; StaticApps testleri yeşil.

## Handoff

- V1-RMD-679
