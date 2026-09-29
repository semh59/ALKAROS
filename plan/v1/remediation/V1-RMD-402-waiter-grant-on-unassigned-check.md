# V1-RMD-402 - Garsonun sahipsiz hesaptaki iptal/ikram isteğini otomatik reddetmek

- Task ID: V1-RMD-402
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi (H-07): model §3 karar 1'e göre garson iptal/ikram isteğini yalnız kendi hesabında açabilir;
başka garsonun hesabındaki istek yöneticiye ulaşmadan reddedilir. Ancak `AuthorizationGrantService`'in kendi hesabı
kontrolü yalnız hesabın bir garsonu (`Order.ServingUserId`) varsa ve istek sahibinden farklıysa çalışıyordu; hesap
sahipsizse (ör. henüz kimsenin almadığı QR siparişi) garsonun isteği yönetici kuyruğuna düşüyordu. Semih'in kararı
(2026-09-28): "direk reddetsin". Bu görev: garsonun bir hesap kalemi üzerindeki `bills.void` / `bills.comp` isteği,
hesap garsonun kendisine ait değilse — sahipsiz olması dahil — yöneticiye ulaşmadan reddedilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-402-waiter-grant-on-unassigned-check.md`
- `evidence/V1-RMD-402/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs
  (V1-IAM-023 sahipliğinde) — yalnız kendi hesabı kontrolünün koşulu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Grants/AuthorizationGrantServiceTests.cs
  (V1-IAM-023 sahipliğinde) — sahipsiz hesap testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs
  (V1-BIL-005 sahipliğinde) — garson akışı testleri garsonun kendi hesabını tohumlar; sahipsiz hesap ret testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs
  (V1-IAM-027 sahipliğinde) — aynı değişiklik
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md (V1-IAM-016 sahipliğinde) — yalnız §3
  karar 1'e sahipsiz hesap notu

## In scope

- Kural: istek sahibi `waiter` rolünde, izin `bills.void` ya da `bills.comp`, istek bir hesap kalemine bağlı
  (`SubjectType`/`SubjectId` dolu) ve `SubjectServingUserId` istek sahibi değil (boş dahil) → `Denied`, `policy_path = auto`.
- Konusu olmayan istekler (politika motorunun birim testleri) değişmez.

## Out of scope

- Çevrimdışı mutabakat yolundaki kendi hesabı kontrolü (V1-RMD-399 H-06) — ayrı görev.
- Kasiyer/şef garson/yönetici istekleri (model §3: onlar için kendi hesabı kuralı yok).

## Dependencies

- V1-RMD-401

## Acceptance evidence

- Gerçek PostgreSQL 18 (`evidence/V1-RMD-402/tests.log`), üç proje Release, 0 uyarı / 0 hata:
  `ALKAROS.Identity.Authorization.Tests` 209/209, `ALKAROS.Host.Experience.Orders.Comp.Tests` 15/15,
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 16/16.
- Yeni testler: `OwnCheckGuardRefusesAWaiterCompOnAnUnassignedCheck` (servis düzeyi, `Refused` / `auto`),
  `AWaiterCompingAnUnassignedCheckIsRefusedByTheOwnCheckGuard` ve
  `AWaiterVoidingAnUnassignedChecksSentItemIsRefusedByTheOwnCheckGuard` (gerçek garson rolü, HTTP 403).
- Aynı üç test üretim değişikliği geri alınınca kırmızı (`evidence/V1-RMD-402/red-without-fix.log`, 3/3 başarısız).
- Karar kaydı: `docs/domain/authorization-model.md` §3 karar 1'e sahipsiz hesap notu.
- Semih'in elle deneyebileceği senaryo: henüz kimsenin almadığı bir QR siparişinde garson ikram istediğinde "İkram talebi
  reddedildi." görür; istek yöneticinin onay listesine düşmez.

## Handoff

- None
