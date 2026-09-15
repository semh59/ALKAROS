# V1-RMD-208 - "En uygun garson" önerisi artık bölgeyi de gözetiyor

- Task ID: V1-RMD-208
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle bulunan gerçek bir eksik: V1-RMD-202/204'ün "en uygun
garson" sorgusu yalnız yük ve rotasyona bakıyor, masanın hangi bölgede
(`table_mgmt.zones`) olduğunu ve garsonun o bölgeyle ilgisini hiç
bilmiyor. Bu görev bölgeyi sıralamaya yükten sonra, rotasyondan önce
üçüncü bir kademe olarak ekliyor — bir engel değil bir tercih: bölgesi
uymayan tek aday bile olsa yine önerilir, aksi halde bir bölgenin ilk
siparişinde kimse önerilmezdi.

Yalnız gerçek bir masaya bağlı çağrılarda (QR yolu, V1-RMD-202) etkili
olur. Cashier'ın kendi `/suggested-waiter` çağrısında gerçek bir masası
olmadığı için (`KASA-1`, V1-RMD-157) hiç masa geçmiyor — davranışı
değişmiyor, geriye dönük uyumlu.

## Owned surface

Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):

- src/Host/Experience/Orders/SuggestedWaiterResolver.cs (paylaşılan
  dosya) — `ResolveMostSuitableWaiterAsync`'e yeni isteğe bağlı
  `Guid? tableId` parametresi; doluysa masanın `zone_id`'si okunur ve
  her adayın en son (açık) siparişinin masasının bölgesiyle karşılaştırılır.
- src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
  (V1-RMD-149 sahipliğinde) — `AnnounceAsync` artık kendi
  `announcement.TableId`'sini geçiyor.
- src/Host/Experience/Orders/OrderManagementEndpoints.cs (Orders
  Management sahipliğinde) — `/suggested-waiter` değişmedi (Cashier'ın
  gerçek masası yok, `tableId` geçmiyor); imza uyumluluğu için tek
  satırlık güncelleme.
- tests/Host/Experience/PendingOrderNotifications/** (V1-RMD-202/203
  sahipliğinde) — yeni bölge senaryoları.

## In scope

1. `ResolveMostSuitableWaiterAsync(Guid? tableId, CancellationToken)`:
   `tableId` doluysa hedef bölge tek sorguda okunur; sıralama
   `aktif_yük ASC, aynı_bölgede_mi DESC, en_uzun_süredir_beklemede ASC`
   olur. `tableId` null'sa (Cashier) veya masanın `zone_id`'si null'sa
   davranış aynen eskisi gibi kalır (bölge kademesi devre dışı).
2. "Aynı bölgede mi": adayın en son (`created_at DESC LIMIT 1`) açık
   siparişinin masasının `zone_id`'si hedef `zone_id`'ye eşit mi —
   hiç siparişi olmayan/masası olmayan bir aday için `false` (öncelik
   kaybeder ama tamamen elenmez).

## Out of scope

- Kalıcı garson-bölge ataması (bir garson bir bölgeye önceden atanır) —
  hâlâ bilinçli kapsam dışı, bu görev yalnız son davranışa (en son nerede
  sipariş aldı) bakıyor.
- Cashier/PosTerminal'e bölge seçimi eklemek — ikisinin de gerçek bir
  masası yok.

## Dependencies

- V1-RMD-202
- V1-RMD-204

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/PendingOrderNotifications`
  → tüm testler yeşil (yeni: aynı yükte iki aday varken aynı bölgede son
  siparişi olan öne geçiyor; tek aday farklı bölgedeyse yine de o
  öneriliyor — bölge bir engel değil); revert-and-confirm ile en az bir
  yeni test gerçekten kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: iki garson eşit yükle, biri
  bahçe bölgesindeki bir masaya en son bakan, diğeri iç mekânda — bahçe
  bölgesindeki bir masadan QR sipariş gelince bildirim bahçedeki
  garsona gider.

## Handoff

- None
