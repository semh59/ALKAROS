# V12-RMD-007 - Webhook kimlik doğrulama sırası ve müşteri notunun korunması

- Task ID: V12-RMD-007
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin webhook (V12-ONL-001) ve müşteri verisi bulgularını kapatmak (Semih:
"en küçük hata bile kritik"):

- Webhook ucu her istek için kimlik doğrulamadan önce 256 KB'lık bir tampon ayırıp gövdeyi okuyor. Kimliği
  doğrulanmamış istekler de bellek ve bant tüketiyor.
- `Authorization` başlığının tamamı gizli değer olarak karşılaştırılıyor ve şema (`Bearer`/`Basic`) büyük/küçük
  harfe duyarlı. RFC 7235'e göre şema duyarsızdır. Beklenen biçim hiçbir yerde belgelenmemiş; tek bir yanlış ayar
  her teslimatı 401 yapar.
- Sipariş düzeyi müşteri notu (sık sık telefon/adres içerir) `orders.notes`'a şifresiz yazılıyor (KVKK). Semih
  2026-09-26 kararı: "Mutfağa gitmesin, şifreli kalsın". Not yalnız şifreli ham kayıtta kalır; online sipariş
  ekranında yetkili personel bir düğmeyle görebilir ve her görüntüleme denetim kaydına yazılır.

## Owned surface

- `plan/v1.2/online-ordering/V12-RMD-007-webhook-auth-and-customer-note.md`
- `evidence/V12-RMD-007/**`
- `database/migrations/V12/V12-RMD-007/**`
- `docs/operations/yemeksepeti-webhook-setup.md` — gizli değerin biçimi; bu görevle oluşturulan yeni dosya.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ (V12-ONL-001) — ayrı kimlik doğrulama adımı, şemaya
    duyarsız karşılaştırma, notun şifreli kayıttan okunması.
  - src/Host/Experience/OnlineOrdering/YemeksepetiWebhookEndpoints.cs (V12-ONL-001) — gövdeden önce kimlik
    doğrulama.
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs (V12-ONL-002) — not artık
    `orders.notes`'a yazılmaz.
  - src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs (V12-OUI-001) — denetimli not görüntüleme ucu.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — "Müşteri notu" düğmesi.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 151 numaralı migration konumu.
  - tests/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ ve tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. `YemeksepetiWebhookInbox.Authenticate(header)`. Uç, gövdeyi okumadan önce bunu çağırır. Kimliği doğrulanmamış
   istek, gövdesi ne kadar büyük olursa olsun 401 alır ve gövdesi okunmaz. `ReceiveAsync` yine de aynı kontrolü
   yapar.
2. Karşılaştırma şemaya duyarsızdır; kimlik bilgisi sabit zamanlı karşılaştırılır. Biçim
   `docs/operations/yemeksepeti-webhook-setup.md`'de belgelenir (doğrulanmamış taslak).
3. Sipariş notu yalnız `Yemeksepeti {görünen kod}` olur; müşteri notu yazılmaz. Migration 151 mevcut online
   siparişlerin notlarındaki müşteri notunu siler. Notun aslı şifreli ham kayıtta durduğu için bilgi kaybolmaz; down
   betiği silineni geri getirmez ve bunu açıkça söyler.
4. `GET /api/v1/terminals/{terminalId}/online-operations/orders/{orderId}/customer-note` (`orders.create`). Not
   şifreli kayıttan açılır, her görüntüleme `audit.audit_events`'e `Order.CustomerNoteViewed` olarak yazılır. Ekranda
   "Müşteri notu" düğmesi notu ya da "Müşteri notu yok" metnini gösterir.

## Out of scope

- Mutfak talimatı olan kalem notları (V12-RMD-004; mutfağa gider).

## Dependencies

- V12-RMD-004

## Deliverables

- Kod, migration 151 (up/down), doküman, testler.

## Acceptance evidence

- İlgili test projeleri yeşil; mutasyon kontrolü `evidence/V12-RMD-007/` altında.
- `task_scope_tool.py --task-id V12-RMD-007 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
