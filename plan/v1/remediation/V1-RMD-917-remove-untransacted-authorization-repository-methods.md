# V1-RMD-917 - Yetki deposunda yalnız testler için duran transaction'sız eski metodlar silinir

- Task ID: V1-RMD-917
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Koordinatör kararı: üretimde çağıranı olmayıp yalnız testler için duran kod ölü koddur, silinir. V1-RMD-907 ve V1-RMD-912 ile yetki kararları (onay/ret, vekalet oluşturma/iptali, sıkılaştırma kaldırma) denetim kaydıyla aynı işlemde yapılıyor; bu yüzden şu eski, işlemsiz metodların üretimde çağıranı kalmadı: `IAuthorizationGrantRepository.ResolveAsync` (işlemsiz), `IAuthorizationDelegationRepository.CreateAsync` ve `RevokeAsync` (bool dönen), `IBehaviouralTighteningRepository.ClearAsync` (işlemsiz) ve `IBehaviouralTighteningService.ClearAsync` (işlemsiz). Karar: hepsi ve uygulamaları silinir (çekirdek metodlar işlemli aşırı yüklemeye girer); depo ve servis testleri gerçek yola (işlemli) taşınır, davranış beklentileri aynı kalır.

## Owned surface

- `evidence/V1-RMD-917/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs
  - src/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepository.cs
  - src/Modules/Identity/Authorization/Delegations/IAuthorizationDelegationRepository.cs
  - src/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepository.cs
  - src/Modules/Identity/Authorization/Behavioural/IBehaviouralTighteningRepository.cs
  - src/Modules/Identity/Authorization/Behavioural/PostgresBehaviouralTighteningRepository.cs
  - src/Modules/Identity/Authorization/Behavioural/IBehaviouralTighteningService.cs
  - src/Modules/Identity/Authorization/Behavioural/BehaviouralTighteningService.cs
  - tests/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepositoryTests.cs
  - tests/Modules/Identity/Authorization/Grants/AuthorizationGrantServiceTests.cs
  - tests/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepositoryTests.cs
  - tests/Modules/Identity/Authorization/Delegations/DelegationEscalationResolverTests.cs
  - tests/Modules/Identity/Authorization/Behavioural/PostgresBehaviouralTighteningRepositoryTests.cs
  - tests/Modules/Identity/Authorization/Behavioural/BehaviouralTighteningGateTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Silinecek her metod için üretimde başka çağıran olmadığının doğrulanması
2. Metodların ve arayüz üyelerinin silinmesi; testlerin işlemli yola taşınması (davranış beklentileri aynı)
3. Mutasyon (testlerin gerçek yolu sınadığını gösteren) ve iki bağımsız qa incelemesi

## Out of scope

- Üretimde çağıranı olan işlemsiz metodlar (örneğin liste ve arama metodları) ve diğer modüller

## Dependencies

- V1-RMD-916

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
