# V1-RMD-323 - "Çözüldü" işaretlenen mutabakat vakaları her taramada yeniden açılıyordu

- Task ID: V1-RMD-323
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K18 bulgusu: `PostgresReconciliationRepository.CreateOrDeduplicateCaseAsync`'in dedup kontrolü yalnız `status IN ('Open', 'Investigating', 'Escalated')` durumundaki vakaları arıyordu. Her kaynak çiftinin (`PaymentUnknownSourcePair`, `ApprovedWithoutAllocationSourcePair`, `CardSettlementAllocationMismatchSourcePair`, `CashSessionDifferenceSourcePair`) kendi `DeduplicationKey`'i kalıcı bir varlık kimliğine (payment id, attempt id, session id) bağlı — bu kimlik hiç değişmiyor. Bir vaka `Resolved`/`Dismissed` yapıldığında altta yatan kayıt (örn. `payments.payments.status`) değişmiyorsa, bir sonraki tarama AYNI anahtarla çağrıldığında dedup kontrolü hiçbir "aktif" vaka bulamıyor ve YENİ bir vaka açıyor — sonsuz döngü. `TransitionCaseStatusAsync`'in kendi testi (`ForbiddenStatusTransitionsThrowException`) `Resolved`'ın gerçek bir TERMİNAL durum olduğunu ve elle yeniden açmanın hiç mümkün olmadığını doğruluyor — yani bu sessiz yeniden-açılma sistemin TEK "yeniden açma" yoluydu ve tamamen istenmeyen bir yan etkiydi.

## Owned surface

- `plan/v1/remediation/V1-RMD-323-reconciliation-case-dedup-includes-resolved.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/CaseFoundation/PostgresReconciliationRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reconciliation/CaseFoundation/PostgresReconciliationRepositoryTests.cs

## In scope

1. `CreateOrDeduplicateCaseAsync`'in dedup kontrolü `status IN (...)` filtresi kaldırılarak AYNI anahtara sahip HERHANGİ bir vakayla (durumu ne olursa olsun) eşleştirir.

## Out of scope

- `uq_reconciliation_cases_active_dedup` kısmi unique index'inin (yalnız aktif durumlar için) tam bir unique index'e genişletilmesi — gerçek dağıtılmış bir sistemde önceden var olan çelişkili satırlar riski nedeniyle ayrı, dikkatli bir migration görevi gerektirir; uygulama katmanındaki düzeltme bu görevin asıl bulgusunu (sessiz yeniden-açılma) zaten tamamen kapatıyor.
- `GetActiveCaseByDedupKeyAsync` — bilinçli olarak farklı, doğru adlandırılmış bir sorgu ("şu an aktif bir vaka var mı" sorusu); bu görevin bulgusuyla ilgisiz, dokunulmadı.
- Tarama için zamanlayıcı eksikliği (K18'in ikinci alt noktası) — ayrı bir operasyonel karar, bu görevin kapsamı dışı.
- `tests/Modules/Reconciliation/OnlineOrders/OnlineOrderReconciliationTests.cs`'te bulunan, bu değişiklikten TAMAMEN bağımsız, önceden var olan 13/21 test hatası (`22P05: unsupported Unicode escape sequence`) — `git stash` ile doğrulandı: temiz `master`'da da AYNI şekilde başarısız.

## Dependencies

- None

## Acceptance evidence

Host/Modules testleri (UTF8 Postgres 18), gerçek bir veritabanına karşı: yeni test `RescanningTheSameKeyAfterTheCaseIsResolvedDeduplicatesIntoItInsteadOfOpeningANewOne` — bir vaka oluşturulur, `Resolved`'a geçirilir, AYNI anahtarla ve aynı ayrıntılarla (gerçek bir yeniden taramayı taklit eder) `CreateOrDeduplicateCaseAsync` tekrar çağrılır; sonuç AYNI `CaseId`'yi döndürür (yeni bir vaka açılmadı), durum hâlâ `Resolved` (sessizce yeniden açılmadı), ve denetim izinde gerçek bir `Deduplicated` eylemi var. `ALKAROS.Reconciliation.CaseFoundation.Tests` 9/9 (1 yeni), regresyon yok.

Mutasyon kontrolü: dedup SQL'i geçici olarak eski hâline (`AND status IN (...)`) döndürüldü — yeni test gerçekten kırmızı oldu (`Expected: {ilk CaseId}, found: {yeni bir CaseId}`); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

Bağımsız doğrulama: `OnlineOrderReconciliationTests`'teki 13 test hatası `git stash` ile bu görevin değişiklikleri tamamen kaldırılıp temiz `master`'da AYNI şekilde tekrarlandığı doğrulanarak bu göreve ait olmadığı kanıtlandı — bu kapanışın kapsamına alınmadı.

## Handoff

- None
