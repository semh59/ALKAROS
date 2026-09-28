# V1-RMD-404 - Çevrimdışı mutabakatta rolü ve hesap sahibini sunucudan almak; kendi hesabı kuralını uygulamak

- Task ID: V1-RMD-404
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi H-05 (Orta) ve H-06 (Düşük-Orta): `POST /api/v1/terminals/{terminalId}/offline-reconciliation`
her eylemin `RequesterRoleCode` ve `SubjectServingUserId` alanını istemciden olduğu gibi alıyor. Canlı politika kontrolü
istemcinin bildirdiği rolle yapılıyor (garson "manager" diyerek `always_deny` sıkılaştırmasını atlatıp isteği yönetici
kuyruğuna düşürebiliyor ve denetim satırına sahte rol yazılıyor); model §3 karar 1'deki kendi hesabı kuralı bu yolda
hiç uygulanmıyor. Uçtaki yorum rolün de doğrulandığını söylüyor ama doğrulamıyor. V1-RMD-297 (Planned) WaiterPwa'yı bu
uca bağlayacağı için düzeltme istemciden önce yapılır.

Bu görev: istemcinin bildirdiği rol, çağıranın sunucudaki rolüyle aynı değilse istek 403 `IDENTITY_MISMATCH` ile
reddedilir; bir sipariş kalemine (`SubjectType = OrderItem`) bağlı eylemde hesabın garsonu istemciden değil sunucudaki
siparişten okunur; garsonun iptal/ikram eylemi hesap kendisine ait değilse (sahipsiz ya da konusuz dahil — V1-RMD-402
kararı) yöneticiye ulaşmadan `Denied` olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-404-offline-reconciliation-server-truth.md`
- `evidence/V1-RMD-404/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs
  (V1-IAM-025 sahipliğinde) — yalnız rol ve hesap sahibi doğrulaması ile yanıltıcı yorumun düzeltilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs
  (V1-IAM-022 sahipliğinde) — yalnız kendi hesabı kuralı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Offline/OfflineGrantReconcilerTests.cs
  (V1-IAM-022 sahipliğinde) — kendi hesabı testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OfflineReconciliation/OfflineReconciliationHttpTests.cs
  (V1-IAM-025 sahipliğinde) — test kullanıcıları gerçek rollere bağlanır; rol ve hesap sahibi testleri

## In scope

- Rol: çağıranın sunucudaki rolü (diğer grant uçlarıyla aynı kaynak); boşsa ya da eylemdeki rolden farklıysa 403.
- Hesap sahibi: `OrderItem` konulu eylemde `orders.orders.serving_user_id`; diğer konu türlerinde yok sayılır.
- Kendi hesabı: `waiter` rolü + `bills.void` / `bills.comp` + hesap sahibi istek sahibi değil → `Denied`.

## Out of scope

- WaiterPwa çevrimdışı istemcisi (V1-RMD-297).
- Çok rollü kullanıcıda rol seçimi (V1-RMD-399 N-1) — ayrı görev.

## Dependencies

- V1-RMD-403

## Acceptance evidence

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None
