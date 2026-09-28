# V1-RMD-407 - Yöneticinin süreli yetki devri (delegasyon) oluşturabileceği uç

- Task ID: V1-RMD-407
- Status: Done
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

- `ALKAROS.Host.Experience.Authorization.Tests` (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 9/9
  (`evidence/V1-RMD-407/tests.log`).
- Yeni testler: `AManagerDelegatesAGrantClassPermissionTheyHoldAndItIsListed` (201, devreden = oturumdaki yönetici,
  listede görünür) ve `ADelegationIsRefusedWhenItBreaksAnyOfItsBounds` (tutulmayan izin 403
  `DELEGATOR_LACKS_PERMISSION`; grant sınıfı olmayan izin, kendine devir, 24 saati aşan süre ve negatif tutar 400;
  bilinmeyen alıcı 404 `GRANTEE_NOT_FOUND`; hiçbir hatalı istek satır bırakmaz).
- Oluşan delegasyonun grant isteğini gerçekten çözdüğü mevcut `AnActiveDelegationResolvesTheGrantAndAppliesDirectly`
  (V1-BIL-005) testiyle zaten sınanıyor; bu görev yalnız oluşturma yolunu ekler.
- V1-RMD-399 probe'larının V1-RMD-400..407 sonrası koşusu (`evidence/V1-RMD-407/audit-probes-after-all-fixes.log` ve
  `.md`): 25'ten 24'ü geçti, `C4DelegationCanBeCreatedThroughSomeRoute` dahil; kalan tek kırmızı sahte rolün artık
  bütün istek 403 ile reddedilmesidir (probe'un beklediğinden katı; açıklaması aynı dosyada).
- Semih'in elle deneyebileceği senaryo: yönetici bir garsona "22:00'ye kadar ₺200'e kadar ikram" yetkisi verir; garsonun
  bu sınırdaki ikram isteği yöneticiye düşmeden onaylanır; süre bitince ya da yönetici iptal edince yeniden onaya gider.

## Handoff

- None
