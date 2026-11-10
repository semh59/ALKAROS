# V1-RMD-907 - Yöneticinin ikram/indirim onay ve ret kararları denetim kaydına yazılır

- Task ID: V1-RMD-907
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Önceki turların açığı (V1-RMD-901 incelemesinden kalan not): yönetici, bekleyen bir ikram/indirim/iptal talebini onayladığında ya da reddettiğinde karar yalnızca identity.authorization_grants satırına (onaylayan ve zaman) yazılıyor; denetim olayı (audit_events) yazılmıyor, bu yüzden "kim, hangi talebi, ne tutarda, hangi gerekçeyle onayladı veya reddetti" denetim izinden okunamıyor. Karar: POST /api/v1/management/authorization/grants/{id}/approve ve /deny başarılı olduğunda authorization.grant.approved / authorization.grant.denied denetim olayı yazılır (aggregate AuthorizationGrant, aktör onaylayan, gerekçe talebin gerekçe kodu; durum: izin kodu, tutar, konu tipi ve kimliği, talep eden, yol manual). Reddedilen kararlar (zaten çözülmüş talep, kendi talebi, yetkisiz onaylayan) olay yazmaz. Karar ve olay tek işlemde yazılır (grant deposuna işlem aşırı yüklemesi eklenir): olay yazımı başarısız olursa karar da geri alınır, istemci yeniden denediğinde olay kaybolmaz (qa tur 1).

## Owned surface

- `evidence/V1-RMD-907/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Authorization/AuthorizationDecisionStore.cs
  - src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs
  - src/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepository.cs
  - tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: onay ve ret sonrası denetim olayı okunur (aktör, tutar, izin kodu, konu, talep eden); reddedilen karar olay yazmaz
2. Kararı çözüp olayı yazan uygulama
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Delegasyon oluşturma ve geri alma olaylarının denetimi

## Dependencies

- V1-RMD-906

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
