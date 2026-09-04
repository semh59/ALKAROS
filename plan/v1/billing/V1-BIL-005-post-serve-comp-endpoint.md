# V1-BIL-005 - Post-serve item complimentary (comp) endpoint

- Task ID: V1-BIL-005
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`ItemExceptionHandler.ApplyComplimentaryAsync` (`Orders/ItemExceptions/**`)
zaten tam çalışır durumda — teslim edilmiş bir kalemi sıfır fiyatlar, sebep
kataloğu ve denetim kaydı ile, `KitchenState`'e bakmaksızın. Ama hiçbir HTTP
endpoint'i onu çağırmıyor. Bu görev onu, `bills.comp` grant-class iznine
bağlı gerçek bir uç noktaya bağlar: çağıran doğrudan izne sahip değilse
`IAuthorizationGrantService.RequestAsync` ile `pending` döner; onay sonrası
aynı idempotency key ile tamamlanır.

## Owned surface

- `plan/v1/billing/V1-BIL-005-post-serve-comp-endpoint.md`
- `tests/Host/Experience/Orders/Comp/**` (yeni proje — üst dizinde
  `V1-RMD-083`/`V1-ORD-005`'in sahip olduğu yollarla çakışmıyor)
- `evidence/V1-BIL-005/**`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Host/Experience/Orders/OrderManagementContracts.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `ApplyComplimentaryRequestV1`/
  `ApplyComplimentaryResultV1` kayıtları; mevcut hiçbir kayıt değişmedi.
  `src/Host/Experience/Orders/OrderManagementEndpoints.cs` (`V1-IAM-024`
  sahipliğinde kalır) — yeni `POST .../items/{itemId}/comp` uç noktası;
  `AddOrderManagementExperience`'a grant-akışının tüm bağımlılıkları
  eklendi (`IAuthorizationGrantService`, `IAuthorizationGrantRepository`,
  `IAuthorizationPolicyRepository`, `IAuthorizationDelegationRepository`,
  `IEscalationResolver`→`DelegationEscalationResolver`,
  `IBehaviouralTighteningRepository`, `IBehaviouralRateSource`,
  `IPrePolicyGate`→`BehaviouralTighteningGate`) — hepsi V1-IAM-025'te zaten
  var olan, hiç HTTP çağrısı almayan bileşenler; mevcut hiçbir kayıt/uç
  nokta değişmedi.

## In scope

- Yeni bir Experience endpoint'i,
  `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/comp`:
  çağıranın `bills.comp` iznini DB'deki rol atamasından (`IRoleRepository
  .GetPermissionCodesForUserAsync`) doğrudan tutup tutmadığına bakar; tutuyorsa
  (`cashier`/`supervisor`/`manager` tipik olarak) doğrudan
  `ItemExceptionHandler.ApplyComplimentaryAsync`'i çağırır. Tutmuyorsa
  (`waiter` tipik olarak) `IAuthorizationGrantService.RequestAsync`'e sarar:
  sonuç `Refused` → 403, `Pending` → 202 (`GrantId` ile), `Authorized` →
  aynı istekte doğrudan `ApplyComplimentaryAsync`'e devam eder.
- `IdempotencyKey` istemciden gelir ve `GrantRequest.IdempotencyKey`'e
  geçirilir — bir yönetici `pending` grant'ı onayladıktan sonra istemci AYNI
  gövdeyle yeniden POST atarsa, `RequestAsync` idempotency anahtarıyla
  eşleşen (artık `Granted`) satırı bulur ve uç nokta doğrudan uygular.
- `docs/engineering/authz-wave-remediation-plan.md`'nin eski C1/C5'i burada
  gerçekleşir: bu, gerçek bir `RequestAsync` HTTP yoluna sahip ilk endpoint.
  Entegrasyon testleri hem `DelegationEscalationResolver`'ın aktif bir
  delegasyonla grant'ı doğrudan çözdüğünü, hem de `BehaviouralTighteningGate`
  'in açık bir "tightening" varken `always_allow` bir politikayı bile
  zorla `pending`'e düşürdüğünü kanıtlıyor — ikisi de üretimde daha önce hiç
  tetiklenmemiş, sadece birim testli bileşenlerdi.
- **Bilinen, disclosure'lı sınır:** model §3'ün "own check" kuralı (bir
  garson yalnız kendi servis ettiği çeki comp'layabilir) uygulanamıyor —
  `V1-WTR-009`'un da belgelediği gibi, sistemde hiçbir yerde garson/sipariş
  servis-atama modeli yok. Uç nokta `SubjectServingUserId: null` geçiyor;
  `AuthorizationGrantService`'in own-check muhafazası (`SubjectServingUserId
  is { } serving`) bu durumda hiç tetiklenmiyor, yani kural şu an sunucu
  tarafında hiç kısıtlama yapmıyor. Sessizce atlanmadı — bir servis-atama
  modeli var olduğunda gerçek kısıtlamayı eklemek gelecek iş olarak kalır.

## Out of scope

- Void (`V1-ORD-005`, `V1-IAM-027`).
- Discount — zaten `Billing.Adjustments`'ın `CreateDiscountPercentage`/
  `CreateDiscountAmount`'ı üzerinden ayrı bir akış; bu görev yalnız comp'u
  kapsar (aynı desen daha sonra discount'a da uygulanabilir, ayrı görev).
- Own-check'in gerçek uygulanması (yukarıdaki disclosure'lı sınıra bakın) —
  bir garson/sipariş servis-atama modeli gerektirir, o modelin kendisi
  hiçbir mevcut görevin kapsamında değil.

## Dependencies

- V1-IAM-025

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 hata.
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Host.Experience.Orders.Comp.Tests` 7/7 (yeni proje — oturumsuz
  istek 401; geçersiz sebep kodu 400; `bills.comp`'ı doğrudan tutan rol
  200 ile doğrudan uygular; hiçbir politika/delegasyon yokken 202 `Pending`
  ile eskalasyon; aktif bir delegasyon grant'ı `DelegationEscalationResolver`
  üzerinden çözüp 200 ile doğrudan uygular; `always_allow` bir politika olsa
  bile açık bir `behavioural_tightening` zorla 202 `Pending`'e düşürür;
  `pending` bir grant onaylandıktan sonra aynı idempotency key ile ikinci
  istek 200 ile tamamlanır). `ALKAROS.Identity.Authorization.Tests` 185/185,
  `ALKAROS.Orders.OrderAggregate.Tests` 97/97,
  `ALKAROS.Orders.ItemExceptions.Tests` 22/22,
  `ALKAROS.Host.Experience.Orders.Void.Tests` 5/5,
  `ALKAROS.Host.Experience.Composition.Tests` 4/4,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz.
- `python tools/consistency-audit/consistency_audit.py`: temiz.

## Handoff

- V1-IAM-027
