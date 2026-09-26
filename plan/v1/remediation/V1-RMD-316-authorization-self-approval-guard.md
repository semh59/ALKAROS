# V1-RMD-316 - Yetki talebinde ve davranışsal kısıtlama temizlemede kendi kendini onaylama koruması yoktu

- Task ID: V1-RMD-316
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K9 bulgusu: `AuthorizationDecisionStore.ResolveAsync` → `PostgresAuthorizationGrantRepository.ResolveAsync`'in SQL'i yalnız `WHERE grant_id = @id AND status = 'pending'` kontrolü yapıyordu, isteği açan kullanıcı (`RequesterUserId`) ile onaylayan/reddeden kullanıcıyı (`approverUserId`) hiçbir yerde karşılaştırmıyordu. V1-IAM-020'nin kendi belgesi de küçük işletmelerde bir kullanıcının hem garson hem yönetici rolüne birden sahip olabileceğini zaten kabul ediyor — bu durumda kullanıcı kendi açtığı indirim/iptal talebini kendi yönetici oturumuyla onaylayabiliyordu. Aynı zayıflık `ClearTighteningAsync`'te de vardı: bir kullanıcının kendi spike'layan davranışının açtığı kısıtlamayı kendisi temizleyebiliyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-316-authorization-self-approval-guard.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs
  (yalnız yeni iki exception'ın hata eşlemesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs
  (yalnız yeni `AuthorizationSelfApprovalException`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Behavioural/BehaviouralTightening.cs
  (yalnız yeni `BehaviouralTighteningSelfClearException`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs

## In scope

1. `AuthorizationDecisionStore.ResolveAsync`: onaylamadan/reddetmeden önce talebin `RequesterUserId`'sini onaylayan kullanıcıyla karşılaştırır, eşleşirse `AuthorizationSelfApprovalException` fırlatır.
2. `AuthorizationDecisionStore.ClearTighteningAsync`: aynı şekilde kısıtlamanın `UserId`'si ile temizleyen kullanıcıyı karşılaştırır.
3. Yeni iki exception, `AuthorizationDecisionEndpoints.cs`'in mevcut hata eşleme desenine (`SELF_APPROVAL_NOT_ALLOWED`, 403) eklenir.

## Out of scope

- Otomatik (policy-tabanlı) onay yollarının kendi kendini onaylama riski — bu görev yalnız manuel (`policy_path = 'manual'`) yolu kapsar; otomatik yol zaten talep eden kullanıcının kendisi tarafından tetiklenir, ayrı bir onay adımı yok.
- Delegasyon iptali (`RevokeDelegationAsync`) — devreden/devralan farklı kişiler, bu görevin bulgusunun kapsamı dışında.

## Dependencies

- None

## Acceptance evidence

Host testleri (UTF8 Postgres 18), gerçek bir HTTP sunucusuna karşı: `AuthorizationDecisionHttpTests` 7/7 (2 yeni test) —
- `ManagerCannotApproveOrDenyTheirOwnGrantRequest`: talebin `RequesterUserId`'si onaylayan yöneticinin kendi kullanıcı kimliğiyle seed edilir; hem `/approve` hem `/deny` gerçek 403 `SELF_APPROVAL_NOT_ALLOWED` döndürür, talep hâlâ `pending` listede görünür (gerçekten çözülmemiş).
- `ManagerCannotClearTheirOwnBehaviouralTightening`: aynı desen, kısıtlamanın `UserId`'si.

Mutasyon kontrolü (her iki fix için ayrı ayrı): `ResolveAsync`'e eklenen kontrol geçici kaldırıldı — ilgili test gerçekten kırmızı oldu (`Expected: Forbidden, Actual: OK`); `ClearTighteningAsync`'e eklenen kontrol geçici kaldırıldı — ilgili test gerçekten kırmızı oldu (`Expected: Forbidden, Actual: NoContent`). Her ikisinde de dosya `diff` ile birebir orijinaline geri getirildi, tüm paket (7/7) tekrar yeşil.

## Handoff

- None
