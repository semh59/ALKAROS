# V1-RMD-164 - Başarısız gönderim sonrası düzeltilen miktar kayboluyordu

- Task ID: V1-RMD-164
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki tek
**High** bulguyu kapatır: "başarısız gönderimden sonra miktar düzeltilirse
mutfağa eski miktar gidiyor ve ekran gönderilmiş gibi temizleniyor (sunucu,
kalıcı bir kalem kimliğini değiştirilemez sayıyor)".

**Kök neden, sunucuda:** `table-draft`'ın "ikinci tur" birleştirme mantığı
(`OrderManagementStore.CreateOrUpdateTableDraftAsync`), gelen bir kalemin
`id`'si zaten siparişte varsa onu **saf bir tekrar** sayıp tamamen atıyordu
— içeriği (miktar, not, eklentiler) hiç karşılaştırmadan. Bu, aynı içerikli
gerçek bir tekrar (çevrimdışı kuyruğun kaybolan bir yanıtı yeniden
denemesi) için doğruydu, ama denetimin sahnesinde yanlıştı: `table-draft`
başarılı olup (kalem artık var, Draft, mutfağa hiç gitmemiş) `submit-draft`
başarısız olursa, garson AYNI satırın miktarını düzeltip tüm taslağı tekrar
gönderiyor — sunucu kimliği zaten tanıdığı için düzeltmeyi sessizce atıyor,
eski miktar veritabanında sonsuza dek kalıyor. Mutfak yanlış miktar
alıyordu, ekran ise turu "gönderildi" diye temizliyordu.

**Düzeltme:** `Order.AddRound`'un yanına yeni `Order.ReconcileRound`
eklendi — bilinmeyen bir kimlik hâlâ eklenir, ama bilinen bir kimlik artık
**içerik farklıysa ve mutfağa hiç gönderilmemişse** (`KitchenState.NotSent`)
gelen versiyonla DEĞİŞTİRİLİR; mutfağa zaten gitmiş bir kalem eskisi gibi
dokunulmaz kalır (o yol hâlâ yalnız void/void-sent'e ait).
`OrderManagementStore`'daki "saf tekrar → veritabanına hiç yazma" kısayolu
korundu ama artık "kimlik bilinen" yerine "içerik gerçekten aynı" temelinde
çalışıyor (yeni `ItemContentUnchanged` — miktar, not, eklenti kümesi
karşılaştırması), böylece gerçek bir tekrarda hâlâ gereksiz `row_version`
artışı olmuyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-164-frontend-high-quantity-correction-lost.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Modules/Orders/OrderAggregate/Order.cs (V1-RMD-064 sahipliğinde)
    — yeni ReconcileRound metodu.
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-RMD-147
    sahipliğinde) — birleştirme mantığı ReconcileRound'a geçti, yeni
    ItemContentUnchanged yardımcı metodu.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (V1-ORD-006 sahipliğinde) — yeni test.

## Out of scope

Frontend bölümünün kalan bulguları (Medium 15 + Low 12) — ayrı görev/görevler.

## Dependencies

- V1-RMD-163

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı:
  - `tests/Modules/Orders/OrderAggregate` — 120/120 yeşil.
  - `tests/Modules/Orders/SubmitOrder` — 16/16 yeşil.
  - `tests/Host/Experience/Orders/TableDraft` — **49/49 yeşil** (48
    mevcut + 1 yeni: `ResendingTheSameLineWithACorrectedQuantityUpdatesTheStoredItem`
    — aynı satırı düzeltilmiş miktarla tekrar gönderme, saklanan kalemin
    güncellendiğini doğruluyor; ardından AYNI düzeltilmiş içerikle üçüncü
    bir gönderim, `row_version`'ın artmadığını (saf tekrar yolu hâlâ
    çalışıyor) doğruluyor).
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,VoidSent}` —
    toplam 46/46 yeşil.
- Yeni testin **vacuous olmadığı kanıtlandı**: `git stash` ile
  `Order.cs`/`OrderManagementStore.cs` değişiklikleri geri alınıp yalnız o
  test çalıştırıldı → gerçekten düştü (`Expected: 3, Actual: 2,000` — eski
  miktar gerçekten kalıcı olarak saklanmış görüldü). Değişiklikler geri
  yüklendi, tekrar 49/49 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
