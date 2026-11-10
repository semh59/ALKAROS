# V1-RMD-912 - Vekalet oluşturma, iptal ve davranışsal sıkılaştırma kaldırma denetim kaydına yazılır

- Task ID: V1-RMD-912
- Status: Planned
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-907 açığı (koordinatör kararı: ertelenmez). Yönetici onay/ret kararları denetim kaydına yazılıyor; ama vekalet (delegation) oluşturma ve iptali ile davranışsal sıkılaştırmayı (tightening) kaldırma izsiz kalıyor, oysa üçü de yetki kararıdır ve yetki izi 10 yıl saklanır. Karar: her biri 907'deki gibi kararı veren kayıtla AYNI işlemde denetim olayı yazar (authorization.delegation.created, authorization.delegation.revoked, authorization.tightening.cleared; aktör, ilgili kayıt, izin kodu, sınır, bitiş, aktörün korelasyon numarası). Olay yazılamazsa karar da verilmez. Bulunamayan veya zaten iptal edilmiş kayıt olay yazmaz.

## Owned surface

- `evidence/V1-RMD-912/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Authorization/AuthorizationDecisionStore.cs
  - src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs
  - src/Modules/Identity/Authorization/Delegations/IAuthorizationDelegationRepository.cs
  - src/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepository.cs
  - src/Modules/Identity/Authorization/Behavioural/IBehaviouralTighteningRepository.cs
  - src/Modules/Identity/Authorization/Behavioural/PostgresBehaviouralTighteningRepository.cs
  - tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testler: vekalet oluşturma, iptal ve sıkılaştırma kaldırma birer denetim olayı yazar (aktör, kayıt kimliği, izin kodu); olmayan ya da önceden iptal edilmiş kayıt olay yazmaz; olay yazımı başarısız olursa karar da verilmez
2. Depo ve mağaza değişiklikleri (işlem bağlayan aşırı yüklemeler)
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Vekalet listesi ve ekranı değişmez

## Dependencies

- V1-RMD-911

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
