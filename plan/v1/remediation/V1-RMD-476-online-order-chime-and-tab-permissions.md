# V1-RMD-476 - Yeni online sipariş sesi her ekranda ve online sekme yetkilerini hizalamak

- Task ID: V1-RMD-476
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Yeni QR ya da online sipariş sesi yalnız Siparişler sekmesi açıkken çalıyor; kasada satış yaparken gelen sipariş sessiz kalıyor ve platformun kabul süresi işliyor.
`orders.create` yetkili oturumda, Siparişler sekmesi dışındaki her ekranda, sıra listesi 20 saniyede bir okunur ve ilk okumadan sonra gelen yeni bir sipariş için ses çalınır
(Siparişler sekmesi kendi sesini çalmaya devam eder, çift ses olmaz). İki yetki uyuşmazlığı da düzelir: Siparişler sekmesi `orders.create` ister (bugün yalnız `tables.status`
olan oturum adresi elle açınca 403 alan boş sekme görür) ve Sorunlar sekmesi listeyi okuyan `reports.view` yetkisini ister (bugün yalnız `reconciliation.manage` olan oturum sekmeyi görür, liste 403 verir).

## Owned surface

- `plan/v1/remediation/V1-RMD-476-online-order-chime-and-tab-permissions.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/useNewOrderChime.ts - yeni dosya: sırayı okuyup yeni sipariş sesini çalan kanca
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/useNewOrderChime.test.tsx - yeni dosya: kanca testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız kancanın bağlanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx - yalnız yeni davranışın testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-hub/onlineHubApi.ts - yalnız sekme yetkileri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.test.tsx - yalnız sekme yetki beklentileri

## In scope

- Kanca: yetkiliyse ve Siparişler sekmesinde değilse `loadOnlineOperations(terminalId, "all")` ile 20 saniyede bir okur; ilk okuma temel alınır, sonraki yeni sipariş kimliği için `playNewItemChime`.
- Yetki tablosu değişikliği ve `workspace.tsx` rota kapısının ona uyması.
- Testler: ses yalnız yeni sipariş için ve yalnız Siparişler sekmesi dışında çalar, yetkisiz oturum hiç istek atmaz, sekme yetkileri.

## Out of scope

- Sesin tarayıcı otomatik oynatma kuralı (kullanıcı etkileşimi sonrası çalışır); görsel bildirim; sunucu yetkileri.

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi (Siparişler dışındaki ekranda yeni sipariş sesi tetiklenir); çıktılar `evidence/V1-RMD-476/` altındadır.

## Handoff

- None
