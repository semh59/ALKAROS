# Dalga 1 — kesin göreve dönüşüm adayları

**54 kaynak → 75 kusur/teyit başlığı → 62 görev sınırı adayı.** Task ID atanmadı, üretim uygulaması başlamadı. Tüm gerçek kusurlar kritik.

## Sayaç ve sınır

{'historical-fixed': 8, 'open': 49, 'decision-blocked': 12, 'needs-verification': 6}

Aynı dosyayı paylaşan 119 aday çifti paralel uygulanamaz. Bu sayı hata veya zorunlu dependency sayısı değildir.

## İlk hazırlaştırılacak iki davranış

1. **RG-TOKEN:** token ve nested LoginSuccess biçimlendirmesi. Sentinel test önce kırmızı; mevcut token erişimi/hash/expiry korunur; interpolation/exception/nested record negatif testleri. Kanıt aynı düzeltmenin parçası.
2. **RG-IMMUTABLE:** Fields/Categories/FieldCategories değişmezliği, downcast ve init/with aliasları. Gerçek redactor bypassını kapatan test; GCM/AAD bütünlüğünü koruyan test; constructor input aliası korunmuşken getter/init kaynakları da güvenli.
Bu ikisi dış provider/mevzuat kararı gerektirmeyen kaynak kusurlarıdır; exact görev sözleşmesi ve peer review tamamlanmadan kod yazılmaz.

## Diğer adaylar

### PAY-G01 — Paid ile mali nihai kapanışın karar ve uygulama sınırı

- Durum: decision-blocked
- Kusurlar: PAY-001
- Parentlar: F0088, PO0015, PO0016
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Billing/PaymentClosure/BillClosureService.cs`
  - `src/Modules/Billing/PaymentClosure/BillPaymentClosureProjector.cs`
  - `tests/Modules/Billing/PaymentClosure/BillClosureServiceTests.cs`
- Kabul:
  - V13-FSC-002 kararını ve Paid tanımını açık onay/kaynakla belirle.
  - Seçilen sözleşme için ödeme başarılı ve mali kapı eksik/başarısız durumunu ayrı doğrula.

### PAY-G02 — Ödeme sonrası masa boşalmasında status_changed_at güncellenmiyor

- Durum: scope-review-required
- Kusurlar: PAY-002
- Parentlar: F0203
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Host/Experience/Orders/OrderSettlementService.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
- Kabul:
  - Ödemeyle Occupied→Available değişince zaman ilerler; zaten Available iken sahte değişim zamanı üretilmez.
  - Merge katılımcısı serbest kalışında aynı zaman invariantı korunur.
  - Kaynak karttaki transfer/merge/rezervasyon/unmerge dalları ayrı alt kusurlara bölünmeden F0203 kapanmaz.

### PAY-G03 — Kart çekilmedi çözümü bekleyen manuel onayı terminal duruma taşımıyor

- Durum: scope-review-required
- Kusurlar: PAY-003
- Parentlar: PL0007
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Payments/CardSettlement/CardSettlementModule.cs`
  - `src/Modules/Payments/ManualResolution/IManualCardConfirmationRepository.cs`
  - `src/Modules/Payments/ManualResolution/ManualPaymentResolutionService.cs`
  - `src/Modules/Payments/ManualResolution/PostgresManualCardConfirmationRepository.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
- Kabul:
  - Pending claim + kart çekilmedi çözümü: ödeme ve claim terminal durumları tutarlı; slip yeniden kullanım kuralı sınanır.
  - Onayla/çözüm yarışında tek terminal sonuç; audit başarısızsa her ikisi rollback.
  - Kaybolan yanıt/replay ve farklı aktör durumları sözleşmeye uygun.

### PAY-G04 — Ödeme ile sipariş tamamlama aktörü kayboluyor

- Durum: scope-review-required
- Kusurlar: PAY-004
- Parentlar: PL0008
- Kaynak dependency önerisi: PAY-G02
- Existing yüzey:
  - `src/Host/DualScreen/DualScreenApplication.Payments.cs`
  - `src/Host/Experience/Orders/OrderSettlementService.cs`
  - `src/Modules/Orders/OrderAggregate/Order.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
- Kabul:
  - İnsan ödeme yolunda geçmiş gerçek aktör içerir.
  - Sweeper için kanıtlı sistem kimliği sözleşmesi belirlenir; rastgele veya hayali kullanıcı üretilmez.
  - Sipariş zaten tamam ise fazladan history/audit oluşmaz.

### PAY-G05 — Kapanış ile eşzamanlı kart girişimi aynı hesap kilidine bağlı değil

- Durum: verification-required
- Kusurlar: PAY-005
- Parentlar: PL0009, PL0010
- Kaynak dependency önerisi: PAY-G01
- Existing yüzey:
  - `src/Modules/Billing/PaymentClosure/BillClosureService.cs`
  - `src/Modules/Billing/PaymentClosure/BillPaymentClosureProjector.cs`
  - `src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
  - `tests/Modules/Billing/PaymentClosure/BillClosureServiceTests.cs`
- Kabul:
  - Gerçek PG mandallarıyla projection okuması sonrası yeni unresolved kartı araya sok; Paid+unresolved invariantını ölç.
  - Kapanış/kart, iptal/tahsilat, bahşiş/kapanış yarışlarını ayrı parametrik matrise bağla.
  - Audit/DB hatası ve iptal kilidi bırakır; mutasyon kilit/snapshot korumasını kaldırınca test kırılır.

### PAY-G06 — İade fiş tekillik kapsamı belirlenmemiş ve korunmuyor

- Durum: decision-blocked
- Kusurlar: PAY-006
- Parentlar: PL0011, PL0014
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Host/Experience/Billing/PaymentRefunds.cs`
  - `src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs`
  - `tests/Host/Experience/Billing/PaymentRefundHttpTests.cs`
- Kabul:
  - Önce slip benzersizlik alanını mevcut mali/provider kararıyla doğrula.
  - Aynı normalleştirilmiş anahtarlı paralel iki tamamlamadan yalnız izinli olanı commit eder.
  - Farklı terminal/sağlayıcı aynı numara karar gereği mümkünse yanlış red olmaz.
  - Migration gerekiyorsa yeni migration/manifest exact surface ayrı görev, mevcut migration değiştirilmez.

### PAY-G07 — Manuel iade tamamlamada isteyen/tamamlayan ayrımı belirsiz

- Durum: decision-blocked
- Kusurlar: PAY-007
- Parentlar: PL0012, PL0014
- Kaynak dependency önerisi: PAY-G06
- Existing yüzey:
  - `src/Host/Experience/Billing/PaymentRefunds.cs`
  - `tests/Host/Experience/Billing/PaymentRefundHttpTests.cs`
- Kabul:
  - Manual/allowance/manager yolları için isteyen-onaylayan-tamamlayan matrisi onaylanır.
  - Seçilen yasaklı aynı kişi kombinasyonu mutasyonla kırmızı/yeşil kanıtlanır.
  - 401/403, audit rollback ve tekrarlanan tamamlama tek yan etki sağlar.

### PAY-G08 — Tamamlanmayan manuel iadede kullanılan allowance yaşam döngüsü belirsiz

- Durum: decision-blocked
- Kusurlar: PAY-008
- Parentlar: PL0013, PL0014
- Kaynak dependency önerisi: PAY-G07
- Existing yüzey:
  - `src/Host/Experience/Billing/PaymentRefunds.cs`
  - `src/Modules/Cash/SessionLifecycle/PostgresRefundAllowanceRepository.cs`
  - `tests/Host/Experience/Billing/PaymentRefundHttpTests.cs`
- Kabul:
  - Requested/bütçesiz red, allowance+manual bekleme, tamamlanma, vazgeçme ve oturum kapanışı matrisi ayrı ölçülür.
  - Rezervasyon mu tüketim mi kuralı onaylanır; ters hareketler negatif bütçe veya çift serbest bırakma yaratmaz.
  - Kayıp yanıt/replay ve işlem arası hata/rollback bütçeyi doğru tutar.

### PAY-G09 — Tahsis satırı olmayan sıfır ödenecek hesabın yeniden kapanışı seçilmiyor

- Durum: scope-review-required
- Kusurlar: PAY-009, PAY-012
- Parentlar: PL0015
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Host/Experience/Billing/BillClosureSweeper.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
- Kabul:
  - Önce sıfır ödenecek tahsissiz hesabın projeksiyonda PaymentSatisfied/Closed olduğunu doğrula.
  - %100 indirim sonrası ilk kapanışı hata ile düşür; tahsis yokken sweep hesabı kapatır.
  - Ödenmemiş pozitif bakiyeli hesap kapanmaz; Cancelled etkilenmez.
  - Grace ve pahalı aday seçimi sınırı korunur; gereksiz tarama ölçülür.
  - Ters fixture: eski fully-paid açık hesap ardından 201 daha yeni partial; sweep tur(lar)ında eski hesap sonunda kapanır.
  - Eşit updated_at ve200/201 sınırı deterministik, cursor/progress restart sonrası kayıp yaratmaz.
  - Sonsuz sürekli yeni trafik altında bounded progress kanıtı; yalnız ASC↔DESC değişimi yeterli kabul edilmez.

### PAY-G10 — Paid kaydı sonrası sipariş tamamlama hatası yeniden seçilmiyor

- Durum: scope-review-required
- Kusurlar: PAY-010
- Parentlar: PL0016
- Kaynak dependency önerisi: PAY-G04, PAY-G09
- Existing yüzey:
  - `src/Host/Experience/Billing/BillClosureSweeper.cs`
  - `src/Host/Experience/Orders/OrderSettlementService.cs`
  - `tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs`
- Kabul:
  - Önce bill Paid olsun, sonra order Save/audit/DB fail enjekte et; ikinci tur order tamamlanır ve masa doğru serbest kalır.
  - Replay aynı history/merge serbest bırakma işlemini ikilemez.
  - Bir order tamamlanamaması diğer adayları durdurmaz; cancellation ve tekrar başlatma çalışır.

### PAY-G11 — Approved tutar ile requested tutar üst sınırı sözleşmesi belirsiz

- Durum: decision-blocked
- Kusurlar: PAY-011
- Parentlar: PL0017
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Payments/PaymentAggregate/Payment.cs`
  - `tests/Modules/Payments/PaymentAggregate/PaymentDomainTests.cs`
- Kabul:
  - Domain talep/onay kuralı ve provider capture contract kaynağı okunur.
  - requested<tendered, partial approval, approved>requested varyasyonları gerçek invariantı sınar; para üstü ayrı korunur.
  - Kural belli değilse yeni guard ve hayali provider davranışı yok.

### RG-457-DOC — İşlem bazlı izin sözlüğü

- Durum: scope-review-required
- Kusurlar: GYZ-025
- Parentlar: F0972
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `plan/v1/remediation/V1-RMD-457-management-roles-permissions-screen.md`
  - `src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.test.tsx`
  - `src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.tsx`
  - `src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.tsx`
  - `src/Host/Experience/Roles/RoleManagementEndpoints.cs`
  - `src/Modules/Identity/Authorization/RoleManagementService.cs`
  - `tests/Host/Experience/Roles/RoleManagementHttpTests.cs`
- Kabul:
  - Role permission assignment ile permission katalog oluşturmayı ayıran işlem->izin tablosu; kullanıcı listeleme UsersManage, role mutation RolesManage açık yazılır.
  - RolesManage-only, UsersManage-only, her ikisi ve neither oturumlarında UI/HTTP kontratı ayrı doğrulanır; kayıttaki wording actual bugı gizliyorsa yeni atomik child olarak açılır, UIye üç kontrol varsayılarak eklenmez.

### RG-663-DOC — 663 -> 664 scope devir tutarlılığı

- Durum: scope-review-required
- Kusurlar: GYZ-018
- Parentlar: F0336
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V1-RMD-663/README.md`
  - `plan/v1/remediation/V1-RMD-663-envelope-hardening-and-boundary-coverage.md`
  - `plan/v1/remediation/V1-RMD-664-typed-envelope-failures.md`
  - `tests/BuildingBlocks/Security/SensitiveData/Encryption/EnvelopeHardeningTests.cs`
- Kabul:
  - 663te tarihsel ilk kapsam korunup 664 transferi Goal/Owned surface/kabulde tutarlı nitelendirilir; 664 kanıtı 663e kopyalanıp yeni sonuç sayılmaz.
  - B019 ve her SensitiveData testinin tek gerçek uygulama sahibine izlenebilir bağı; Messaging ve boundary teslimi ayrı kalır.

### RG-ANON-BOUNDARY — Anonimleştirme SQL sahiplik kararı

- Durum: decision-blocked
- Kusurlar: GYZ-011
- Parentlar: F0176
- Kaynak dependency önerisi: RG-SUPPLIER-ELIGIBILITY
- Existing yüzey:
  - `docs/architecture/module-dependency-rules.md`
  - `plan/v1.5/kvkk/V15-KVK-002-cross-store-anonymization.md`
  - `src/Host/Program.cs`
  - `src/Modules/Privacy/Anonymization/AnonymizationModels.cs`
  - `src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs`
  - `tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`
  - `tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs`
- Kabul:
  - Yetkili mimari karar açıkça SQL-plan istisnasını kabul eder veya sahibi modül kontratlarına geçişi seçer; geçmiş scope ihlali geriye dönük silinmez.
  - Sırf SQL metnini başka klasöre taşıma/allowlist genişletme kabul değil; seçilen kontrat için canlı 5 sınıf, checkpoint/audit/rollback ve architecture negatif testleri aynı invariantı korur.

### RG-AUDIT-REFERENCE — Audit-disposal bağlantı durumu

- Durum: scope-review-required
- Kusurlar: GYZ-017
- Parentlar: F0565
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V15-KVK-002/README.md`
  - `plan/v1/remediation/V1-RMD-481-audit-log-ten-year-disposal.md`
- Kabul:
  - Tarihsel planned ifadesine güncel Done/kanıt bağı ekle; scope yalnız audit.audit_events.
  - privacy.anonymization_events kusuru açık kalır; RMD-481 tarihsel logları yeni runtime testi olarak sunulmaz.

### RG-BLOCKED-EVENTS — Değişmeyen engel olaylarının tekilliği

- Durum: scope-review-required
- Kusurlar: GYZ-016
- Parentlar: F0565
- Kaynak dependency önerisi: RG-ANON-BOUNDARY
- Existing yüzey:
  - `src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs`
  - `tests/Modules/Privacy/Anonymization/AnonymizationWorkflowTests.cs`
- Kabul:
  - Aynı balance ile 100 retry: seçilen kayıtlı transition/attempt politikasına göre event hacmi sınırlı; aynı Blocked transition tekilleşir.
  - Engel nedeni değişmesi, release->resume, gerçek failed step->retry ve completed olayları korunur; iki worker duplicate blocked transition üretmez.

### RG-CREDENTIAL-DELETE — Credential event-trigger delete kararı

- Durum: decision-blocked
- Kusurlar: GYZ-013
- Parentlar: F0178
- Kaynak dependency önerisi: RG-RETENTION-INTEGRATION
- Existing yüzey:
  - `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`
  - `plan/v1.5/security/V15-SEC-003-sensitive-payload-protection.md`
  - `src/Host/Program.cs`
  - `src/Modules/Security/DataProtectionRetention/DisposalMatrix.cs`
  - `src/Modules/Security/DataProtectionRetention/PostgresRetentionSubjectStore.cs`
  - `src/Modules/Security/DataProtectionRetention/RetentionExecutionService.cs`
  - `tests/Modules/Security/DataProtectionRetention/DeletionQueueProcessorTests.cs`
  - `tests/Modules/Security/DataProtectionRetention/RetentionExecutionServiceTests.cs`
- Kabul:
  - Authoritative employment-end olayı ve approved due zamanı ile yalnız uygun credential kuyruklanır, sonra gerçek authoritative kayıt silinir; tekrar no-op.
  - Aktif/yeni/held credential, retain verisi ve mali referanslar korunur. nullu sıfır gün yaparak tüm credentialı silme kabul değil.
  - Mevcut misleading test adı actual skip davranışını anlatır; event-trigger testine mekanizma testini ikame etmez.

### RG-CUSTOMER-ELIGIBILITY — Müşteri scrub öncesi tüm uygunluk predicatei

- Durum: scope-review-required
- Kusurlar: GYZ-008, GYZ-009
- Parentlar: F0175
- Kaynak dependency önerisi: RG-HOLD
- Existing yüzey:
  - `src/Host/Program.cs`
  - `src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs`
  - `src/Modules/Privacy/RetentionExecution/PostgresRetentionExecutionService.cs`
  - `src/Modules/Privacy/RetentionExecution/RetentionSelection.cs`
  - `tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs`
- Kabul:
  - Önce eski müşteri kuyruğa; sonra sıfır-net güncel hareket ve ayrı güncel fatura; apply profili/requests değiştirmez, kalem Pending.
  - Vade sınırı kullanılan policy_version/as-of ile açık tanımlı; uygun eski müşteri temizlenir. Aynı account lock ile hareket yarışını ve fatura üreticisinin katılımını gerçek PG ile kanıtla.
  - Queue -> Pending request insert -> apply: profile envelope ve request status/version değişmez, checkpoint yok.
  - RetentionBlocked/uygun request kombinasyonları mevcut sözleşmeye göre ayrı; Pending çıkınca resume idempotent; request insert/write yarışında ortak serialization kanıtı.

### RG-DEVICE-DELETE — Cihaz lifecycle delete kararı

- Durum: decision-blocked
- Kusurlar: GYZ-014
- Parentlar: F0178
- Kaynak dependency önerisi: RG-CREDENTIAL-DELETE
- Existing yüzey:
  - `evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`
  - `src/Modules/Security/DataProtectionRetention/DisposalMatrix.cs`
  - `src/Modules/Security/DataProtectionRetention/PostgresRetentionSubjectStore.cs`
  - `src/Modules/Security/DataProtectionRetention/RetentionExecutionService.cs`
  - `tests/Modules/Security/DataProtectionRetention/DeletionQueueProcessorTests.cs`
  - `tests/Modules/Security/DataProtectionRetention/RetentionExecutionServiceTests.cs`
- Kabul:
  - Onaylı cihaz kimliği+decommission state/event olmadan otomatik delete eklenmez.
  - Karar sonrası gerçek decommission akışı queue -> purge yapar; aktif/başka cihaz/held kayıt değişmez; yinelenen event idempotent.

### RG-HOLD — Legal hold-write atomik sınırı

- Durum: scope-review-required
- Kusurlar: GYZ-007
- Parentlar: F0019
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs`
  - `src/Modules/Privacy/RetentionExecution/IRetentionExecutionService.cs`
  - `src/Modules/Privacy/RetentionExecution/PostgresRetentionExecutionService.cs`
  - `tests/Modules/Privacy/Anonymization/AnonymizationWorkflowTests.cs`
  - `tests/Modules/Privacy/RetentionExecution/RetentionExecutionTests.cs`
- Kabul:
  - İki gerçek PG bağlantısı ve deterministik bariyerle: liste alındı -> hold commit -> ilk write: tüm alan byte-for-byte aynı, checkpoint/StepDone yok, work Pending.
  - İlk adım commit -> hold commit -> ikinci adım: sonraki alan dokunulmaz, önce tamamlanan adım kaydı korunur; hold release sonrası güvenli resume.
  - Hold ve write tam çakışmada her iki commit sırası kanıtlanır; yalnız application check değil ortak lock/transaction protokolü. Hata/cancellation write+checkpoint+event rollback.

### RG-IAM008-DOC — Authorization linearization policy kararı

- Durum: scope-review-required
- Kusurlar: GYZ-019, GYZ-023
- Parentlar: F0130, F0957
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`
  - `plan/v1/remediation/V1-RMD-743-son-yonetici-es-zamanlilik.md`
  - `src/Modules/Identity/Authorization/AuthorizationService.cs`
  - `src/Modules/Identity/Authorization/PostgresRoleRepository.cs`
  - `src/Modules/Identity/Authorization/RoleManagementService.cs`
- Kabul:
  - Yetkili karar command-startı kabul ederse IAM008 atomiklik kabulü ile yorum tutarlı açıkça düzeltilir; reddederse authorization+mutation transaction/conditional write olarak uygulanır.
  - Gerçek bariyer: allow kararı tamamlanır, actor revocation commit olur, mutation serbest bırakılır; seçilen sonuç ve side effects kesin assert edilir.
  - Tarihsel implementasyon ve doğrulama rolü seçilen policyye göre tutarlı adlandırılır; eski kapanış tarihi ve proof zayıflığı korunur.
  - Plan-audit ve scope kontrolü gerçek exit koduyla; belge düzenlemesi runtime authorization fix sayılmaz.

### RG-IMMUTABLE — Değişmez hassas payload/envelope sınırı ve gerçek regresyon kanıtı

- Durum: scope-review-required
- Kusurlar: GYZ-003, GYZ-004, GYZ-005, GYZ-006
- Parentlar: F0015, F0016
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V1-SEC-005/immutable_data_classification_verification.txt`
  - `plan/v1/security-foundation/V1-SEC-005-immutable-data-classification.md`
  - `src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs`
  - `src/BuildingBlocks/Security/SensitiveData/SensitivePayload.cs`
  - `tests/BuildingBlocks/Security/SensitiveData/MetadataIntegrity/EnvelopeMetadataIntegrityTests.cs`
  - `tests/BuildingBlocks/Security/SensitiveData/Protection/EnvelopeProtectionTests.cs`
  - `tests/BuildingBlocks/Security/SensitiveData/Protection/PayloadRedactionTests.cs`
- Kabul:
  - Kurucu girdisini sonradan değiştirme çıktıyı değiştirmez.
  - Returned Fields üzerinde desteklenen mutable arayüz/cast ile set/add/remove/clear etkisiz veya açık ret; redaction/serialization snapshot tutarlı.
  - Pii/Payment sentinel için Dictionary ve IDictionary mutasyon denemeleri sonrası maske kalır; Public özgün alan görünür.
  - Eksik sınıflandırma kurucuda reddedilir, kategori silme/clear ve tekrar redaction ham veri üretmez.
  - Ctor input alias, returned dictionary mutable arayüzleri, persistence restore ve desteklenen with/init yolu immutable snapshot sağlar.
  - AAD üzerinde metadata değişmiş zarf hâlâ typed failure verir; doğru şifreli eski zarf round-trip açılır; immutable düzeltme persistence uyumluluğunu bozmaz.
  - a9ebe9a2 stat/diff çıktısı ile tarihsel hükmü correction notunda ayır.
  - RG-PAYLOAD ve RG-ENVELOPE için yeni SHA, cast/alias/init red-green ve immutable temsil kaldırılınca kırılan mutant çıktısı; kaynak restore hashleri.

### RG-INFLIGHT-EVIDENCE — IAM008 kanıt tamlığı

- Durum: scope-review-required
- Kusurlar: GYZ-022
- Parentlar: F0957
- Kaynak dependency önerisi: RG-INFLIGHT-TEST
- Existing yüzey:
  - `evidence/V1-IAM-008/authorization_linearization_verification.txt`
  - `evidence/V1-IAM-008/test_output.txt`
  - `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`
  - `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- Kabul:
  - Seçilen policy ve RG-INFLIGHT-TESTe özgü raw command/exit0/SHA/ortam/commit-order sonuçları kaydedilir; eski özet tarihsel kalır.
  - Format ve Authorization suite çıktıları ayrı scope ile; 20 tarihsel test sayısı current count veya atomiklik kanıtı sayılmaz.

### RG-INFLIGHT-TEST — Gerçek revocation-before-write interleaving testi

- Durum: scope-review-required
- Kusurlar: GYZ-020
- Parentlar: F0130
- Kaynak dependency önerisi: RG-IAM008-DOC
- Existing yüzey:
  - `src/Modules/Identity/Authorization/IAuthorizationService.cs`
  - `src/Modules/Identity/Authorization/PostgresRoleRepository.cs`
  - `src/Modules/Identity/Authorization/RoleManagementService.cs`
  - `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- Kabul:
  - Authorization sonrası kontrollü bariyer, ayrı bağlantıda revocation commit, sonra repo write release; commit sırası kanıtı ve row postcondition.
  - İzin yok başlangıçta hiç write yok; command-start kararı seçilmişse önce-authorized write kabulü ve sonrası komutun denialı ayrı test; scheduler timingine dayalı Task.Delay tek başına yok.

### RG-LAST-MANAGER — Tarihsel last-holder fix teyidi

- Durum: historical-regression-only
- Kusurlar: GYZ-021
- Parentlar: F0131, F0159
- Kaynak dependency önerisi: RG-INFLIGHT-TEST
- Existing yüzey:
  - `src/Modules/Identity/Authorization/IRoleRepository.cs`
  - `src/Modules/Identity/Authorization/PostgresRoleRepository.cs`
  - `src/Modules/Identity/Authorization/RoleManagementService.cs`
  - `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- Kabul:
  - Fresh gerçek PGde two-user mutual revoke, same-role ve mixed user/permission revoke yarışında en az bir aktif holder kalır.
  - Yetkisiz actor ve unrelated permission, inactive ikinci holder, multiple-role surviving holder, cancellation/rollback yolları. Kilit çıkarılınca test kırılmalı; geçmiş 3 mutant bu oturum sonucu değildir.

### RG-RETENTION-INTEGRATION — Gerçek provider payload lifecycle entegrasyonu

- Durum: scope-review-required
- Kusurlar: GYZ-012
- Parentlar: F0020, F0325
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `database/migrations/V15/V15-SEC-003/137-retention-subjects.up.sql`
  - `src/Host/Experience/SecurityAdministration/Maintenance/RetentionSweepMaintenanceJob.cs`
  - `src/Modules/OnlineOrdering/Providers/Inbox/ProviderInbox.cs`
  - `src/Modules/Security/DataProtectionRetention/AuthorizedReEncryptionService.cs`
  - `src/Modules/Security/DataProtectionRetention/DeletionQueueProcessor.cs`
  - `src/Modules/Security/DataProtectionRetention/IRetentionSubjectStore.cs`
  - `src/Modules/Security/DataProtectionRetention/PostgresRetentionSubjectStore.cs`
  - `src/Modules/Security/DataProtectionRetention/RetentionExecutionService.cs`
  - `src/Modules/Security/SecurityModule.cs`
  - `tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs`
  - `tests/Modules/Security/DataProtectionRetention/RetentionExecutionServiceTests.cs`
- Kabul:
  - Gerçek provider ingest ile retention kaydı oluşur; doğrudan test InsertAsync/seeding kullanılmaz. Duplicate delivery tek lifecycle record; write/registration yarım başarısında rollback.
  - Zamanı ilerlet/sweep -> authoritative ProviderInbox.OpenPayload artık ham body döndüremez; retention kaydı ve ana tablo çelişmez. Held/fresh payload okunabilir kalır, retry idempotent, audit/logda sentinel plaintext yok.
  - Existing business retention (CustomerProfile vb.) ile çift imha ve çakışan süre yaratılmaz; re-encryption gerçek okuyucunun aynı authoritative kaydını günceller.

### RG-SCHEDULER-DOC — Retention erişilebilirlik güncellik notu

- Durum: scope-review-required
- Kusurlar: GYZ-015
- Parentlar: F0567
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `plan/v1.5/security/V15-SEC-003-sensitive-payload-protection.md`
  - `src/Host/Experience/SecurityAdministration/Maintenance/RetentionSweepMaintenanceJob.cs`
  - `src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs`
  - `tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs`
- Kabul:
  - Tarihsel 2026-09-23 notu korunur, güncel V1-RMD-268 wiring ile sweep/deletion erişimi ve hâlâ eksik producer/re-encryption ayrılır.
  - Manual run 401/403 ve held/fresh/expired/idempotent mevcut test kapsamı açıkça yazılır; test çalıştırılmadıysa yeni geçti denmez.

### RG-SELF-REVOKE — Self-revoke sözleşme kararı ve doğrulama

- Durum: verification-required
- Kusurlar: GYZ-024
- Parentlar: F0972
- Kaynak dependency önerisi: RG-LAST-MANAGER
- Existing yüzey:
  - `plan/v1/remediation/V1-RMD-457-management-roles-permissions-screen.md`
  - `src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.test.tsx`
  - `src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.tsx`
  - `src/Modules/Identity/Authorization/PostgresRoleRepository.cs`
  - `src/Modules/Identity/Authorization/RoleManagementService.cs`
  - `tests/Host/Experience/Roles/RoleManagementHttpTests.cs`
  - `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- Kabul:
  - Yetkili karar self-revokeyi kabul/ret olarak kaydeder; kabulde confirmation cancel hiçbir istek göndermez, last-manager 409 veri değiştirmez.
  - Başka aktif yönetici varken onaylanan self-revoke sonrası yeni komut denialı ve UI capability yenilenmesi ayrıca doğrulanır; client-only engel security fix sayılmaz.

### RG-STK-02 — Mal kabul kayıtlı satır-yarışı kapanışı

- Durum: scope-review-required
- Kusurlar: STK-001, STK-002
- Parentlar: F0017, F0026
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Purchasing/OrdersAndReceipts/IPurchaseOrderRepository.cs`
  - `src/Modules/Purchasing/OrdersAndReceipts/IPurchasingService.cs`
  - `tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs`
- Kabul:
  - Mevcut ConcurrentReceiptsOfSameOrderPostStockOnce ve StaleReceivedQuantityIsRejectedWhenLockingOrderLines yeniden çalıştırılır; yalnız beklenen PurchaseOrderStatusException kabul edilir; received_quantity, fiş sayısı, ledger toplamı ve on_hand birlikte doğrulanır.
  - İki bağlantı ve barrier ile cancel-commit→receipt-write ve receipt-commit→stale-cancel-write sıraları kurulur; biri tipli conflict alır; PO status/miktar, receipt, ledger ve balance tek tutarlı sonucu verir.

### RG-STK-03 — Receipt duplicate repository koruması teyidi

- Durum: historical-regression-only
- Kusurlar: STK-003
- Parentlar: F0026
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Purchasing/OrdersAndReceipts/IGoodsReceiptRepository.cs`
  - `tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs`
- Kabul:
  - Farklı PO üstünde aynı ReceiptNumber yarışı; bir kayıt, beklenen DuplicateGoodsReceiptException, kaybedenin PO/ledger/balance etkisi yok. Exact constraint discriminasyonu için farklı unique failure ayrı negatif kontrol edilir; bugünkü header catch bütün23505leri çevirir.

### RG-STK-04 — Mal kabul politika teyidi

- Durum: decision-blocked
- Kusurlar: STK-004
- Parentlar: F0026
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `plan/v1.1/purchasing/V11-PUR-001-purchase-order-and-receipt.md`
  - `src/Modules/Purchasing/OrdersAndReceipts/ReceiptVariancePolicy.cs`
  - `tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs`
- Kabul:
  - Semih %5 eşiği ve purchasing.manage→onay yetkisi ilişkisini tarihli karar olarak teyit eder; onaylı/onaysız/sınır toleransı testleri seçilen kurala bağlanır.

### RG-STK-05 — 742 dar kapanış teyidi

- Durum: historical-regression-only
- Kusurlar: STK-005
- Parentlar: F0030
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs`
  - `src/Modules/Inventory/MovementLedger/PostgresStockMovementRepository.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
- Kabul:
  - 5100 hareket rebuild ve ledger-read-failure mevcut testleri yeniden çalıştırılır; negatif Out işareti ve okuma-önce sırası mutasyonları kırılır.

### RG-STK-06 — Full stock rebuild yazım/hata/iptal atomikliği

- Durum: scope-review-required
- Kusurlar: STK-006, STK-007
- Parentlar: F0030
- Kaynak dependency önerisi: RG-STK-05, RG-STK-08
- Existing yüzey:
  - `src/Modules/Inventory/BalanceProjection/IStockBalanceRepository.cs`
  - `src/Modules/Inventory/BalanceProjection/PostgresStockBalanceRepository.cs`
  - `src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs`
  - `src/Modules/Inventory/MovementLedger/IStockMovementRepository.cs`
  - `src/Modules/Inventory/MovementLedger/PostgresStockMovementRepository.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDomainTests.cs`
- Kabul:
  - Reset sonrası, ikinci on-hand yazımında ve reserved restore sırasında gerçek transaction içinde hata/ct cancel enjekte edilir; bütün balance satırları ve reserved/available önceki snapshot ile eşit kalır.
  - SUM sonrası purchase/consumption commit ve reserved snapshot sonrası create/terminal commit barrier ile zorlanır; rebuild + writer sonrası SQL ledger SUM ve active reservation SUM ile tüm alanlar eşleşir; yeni item/location satırı da kapsanır.

### RG-STK-07 — Item/location replay SQL aggregate

- Durum: scope-review-required
- Kusurlar: STK-008
- Parentlar: F0030
- Kaynak dependency önerisi: RG-STK-08
- Existing yüzey:
  - `src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs`
  - `src/Modules/Inventory/MovementLedger/IStockMovementRepository.cs`
  - `src/Modules/Inventory/MovementLedger/PostgresStockMovementRepository.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDomainTests.cs`
- Kabul:
  - Aynı konumda 5100 hareket ve başka item hedefi; doğrudan item/location aggregate sonucu doğru; diğer konum hareketleri karışmaz; In/Out/Reversal ve boş ledger doğrulanır.

### RG-STK-08 — Full stock rebuild erişilebilirlik kararı

- Durum: decision-blocked
- Kusurlar: STK-009
- Parentlar: F0227
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/BalanceProjection/IStockBalanceProjector.cs`
  - `src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs`
  - `src/Modules/Inventory/InventoryModule.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDomainTests.cs`
- Kabul:
  - Semih bakım gereksinimini seçer: gerçek yetkili operasyon akışı exact path ile ayrı planlanır veya unused method/interface/test-only feature kaldırma etkisi kaydedilir; yeni hayali endpoint kurulmaz.

### RG-STK-09 — 747 terminal replay dar kapanış teyidi

- Durum: historical-regression-only
- Kusurlar: STK-011
- Parentlar: F0025, F0212
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/ReservationBalanceProjection/PostgresReservationBalanceRepository.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjector.cs`
  - `tests/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjectionDatabaseTests.cs`
- Kabul:
  - Mevcut ReplayingATerminalTransitionAfterARebuildIsStillIdempotent yeniden koşulur; Released/Consumed/Waste ve aynı pair üstünde başka Reserved varken replay başka rezervasyonu azaltmaz; terminal olay üretimi kaldırılınca test kırılır.

### RG-STK-10 — Reservation event+balance rebuild yazım atomikliği

- Durum: scope-review-required
- Kusurlar: STK-012, STK-013
- Parentlar: F0211
- Kaynak dependency önerisi: RG-STK-06, RG-STK-09, RG-STK-33
- Existing yüzey:
  - `src/Modules/Inventory/ReservationBalanceProjection/IReservationBalanceRepository.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/PostgresReservationBalanceRepository.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjector.cs`
  - `tests/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjectionDatabaseTests.cs`
- Kabul:
  - Event rebuild sonrası ve ikinci balance yazımında failure/cancel; event seti ve tüm reserved/available önceki duruma tamamen döner; retry sonunda drift yoktur.
  - Aggregate sonrası terminal ve create commit interleaving; rebuild sonunda active reservation SUM=reserved, available=on_hand-reserved; event replay hiçbir ikinci etki üretmez.

### RG-STK-11 — Reservation raporunda COUNT ve SUM ayrımı

- Durum: scope-review-required
- Kusurlar: STK-014
- Parentlar: F0211
- Kaynak dependency önerisi: RG-STK-10
- Existing yüzey:
  - `src/Modules/Inventory/ReservationBalanceProjection/IReservationBalanceRepository.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/PostgresReservationBalanceRepository.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceModels.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjector.cs`
  - `tests/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjectionDatabaseTests.cs`
- Kabul:
  - Aynı pairde 3×0.5 Reserved ve bir terminal kaydı: TotalActiveReservations=3, reserved=1.5; miktar toplamı int sınırını aşsa da kayıt sayısı overflowa bağlanmaz.

### RG-STK-12 — Tx’sız reversal atomikliği

- Durum: scope-review-required
- Kusurlar: STK-015
- Parentlar: F0228
- Kaynak dependency önerisi: RG-STK-35
- Existing yüzey:
  - `src/Modules/Inventory/MovementReversal/IStockMovementReversalService.cs`
  - `src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDomainTests.cs`
- Kabul:
  - Overload tutulursa gerçek Postgres ledger append sonrası balance failure/cancel: ne reversal ne delta kalır; tekrar tek etki; kaldırılırsa bütün callers tx overloada taşınır ve kullanılmayan dependency kalmaz.

### RG-STK-13 — 748 işlemli reversal kapanış teyidi

- Durum: historical-regression-only
- Kusurlar: STK-017
- Parentlar: F0229
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs`
  - `tests/Modules/Inventory/MovementReversal/PartialStockReturnTests.cs`
- Kabul:
  - Mevcut tipli ret testi tekrar koşulur; caller transaction rollback açıkça doğrulanır; guard kaldırma mutasyonu kırılır.

### RG-STK-14 — Manual adjustment authorization sınırı

- Durum: scope-review-required
- Kusurlar: STK-018
- Parentlar: F0230
- Kaynak dependency önerisi: RG-STK-34
- Existing yüzey:
  - `src/Modules/Inventory/InventoryModule.cs`
  - `src/Modules/Inventory/ManualAdjustments/IInventoryAdjustmentService.cs`
  - `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs`
  - `tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDatabaseTests.cs`
  - `tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDomainTests.cs`
- Kabul:
  - Seçilen gerçek güven sınırında nonempty fakat izinsiz actor reddedilir ve hiçbir kayıt oluşmaz; yetkili actor tek ledger+balance etkisi verir; istemciden AuthorizedBy güvenilir kimlik sayılmaz.

### RG-STK-15 — Waste idempotency payload bütünlüğü

- Durum: scope-review-required
- Kusurlar: STK-020
- Parentlar: F0231
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Host/Experience/Inventory/StockMasterEndpoints.cs`
  - `src/Modules/Inventory/WasteRecording/PostgresWasteRecordRepository.cs`
  - `src/Modules/Inventory/WasteRecording/WasteModels.cs`
  - `src/Modules/Inventory/WasteRecording/WasteRecordingService.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
  - `tests/Modules/Inventory/WasteRecording/WasteRecordingDatabaseTests.cs`
  - `tests/Modules/Inventory/WasteRecording/WasteRecordingDomainTests.cs`
- Kabul:
  - Aynı key+aynı yük replay; farklı item/location/quantity/unit/source/sourceReference ve onaylı payload alanları conflict. Erken hit ve concurrent unique-race recovery aynı karşılaştırmayı kullanır; caller tx ve HTTP 409/Türkçe mesaj; ikinci ledger/balance etkisi yok.

### RG-STK-16 — Waste reversal invariantının bütün girişlerde korunması

- Durum: scope-review-required
- Kusurlar: STK-021
- Parentlar: F0232
- Kaynak dependency önerisi: RG-STK-12
- Existing yüzey:
  - `src/Modules/Inventory/MovementLedger/StockMovement.cs`
  - `src/Modules/Inventory/MovementLedger/StockMovementService.cs`
  - `src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementLedger/StockMovementDomainTests.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDomainTests.cs`
- Kabul:
  - Hazırlanmış/PortionReservation waste için CanReverse=false ve tüm kullanılan reversal girişlerinde tipli ret; ledger ve on_hand/reserved/available değişmez. Yanlış girilmiş genel spoilage fire düzeltmesi isteniyorsa Semih onaylı ayrı düzeltme politikası gerekir; otomatik yeniden satış yok.

### RG-STK-17 — Waste çift azaltma concurrency coverage

- Durum: scope-review-required
- Kusurlar: STK-022, STK-023
- Parentlar: F0516
- Kaynak dependency önerisi: RG-STK-15
- Existing yüzey:
  - `tests/Modules/Inventory/WasteRecording/WasteRecordingDatabaseTests.cs`
- Kabul:
  - 10 stok, iki farklı-key 7 miktar; stale precheck barrier ile ikisini geçir; tam bir başarı+bir InsufficientStockForWasteException, son stok3, tek waste+tek hareket. Guard WHERE kaldırma mutasyonu testi kırar.
  - Ledger append sonrası, waste insert sonrası, guarded apply öncesi ve commit ct iptalinde exception enjekte et; üçünün toplamları eski durumda; sonra retry bir kez başarılı. Caller rollback mevcut testi de yeniden koş.

### RG-STK-19 — 741 nonzero delta dar kapanış teyidi

- Durum: scope-review-required
- Kusurlar: STK-024, STK-025
- Parentlar: F0031
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/PhysicalCounts/PhysicalCountService.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
  - `tests/Modules/Inventory/PhysicalCounts/PhysicalCountDatabaseTests.cs`
- Kabul:
  - Mevcut nonzero yarış testi ve guard removal mutasyonu yeniden çalıştırılır; count row da rollback sonrası yok assertions eklenir.
  - Mevcut RacingBalanceRepository ile initial10, counted10, araya−3 koy; PhysicalCountBalanceGuardFailedException/HTTP409, count row yok, balance7 korunur. Bir başka tx hareketini count commitine kadar serialize eden doğru çözümde postcondition eşdeğer kanıtlanır.

### RG-STK-20 — Physical count retry kimliği

- Durum: scope-review-required
- Kusurlar: STK-026
- Parentlar: F0699
- Kaynak dependency önerisi: RG-STK-19
- Existing yüzey:
  - `database/MigrationComposition/order.json`
  - `src/Clients/PosTerminal/src/features/management-stock/stockApi.ts`
  - `src/Host/Experience/Inventory/StockMasterContracts.cs`
  - `src/Host/Experience/Inventory/StockMasterEndpoints.cs`
  - `src/Modules/Inventory/PhysicalCounts/IPhysicalCountRepository.cs`
  - `src/Modules/Inventory/PhysicalCounts/IPhysicalCountService.cs`
  - `src/Modules/Inventory/PhysicalCounts/PhysicalCountService.cs`
  - `src/Modules/Inventory/PhysicalCounts/PostgresPhysicalCountRepository.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
  - `tests/Modules/Inventory/PhysicalCounts/PhysicalCountDatabaseTests.cs`
- Kabul:
  - Kalıcı count/request identity ile aynı payload retry aynı countId döner; count sonrası satış−3 ardından retry stok artırmaz. Aynı key farklı item/location/count/notes için açık conflict politikası; farklı key yeni gerçek sayım kabul edilir. DB migration gerekiyorsa yeni exact migration adı ve composition yüzeyi plan onayı olmadan eklenmez.

### RG-STK-21 — Ledger reason dil iddiası teyidi

- Durum: verification-required
- Kusurlar: STK-027
- Parentlar: F0699
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/PhysicalCounts/PhysicalCountService.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
- Kabul:
  - Ledger reasonı gösteren gerçek UI/API consumer bulunur; kullanıcıya ham English veriyorsa o sinkte test planlanır; sink yoksa aday explicit non-finding olarak sonuçlandırılır.

### RG-STK-22 — INV-008 ilk kapanış kanıt teyidi

- Durum: scope-review-required
- Kusurlar: STK-028
- Parentlar: F0031
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `plan/v1.1/inventory/V11-INV-008-physical-counts.md`
  - `tests/Modules/Inventory/PhysicalCounts/PhysicalCountDatabaseTests.cs`
- Kabul:
  - INV-008 tarihsel commit/artifact bulunur ve acceptance bazında eşlenir; yoksa kanıtsız olduğu kayda geçer, 741 sayıları ilk kapanış kanıtı diye kullanılmaz.

### RG-STK-23 — Stock balance uniqueness belgesi

- Durum: scope-review-required
- Kusurlar: STK-029
- Parentlar: F0295
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `docs/data/nullable-unique-policy.md`
  - `plan/v0/data-architecture/V0-DAT-003-nullable-unique-policy.md`
- Kabul:
  - Karar metni exact kolon/NOT NULL/UNIQUE ile eşlenir; central-stock nullable ihtiyacı varsa ayrı Semih kararı olmadan mevcut şema değiştirilmez.

### RG-STK-24 — Printer route uniqueness belgesi

- Durum: scope-review-required
- Kusurlar: STK-030
- Parentlar: F0295
- Kaynak dependency önerisi: RG-STK-23
- Existing yüzey:
  - `docs/data/nullable-unique-policy.md`
  - `plan/v0/data-architecture/V0-DAT-003-nullable-unique-policy.md`
- Kabul:
  - Belgedeki printer_routes satırı ve partial-index invariantı route_level/is_active koşullarını doğru yansıtır; sırf belgeye uydurmak için eski migration yeniden yazılmaz.

### RG-STK-25 — Ledger migration kanıt yolu düzeltmesi

- Durum: scope-review-required
- Kusurlar: STK-031
- Parentlar: F0641, F1005
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V11-INV-001/closure-evidence.txt`
  - `plan/v1.1/inventory/V11-INV-001-immutable-stock-ledger.md`
- Kabul:
  - 057→060 yol eşleme notu tarihli provenance ile eklenir; eski logları yeni çalıştırılmış gibi değiştirme; up/down gerçek dosyalara resolve olur.

### RG-STK-26 — Balance migration kanıt yolu düzeltmesi

- Durum: scope-review-required
- Kusurlar: STK-032
- Parentlar: F0697
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V11-INV-002/closure-evidence.txt`
  - `plan/v1.1/inventory/V11-INV-002-stock-balance-projection.md`
- Kabul:
  - 058→061 güncel yol notu tüm up/down/write-set/test atıflarını kapsar; eski komut sonuçları tarihsel tutulur.

### RG-STK-27 — Ledger-only unused orchestration kaldırma planı

- Durum: scope-review-required
- Kusurlar: STK-033
- Parentlar: F1005
- Kaynak dependency önerisi: RG-STK-16
- Existing yüzey:
  - `src/Modules/Inventory/InventoryModule.cs`
  - `src/Modules/Inventory/MovementLedger/IStockMovementService.cs`
  - `src/Modules/Inventory/MovementLedger/StockMovementService.cs`
  - `tests/Modules/Inventory/BalanceProjection/StockBalanceDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementLedger/StockMovementDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementLedger/StockMovementDomainTests.cs`
  - `tests/Modules/Inventory/MovementReversal/PartialStockReturnTests.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDatabaseTests.cs`
- Kabul:
  - Üretimde gerçek caller yokluğu method-level doğrulanır; kullanılmayan service/interface/DI kaldırma planı test fixture recorderlarını repository seeding ile değiştirir; yaşayan repository/domain/immutability/reversal test gücü düşmez.

### RG-STK-28 — Inactive mapping karar teyidi

- Durum: decision-blocked
- Kusurlar: STK-034
- Parentlar: F0698
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/StockMaster/StockMasterService.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
  - `tests/Modules/Inventory/StockMaster/StockMasterDatabaseTests.cs`
- Kabul:
  - Semih pasif item mapping politikası seçer; yasaksa inactive target ile mapping yazılmadığı HTTP409+DB testle, serbestse hareket ret kuralı ile açıkça ayrıştırılır.

### RG-STK-29 — Product mapping FK tarihsel düzeltme teyidi

- Durum: historical-regression-only
- Kusurlar: STK-035
- Parentlar: F0698
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `database/migrations/V1/V1-RMD-156/097-database-integrity-constraints.up.sql`
  - `src/Host/Experience/Inventory/StockMasterEndpoints.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
- Kabul:
  - Tam production migration zinciri üstünde bilinmeyen product mapping reddedilir; FK kaldırma mutasyonu assertionı kırar; stock-master-only059 fixture bu güvenceyi test ediyor diye gösterilmez.

### RG-STK-30 — Master duplicate yarışının sözleşme teyidi

- Durum: verification-required
- Kusurlar: STK-036
- Parentlar: F0698
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/StockMaster/StockMasterService.cs`
  - `tests/Host/Experience/Inventory/StockMasterHttpTests.cs`
  - `tests/Modules/Inventory/StockMaster/StockMasterDatabaseTests.cs`
- Kabul:
  - Parallel same code create HTTP testinde tam bir create ve kalanlar409; mapping upsert yeniden gönderimde OK. Servis typed exception contractı gerekip gerekmediği belgelenir; gerekirse yalnız exact unique constraint çevrilir.

### RG-STK-32 — Production raw-read kaynak iddiası teyidi

- Durum: scope-review-required
- Kusurlar: STK-037, STK-038
- Parentlar: F0560
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `docs/architecture/module-dependency-rules.md`
  - `src/Modules/Production/StockEffects/ProductionStockEffectService.cs`
  - `tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`
  - `tests/Modules/Production/StockEffects/ProductionStockEffectsDatabaseTests.cs`
- Kabul:
  - Mimari sahiplik kuralı read vs write ayrımıyla değerlendirilir; sadece raw SELECT var diye gereksiz abstraction eklenmez; lock-order testi ayrı tutulur.
  - İki gerçek bağlantıda sale advisory locku tutarken production row-lock barrierına gelir; sale row write başlatılır, production advisory bekler. Mevcut kod40P01/timeout ile kırılır; ortak advisory→row düzeni sonrası ikisi tutarlı sıralı sonuçla tamamlanır; stock/ledger toplamı doğru.

### RG-STK-33 — Reservation bakım erişilebilirlik kararı

- Durum: decision-blocked
- Kusurlar: STK-010
- Parentlar: F0211
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/InventoryModule.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/IReservationBalanceProjector.cs`
  - `src/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjector.cs`
  - `tests/Modules/Inventory/ReservationBalanceProjection/ReservationBalanceProjectionDatabaseTests.cs`
- Kabul:
  - Rebuild/drift tutulma kararı gerçek operatör senaryosuna bağlanır; test ve DI kayıtları caller sayılmaz; seçilen yol yetki/hata/iptal ile kanıtlanır.

### RG-STK-34 — Manual adjustment erişilebilirlik kararı

- Durum: decision-blocked
- Kusurlar: STK-019
- Parentlar: F0230
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/InventoryModule.cs`
  - `src/Modules/Inventory/ManualAdjustments/IInventoryAdjustmentService.cs`
  - `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs`
  - `tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDatabaseTests.cs`
  - `tests/Modules/Inventory/ManualAdjustments/ManualAdjustmentDomainTests.cs`
- Kabul:
  - Kullanıcı gerçek ihtiyacı onaylar; integration seçilirse tam yeni giriş/contract/test yolları ayrıca planlanır, removal seçilirse yaşayan physical-count/waste davranışları korunur.

### RG-STK-35 — Tx’sız reversal kullanılmayan overload kararı

- Durum: decision-blocked
- Kusurlar: STK-016
- Parentlar: F0228
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `src/Modules/Inventory/MovementReversal/IStockMovementReversalService.cs`
  - `src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDatabaseTests.cs`
  - `tests/Modules/Inventory/MovementReversal/StockMovementReversalDomainTests.cs`
- Kabul:
  - Semih retain+gerçek caller gereksinimi veya remove seçer; gereksiz overload kalmaz; tx yolunun ledger/balance rollback testleri korunur.

### RG-SUPPLIER-ELIGIBILITY — Tedarikçi scrub uygunluğu

- Durum: scope-review-required
- Kusurlar: GYZ-010
- Parentlar: F0175
- Kaynak dependency önerisi: RG-CUSTOMER-ELIGIBILITY
- Existing yüzey:
  - `src/Host/Program.cs`
  - `src/Modules/Privacy/Anonymization/PostgresAnonymizationWorkflow.cs`
  - `src/Modules/Privacy/RetentionExecution/PostgresRetentionExecutionService.cs`
  - `src/Modules/Privacy/RetentionExecution/RetentionSelection.cs`
  - `tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs`
- Kabul:
  - Eski supplier queue sonrası yeni purchase order, invoice ve receipt ayrı ayrı eklenince alanlar değişmez.
  - Yeni faaliyet ve scrub yarışında commit sırası tutarlı; eski/uygun supplierde tax ID ve ledger korunur.

### RG-TOKEN — Token biçimlendirme sınırı

- Durum: scope-review-required
- Kusurlar: GYZ-001, GYZ-002
- Parentlar: F0002, F0055
- Kaynak dependency önerisi: Yok
- Existing yüzey:
  - `evidence/V1-IAM-013/issued_token_redaction_verification.txt`
  - `plan/v1/identity-authorization/V1-IAM-013-issued-token-redaction.md`
  - `src/Modules/Identity/Authentication/LoginResult.cs`
  - `src/Modules/Identity/Authentication/SessionTokenIssuer.cs`
  - `tests/Modules/Identity/Authentication/SessionTokenIssuerTests.cs`
- Kabul:
  - Sentinel token ile doğrudan ToString, string interpolation, object.ToString ve LoginSuccess.ToString içinde sentinel yok; mask var.
  - Exception/assertion mesajı nesne biçimlendirmesinden üretildiğinde sentinel yok. Token property ve hash/expiry davranışı değişmez; normal login başarılı kalır.
  - Eski kanıt silinmeden tarihsel/UNPROVEN düzeltme notu ekle; yeni SHA ve sentinel-red/green ham transcript iliştir.
  - Yeni raporu RG-TOKEN kabul matrisine satır bazlı bağla; tüm auth test sayısını sentinel kanıtı sayma.
