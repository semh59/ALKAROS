# V1-ORD-005 - Pre-send item void endpoint

- Task ID: V1-ORD-005
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`ItemExceptionHandler.VoidItemAsync` (`Orders/ItemExceptions/**`) zaten tam
çalışır durumda — henüz mutfağa gönderilmemiş (`KitchenState = NotSent`) bir
kalemi iptal eder, sebep kataloğu ve denetim kaydı ile. Ama hiçbir HTTP
endpoint'i onu çağırmıyor: bu servis bugün ölü koddur. Bu görev, mevcut
domain mantığına dokunmadan, onu gerçek bir uç noktaya bağlar. Bu, `V1-SET-002`
/ `V1-KIT-005`'e bağımlı değildir — anahtar kapalıyken de (bugünkü tek
gerçekleşen durum) çalışır.

## Owned surface

- `plan/v1/orders/V1-ORD-005-pre-send-void-endpoint.md`
- `tests/Host/Experience/Orders/Void/**` (2026-09-04 kararıyla
  `V1-RMD-083`'ten devralındı — bkz. o görevin Owned surface notu; üst
  dizin tests/Host/Experience/Orders/** genel olarak V1-RMD-083
  sahipliğinde kalmaya devam eder)
- `evidence/V1-ORD-005/**`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Host/Experience/Orders/OrderManagementContracts.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `VoidOrderItemRequestV1`/`VoidOrderItemResultV1`
  kayıtları; mevcut hiçbir kayıt değişmedi.
  `src/Host/Experience/Orders/OrderManagementEndpoints.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `POST .../items/{itemId}/void` uç noktası;
  `AddOrderManagementExperience`'a `ItemExceptionHandler` kaydı eklendi;
  `RequireCashierPermissionAsync`'in dönüş tipi `Task`'tan `Task<Guid>`'e
  değişti (çağıran taraflar etkilenmedi, sadece yeni uç nokta değeri
  kullanıyor). Ayrıca aynı dosyada iki gerçek, önceden var olan kusur
  giderildi (bkz. In scope) — üçüncü madde.

## In scope

- `src/Host/Experience/Orders/OrderManagementEndpoints.cs`'e
  `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/void`
  eklemek: `orders.create` izniyle (model §2: "unsent items need only
  orders.create"), `ItemExceptionHandler.VoidItemAsync`'i çağırır.
- `VoidReasonCatalog`'daki dört sebepten birini isteyen bir sözleşme
  (`InvalidItemReasonException` → 400 `VALIDATION_FAILED`).
  `IsManagerAuthorized` alanı `ItemExceptionHandler`'ın kendi eski
  varsayımıydı; uç nokta her zaman `true` geçiyor çünkü `orders.create`
  granüler izninin kendisi zaten yetki kontrolü — ayrı bir "manager"
  kavramı granüler modelde yok (V1-IAM-017/018).
- **Bulunan ve giderilen kusurlar (aynı dosyada, kapsam dahilinde):** yeni
  uç noktayı gerçek bir HTTP testiyle doğrularken iki önceden var olan
  kusur ortaya çıktı; ikisi de bu görevden önce grubun HER mevcut uç
  noktasını (`table-draft`, `table/{id}`, `{orderId}`, `submit`) etkiliyordu,
  sadece hiç HTTP seviyesinde test edilmemişlerdi (mevcut
  `OrderManagementExperienceTests.cs`'nin hiç `.csproj`'u yok, hiç
  çalışmamış — bkz. `V1-RMD-083` Owned surface notu). (1)
  `AddOrderManagementExperience`, her uç noktanın aldığı `DualScreenStore`'u
  hiç kaydetmiyordu (yalnız tam Host kompozisyonu içinde çalıştığında
  kazara başka bir modülün kaydı üzerinden çalışıyordu); bağımsız bir host
  minimal API parametre çıkarımını başaramıyor, route inşası
  `InvalidOperationException: Failure to infer one or more parameters` ile
  çöküyordu. Düzeltme: `services.TryAddSingleton<DualScreenStore>();`. (2)
  Grup, `DualScreenUnauthorizedException`'ı (oturumsuz istek) yakalayan
  hiçbir filtre içermiyordu — Billing/Catalog/Kitchen/Tables/Authorization
  modüllerinde olan `XxxExceptionFilter` deseni Orders'ta hiç yoktu; sonuç
  401 yerine çıplak 500'dü. Düzeltme: aynı desende yeni
  `OrderManagementExceptionFilter` (`DualScreenUnauthorizedException`→401,
  `AuthorizationDeniedException`→403, artı mevcut domain istisnaları için
  aynı kod/mesajları üreten bir güvenlik ağı — mevcut uç noktaların kendi
  satır-içi `catch`'leri hâlâ önce çalışır, davranışları değişmedi).

## Out of scope

- Gönderildi-ama-servis-edilmedi void (`V1-IAM-027`).
- Comp (`V1-BIL-005`).
- `OrderManagementExperienceTests.cs`'nin derlenmemesi (`V1-RMD-083`
  sahipliğinde kalan ayrı, önceden var olan bir kusur).

## Dependencies

- V1-IAM-024

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 hata.
- `python tools/project-manifest/project_manifest_tool.py`: VALID (yeni
  proje referansı ve yeni test projesi tutarlı).
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Host.Experience.Orders.Void.Tests` 5/5 (yeni proje — oturumsuz
  istek 401, `orders.create` olmayan kullanıcı 403, geçersiz sebep kodu 400,
  henüz gönderilmemiş kalem başarıyla iptal edilir (200, satır sürümü 1→2),
  zaten mutfağa gönderilmiş kalem (`KitchenState.Preparing`) 409
  `ALREADY_SENT`). `ALKAROS.Orders.OrderAggregate.Tests` 97/97,
  `ALKAROS.Orders.ItemExceptions.Tests` 22/22,
  `ALKAROS.Orders.SubmitOrder.Tests` 15/15,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz.
- `python tools/consistency-audit/consistency_audit.py`: temiz.

## Handoff

- V1-IAM-027
