# V1-RMD-161 - Eklenti seçim kurallarının sunucuda zorlanması

- Task ID: V1-RMD-161
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin API uç noktaları
bölümünden bir bulguyu kapatır: "Eklenti seçim kuralları sunucuda hiç
zorlanmıyor (adet istemci kontrolünde bir fiyat girdisi)".

`catalog.modifier_groups.min_selections`/`max_selections` (ör. "tam bir
boyut seç", "en fazla iki ekstra") hiçbir zaman kontrol edilmiyordu.
`OrderManagementStore.ResolveModifiersAsync` yalnızca seçilen her
`modifierId`'nin ürüne ait ve aktif olduğunu doğruluyor, ismini/fiyatını
katalogdan çözüyordu — grup kuralına hiç bakmıyordu. Bir istemci zorunlu
bir gruptan hiç seçim yapmadan veya bir grubun izin verdiğinden fazla
seçerek gönderse, sunucu sessizce kabul ediyordu.

Düzeltme:

- `ResolveModifiersAsync`'in sonuç sözlüğüne artık her eklentinin
  `modifier_group_id`'si de dahil.
- Yeni `ResolveApplicableModifierGroupsAsync`: bir siparişteki ürünlere ait
  TÜM eklenti gruplarını (doğrudan `catalog.modifiers.product_id` üzerinden
  ya da `catalog.product_modifier_groups` üzerinden), min/max kurallarıyla
  birlikte tek bir toplu sorguda getiriyor — bu, hiç seçilmemiş zorunlu bir
  grubu da görebilmek için gerekli (yalnız seçilenlerden yola çıkan bir
  sorgu bunu asla göremez).
- Yeni `ValidateModifierGroupSelections`: her kalemin gerçek seçimlerini
  grup başına sayıp ürünün her uygulanabilir grubunun min/max'ına göre
  doğruluyor; ihlal `ArgumentOutOfRangeException` fırlatıyor (var olan
  miktar doğrulamasıyla aynı desen) — bu zaten `ArgumentException`
  üzerinden 400 `VALIDATION_FAILED`'e eşleniyor, ayrı bir eşleme
  gerekmedi.

## Owned surface

- `plan/v1/remediation/V1-RMD-161-modifier-selection-rules.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-RMD-147
    sahipliğinde) — ResolveModifiersAsync'e GroupId eklendi;
    ResolveApplicableModifierGroupsAsync ve
    ValidateModifierGroupSelections eklendi; ana akışa bir çağrı eklendi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
    (V1-ORD-006 sahipliğinde) — SeedModifierAsync'e opsiyonel min/max
    parametreleri; yeni SeedModifierGroupWithTwoOptionsAsync helper'ı.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (V1-ORD-006 sahipliğinde) — üç yeni test.

## Out of scope

API uç noktaları bölümünün kalan beş bulgusu (ayrı görev/görevler):
- Miktar/kalem sayısı/not uzunluğu sınırları.
- `PostgresException` → 503 eşlemesinin genişletilmesi.
- Masa yönetimi/push uç noktalarında hız sınırı; `/pending`'in kovası.
- Katalog sayfalama imleci.
- `X-Idempotency-Key` başlığının okunması.
- `/comp` ve `/transfer-server`'ın istemcisi.

## Dependencies

- V1-RMD-160

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı:
  - `tests/Host/Experience/Orders/TableDraft` — **45/45 yeşil** (42
    mevcut + 3 yeni: zorunlu grup boş seçim reddi, max aşımı reddi, kural
    içi geçerli seçim kabulü). Mevcut testlerin hiçbiri bozulmadı çünkü
    `SeedModifierAsync`'in varsayılan min/max'ı (0, 5) hâlâ serbest.
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,VoidSent}` —
    toplam 46/46 yeşil.
- Yeni iki negatif testin **vacuous olmadığı kanıtlandı**: `git stash` ile
  `OrderManagementStore.cs` değişikliği geri alınıp yalnız o iki test
  çalıştırıldı → ikisi de gerçekten düştü (`Expected: BadRequest, Actual:
  OK`). Değişiklik geri yüklendi, tekrar 45/45 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
