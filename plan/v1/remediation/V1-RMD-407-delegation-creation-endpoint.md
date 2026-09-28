# V1-RMD-407 - Yöneticinin süreli yetki devri (delegasyon) oluşturabileceği uç

- Task ID: V1-RMD-407
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi H-04: V1-IAM-021'in hedefi "bir yönetici belirli bir saate kadar sınırlı ikram/iskonto yetkisini
bir kişiye devreder"; tablo, çözücü (`DelegationEscalationResolver`), listeleme ve erken iptal var, ancak delegasyon
oluşturan hiçbir uç ya da kod yolu yok — çözücü yalnız elle SQL ile eklenen satırda çalışıyor. Bu görev onay karar
yüzeyine (`/api/v1/management/authorization`, yönetici veya şef garson oturumu + `reports.view`) delegasyon oluşturma
ucunu ekler.

Kurallar: devredilebilen izinler yalnız grant sınıfıdır (`bills.void`, `bills.comp`, `bills.discount`; model §3);
devreden kişi devrettiği izni kendisi doğrudan tutmalıdır (tutmadığı yetkiyi devredemez); kişi kendine devredemez;
bitiş zamanı gelecekte ve en fazla 24 saat sonra olmalıdır (model §1: "süreli"; bir vardiyayı aşmaz); tutar sınırı
negatif olamaz; alıcı var olan bir kullanıcı olmalıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-407-delegation-creation-endpoint.md`
- `evidence/V1-RMD-407/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs,
  src/Host/Experience/Authorization/AuthorizationDecisionStore.cs ve
  src/Host/Experience/Authorization/AuthorizationDecisionContracts.cs (V1-IAM-020 sahipliğinde) — yalnız delegasyon
  oluşturma ucu, deposu, sözleşmesi ve hata eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs
  (V1-IAM-020 sahipliğinde) — delegasyon oluşturma testleri ve tohumlama yardımcıları

## In scope

- `POST /api/v1/management/authorization/delegations` gövde: alıcı, izin kodu, tutar sınırı, bitiş zamanı;
  201 + oluşan delegasyon. Hatalar: geçersiz istek 400, devredenin izni yoksa 403 `DELEGATOR_LACKS_PERMISSION`,
  bilinmeyen alıcı 404 `GRANTEE_NOT_FOUND`.
- Oluşan delegasyon mevcut listede görünür ve çözücü tarafından kullanılır.

## Out of scope

- PosTerminal "Onaylar" ekranına delegasyon formu (istemci görevi).
- Onay yüzeyinin mevcut İngilizce hata metinleri (ayrı görev).

## Dependencies

- V1-RMD-406

## Acceptance evidence

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None
