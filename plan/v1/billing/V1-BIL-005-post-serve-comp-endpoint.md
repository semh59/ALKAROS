# V1-BIL-005 - Post-serve item complimentary (comp) endpoint

- Task ID: V1-BIL-005
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`ItemExceptionHandler.ApplyComplimentaryAsync` (`Orders/ItemExceptions/**`)
zaten tam çalışır durumda — teslim edilmiş bir kalemi sıfır fiyatlar, sebep
kataloğu ve denetim kaydı ile, `KitchenState`'e bakmaksızın. Ama hiçbir HTTP
endpoint'i onu çağırmıyor. Bu görev onu, `bills.comp` grant-class iznine
bağlı gerçek bir uç noktaya bağlar: çağıran doğrudan izne sahip değilse (waiter
rolü, kendi çeki değilse otomatik red — model §3 "own check") `IAuthorizationGrantService.RequestAsync`
ile `pending` döner; onay sonrası aynı idempotency key ile tamamlanır.

## Owned surface

- `plan/v1/billing/V1-BIL-005-post-serve-comp-endpoint.md`
- `evidence/V1-BIL-005/**`
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- Yeni bir Experience endpoint'i (muhtemelen
  `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/comp`):
  çağıranın `bills.comp` izni var mı kontrol eder; yoksa
  `IAuthorizationGrantService.RequestAsync` ile grant ister
  (`SubjectServingUserId` = order'ın serving user'ı — own-check kuralı için);
  varsa veya onaylandıysa `ItemExceptionHandler.ApplyComplimentaryAsync`'i
  çağırır.
- `docs/engineering/authz-wave-remediation-plan.md`'nin C1'i burada
  gerçekleşir: bu, gerçek bir `RequestAsync` HTTP yoluna sahip ilk endpoint
  olur; `BehaviouralTighteningGate` ve `DelegationEscalationResolver`'ın bu
  yolda tetiklendiğini kanıtlayan entegrasyon testleri (eski C5).

## Out of scope

- Void (`V1-ORD-005`, `V1-IAM-027`).
- Discount — zaten `Billing.Adjustments`'ın `CreateDiscountPercentage`/
  `CreateDiscountAmount`'ı üzerinden ayrı bir akış; bu görev yalnız comp'u
  kapsar (aynı desen daha sonra discount'a da uygulanabilir, ayrı görev).

## Dependencies

- V1-IAM-025

## Blocker

- Endpoint'in hangi Experience modülüne (yeni bir tane mi, yoksa
  `OrderManagementEndpoints.cs`'e mi) ekleneceği ve `ItemExceptions/**`
  (sahip: `V1-ORD-004`) için custody devri implementasyon başlarken netleşir.
  Ancak bu eklenip `validate` temiz kaldığında görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-IAM-027
