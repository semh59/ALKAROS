# V1-RMD-204 - Masa açılışında garson atama (backend + öneri ucu)

- Task ID: V1-RMD-204
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteği: "kasiyer/garson elle masa açarken en uygun garson
önerisi" — tespit edilen son gerçek boşluk. `TableDraftService
.CreateOrUpdateTableDraftAsync` bir masa açıldığında `Order.ServingUserId`
her zaman `actingUserId` (masayı kim açtıysa) oluyordu — kasiyer Cashier'dan
bir masa açarsa, garsonu değil kendini servis eden yapıyordu (V1-RMD-111'in
bilinçli tasarımı, ama garson atama diye bir seçenek hiç yoktu). Bu görev
sunucu tarafını kurar: (1) V1-RMD-202'nin "en uygun garson" sorgusunu
paylaşılan bir servise çıkarır, (2) yeni bir öneri ucu ekler, (3)
`/table-draft`'a açıkça "bu masayı şu garsona ata" seçeneği ekler —
V1-RMD-111'in `orders.transfer-server-any` iki-katmanlı izin modeliyle
aynı desende (kendi masanı kendine atamak serbest, başkasına atamak
kasiyer/amir izni ister).

İstemci (Cashier) kablolaması ayrı görev (V1-RMD-205) — bu görev yalnız
sunucu tarafı, uç UI'sız teslim edilebilir (V1-RMD-102/110/177'nin aynı
emsali).

## Owned surface

- `src/Host/Experience/Orders/SuggestedWaiterResolver.cs` (yeni) —
  V1-RMD-202'nin `SignalRPendingOrderAnnouncer.ResolveMostSuitableWaiterAsync`
  sorgusunun birebir aynısı, artık paylaşılan bir servis; görüntü adı da
  döndürüyor (`SuggestedWaiterV1(UserId, DisplayName)`).
- Sınırlı ek (yollar geri-tik olmadan, V1-RMD-111 emsali):
  - src/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncer.cs
    (V1-RMD-149 sahipliğinde) — artık kendi sorgusu yerine
    SuggestedWaiterResolver'ı kullanıyor; davranış aynı, kod tekilleşti.
  - src/Host/Experience/Orders/OrderManagementContracts.cs (Orders
    Management sahipliğinde) — `CreateTableDraftRequest`'e yeni
    `AssignedWaiterUserId` (nullable), yeni `SuggestedWaiterV1` DTO.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (Orders
    Management sahipliğinde) — `/table-draft` artık `AssignedWaiterUserId`
    farklıysa `orders.transfer-server-any` istiyor ve onu ServingUserId
    yapıyor; yeni `GET .../orders/suggested-waiter`.
  - tests/Host/Experience/Orders/TableDraft/** (ilgili görev sahipliğinde)
    — yeni atama/izin senaryoları.
  - tests/Host/Experience/PendingOrderNotifications/** (V1-RMD-202/203
    sahipliğinde) — `SignalRPendingOrderAnnouncer`'ın yeni bağımlılığına
    göre güncellenen kurulum, davranış testleri değişmedi.

## In scope

1. `SuggestedWaiterResolver.ResolveMostSuitableWaiterAsync`: V1-RMD-202'nin
   birebir aynı sorgusu (orders.send + açık oturum + en az yük + rotasyon
   tiebreak), artık görüntü adıyla birlikte.
2. `POST .../orders/table-draft`: `request.AssignedWaiterUserId` doluysa ve
   `actingUserId`'den farklıysa `orders.transfer-server-any` yetkisi
   istenir (V1-RMD-111 ile aynı iki-katmanlı model); o zaman ServingUserId
   atanan kişi olur. Boşsa veya kendisiyse davranış aynen eskisi gibi.
3. `GET .../orders/suggested-waiter`: sistemin önerisini (varsa) döndürür —
   Cashier'ın picker'ı bunu varsayılan olarak gösterecek.

## Out of scope

- Cashier/PosTerminal istemci kablolaması (picker UI) — V1-RMD-205.
- PosTerminal `/table-draft` hiç çağırmıyor, bu görevin kapsamı dışında.
- WaiterPwa'da herhangi bir değişiklik — kendi masasını açan garson zaten
  kendisi, öneriye ihtiyaç yok.

## Dependencies

- V1-RMD-202
- V1-RMD-203

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı: `tests/Host/Experience/Orders/TableDraft`,
  `tests/Host/Experience/PendingOrderNotifications` tüm testler yeşil
  (yeni atama/izin/öneri senaryoları dahil); revert-and-confirm ile en az
  bir yeni test gerçekten kırılıp doğrulanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: kasiyer oturumuyla
  `GET .../orders/suggested-waiter` çağır, dönen garsonun id'sini
  `AssignedWaiterUserId` olarak `/table-draft`'a gönder — yeni siparişin
  `ServingUserId`'si o garson olur; `orders.transfer-server-any` taşımayan
  bir oturumla başka birine atamayı dene — 403.

## Handoff

- None
