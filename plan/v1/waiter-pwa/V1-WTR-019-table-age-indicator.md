# V1-WTR-019 - Masa yaşlanma göstergesi

- Task ID: V1-WTR-019
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünden
7. madde: dolu bir masanın adisyonu ne kadar süredir açık olduğunu (Toast/
Square/Lightspeed'in "table timer" özelliği) masa ızgarasında rozet olarak
göstermek — unutulmuş bir masayı garsonun fark etmesini kolaylaştırır.

**Yeni bir sütun yerine bir okuma-modeli:** `table_mgmt.tables`e yeni bir
zaman damgası eklemek yerine (V1-RMD-135'in `CurrentOrderTotal`i için
kurduğu ve bu görevin de izlediği kesin emsal), gerçek kaynak zaten
`orders.orders.created_at` — `t.current_order_id`'nin işaret ettiği
sipariş. Bu, şema değişikliği gerektirmediği için önceki iki görevin
(V1-WTR-015, V1-WTR-017) yaşadığı test-projesi blast-radius taramasını
tamamen ortadan kaldırdı — yalnızca `TableManagementStore`'un kendi
SQL'i ve `TableDto`'nun trailing (opsiyonel) bir alanı değişti, hiçbir
migration dosyası yok.

**Eşikler bilinçli olarak seçildi, sorulmadı:** 45/90 dakika, Katman C
maddelerinin (bahşiş havuzu gibi) aksine gerçek bir işletme-politikası
kararı değil — görsel bir varsayılan, yorum satırında gerekçelendirildi.
Sunucu yalnızca ham zaman damgasını döner; eşik/renk mantığı tamamen
istemci tarafında, ileride kolayca değiştirilebilir.

**İstemci tarafı:** `state.tables`'a `openedAt` eklendi; dolu masa
hücrelerinde durum rozetinin yanına `tag-age-normal/warning/danger`
sınıflı bir "N dk" rozeti. Rozet, `openedAt`'tan istemci tarafında
hesaplanıyor (sunucuya yeniden sorgu göndermeden) — dakikayı ilerletmek
için `renderTables()`'ı 60 saniyede bir çağıran ucuz bir zamanlayıcı
yeterli.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-019-table-age-indicator.md` (yeni)
- Sınırlı ek:
  - src/Host/Experience/Tables/TableManagementStore.cs,
    TableManagementContracts.cs, TableManagementApplication.cs (Tables/Host
    sahipliğinde) — `CurrentOrderOpenedAtSql`, `TableDto.CurrentOrderOpenedAt`,
    `ToDto`'nun yeni parametresi, iki uç noktanın tuple-destructuring
    güncellemesi.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js, waiter-app.css (V1-WTR-010
    sahipliğinde) — `openedAt` alanı, `tableAgeMinutes`/`tableAgeBadgeHtml`,
    rozet render'ı, 60sn'lik `renderTables` zamanlayıcısı, `.tag-age-*`
    sınıfları.
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs (Tables test
    sahipliğinde) — yeni test.

## Out of scope

- Eşik değerlerini yönetici tarafından yapılandırılabilir kılmak — şimdilik
  sabit bir istemci-tarafı varsayılan; gerçek bir işletme ihtiyacı ortaya
  çıkarsa ayrı bir görev (muhtemelen Settings modülüne bir ayar).
- Masa ızgarası dışında bir bildirim/uyarı (ör. push) — yalnızca görsel
  rozet, "yardım çağır" (V1-WTR-014) gibi bir sinyal mekanizması değil.

## Dependencies

- V1-RMD-135

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz; `waiter-app.css` parantez dengesi
  255/255.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj`
  → 12/12 (1 yeni test: sipariş yokken null, sipariş açıldığında
  `orders.orders.created_at`'a denk düşen bir zaman damgası döner).
- Şema değişikliği yok, dolayısıyla migration fixture taraması gerekmedi —
  bu, görevin kendi tasarım kararının (yeni sütun yerine okuma-modeli)
  doğrudan sonucu.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None
