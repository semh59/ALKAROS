# V13-PAY-005 - EFT/Havale tender handler'ı uygula

- Task ID: V13-PAY-005
- Status: Done
- Assignee: Claude Sonnet 5 (fork a9be63ffd6a772f7e)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

Cash/BankCard/MealCard'ın yanına dördüncü bir tender yöntemi eklemek: EFT/
Havale. Provider entegrasyonu YOK — kasiyer tutarı işletmenin banka hesap
hareketinde gözle görüp beyan eder, sistem yalnız bu beyanı kaydeder. Bu
yöntem PDF baseline'da yoktu; Semih'in doğrudan ürün kararıdır
(kasa-onizleme artifact review, 2026-09-16).

## Owned surface

- `src/Modules/Payments/EftTender/**`, `tests/Modules/Payments/EftTender/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/TenderRouting/TenderMethod.cs
  (V13-PAY-002 sahipliğinde) — yalnız `Eft` enum üyesi eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/TenderRouting/TenderRequest.cs
  (V13-PAY-002 sahipliğinde) — yalnız isteğe bağlı `Note` alanı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/TenderComposition/TenderHandlerRegistryFactory.cs,
  src/Modules/Payments/TenderComposition/TenderCompositionModule.cs
  (V13-PAY-003 sahipliğinde) — yalnız Eft handler'ının registry'ye kaydı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/TenderComposition/TenderCompositionTests.cs
  (V13-PAY-003 sahipliğinde) — yalnız yeni `Build` imzasına üçüncü argüman eklendi, Eft
  çözümlemesi için bir assertion genişletildi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx, src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs,
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (yeni `EftTenderModule`'ün kataloğa/mimari sınır listesine/sabit modül sayısına eklenmesi).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- `Eft` tender tipi: tutar, isteğe bağlı serbest metin not alanı (referans
  numarası ZORUNLU DEĞİL — Semih'in tercihi), kasiyer kimliği, zaman damgası.
- V13-PAY-002'nin typed tender request/handler contract'ına yeni bir case
  olarak eklenir (Cash/BankCard/MealCard ile aynı desende).
- V13-PAY-003'ün fail-closed registry'sine kayıt.
- Para üstü YOK — tutar her zaman tam kalan miktar kadar veya daha az
  uygulanır, asla aşamaz (Cash'in aksine).
- Kasa nakit çekmecesiyle/`CashSession`'la HİÇBİR ilgisi yok — vardiya
  açık olmasa da kullanılabilir (bu, Nakit'in aksine; Nakit
  `RecordTransaction` için oturum `Open` şartına bağlı, EFT değil).

## Out of scope

- Banka API/Open Banking entegrasyonu, otomatik mutabakat, gerçek zamanlı
  hesap hareketi doğrulama — hepsi büyük, ayrı bir workstream; bu görev
  yalnız kasiyer beyanını kaydeder.
- Referans numarası zorunluluğu veya format doğrulaması.
- UI — V13-PUI-004'ün kapsamı.

## Dependencies

- V13-PAY-001
- V13-PAY-002
- V13-PAY-003

## Acceptance evidence

Uygulama: `EftTenderHandler` (Cash'in advisory-lock/replay desenini tekrar
kullanır, ama `ICashSessionRepository`/`CashSession`'a hiç referans vermez —
`EftTender` klasörü `ALKAROS.Cash` derlemesine bağımlı bile değildir).
`Eft` tender tipi `TenderMethod`'a eklendi, `TenderRequest`'e isteğe bağlı
`Note` alanı eklendi (Payment'ın kendi `PaymentStatusHistoryEntry.reason`
alanına taşınır — yeni migration/tablo gerekmedi). `V13-PAY-003`'ün
`TenderHandlerRegistryFactory`/`TenderCompositionModule`'üne üçüncü bir
handler olarak kaydedildi (Cash/BankCard'ın yanına).

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (kişisel olarak
  çalıştırılıp görüldü).
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432) yeni proje
  `ALKAROS.Payments.EftTender.Tests`: **11/11** — başarılı tam tutar
  tahsili (para üstü sıfır), kalanın altında kısmi tutar (yine para üstü
  sıfır), kalanı aşan tutarın reddi (`EftOverTenderException`, hiçbir kayıt
  oluşmadan), idempotent tekrar gönderim, gerçek eşzamanlı çift gönderim,
  isteğe bağlı not alanının dolu/boş/null üç halinin de kabulü, eksik
  BillId'nin reddi, var olmayan Bill'in reddi.
- **CashSession bağımsızlığı, planlanandan daha güçlü şekilde kanıtlandı**:
  görev metni "vardiya kapalıyken EFT'nin yine de kabul edilmesi"ni
  istiyordu; test veritabanı bunun yerine `cash_sessions`/`cash_transactions`
  migration'larını HİÇ uygulamadan kuruldu — yani bir CashSession satırı
  oluşturmak bu test DB'sinde zaten mümkün değil. Bu, "kapalı oturumla da
  çalışır" iddiasından daha güçlü bir kanıt: EftTenderHandler'ın herhangi
  bir CashSession durumuna (açık/kapalı/yok) bağımlı OLMADIĞINI, şemanın
  kendisi bile mevcut değilken çalıştığını gösteriyor. Ayrıca
  `EftTenderHandler`'ın tek constructor'ının hiçbir `ALKAROS.Cash.*` tipi
  parametre almadığı ayrı bir reflection testiyle de doğrulandı.
- Regresyon: `Payments.TenderComposition` 8/8 (Eft'in registry'ye kaydı ve
  çözümlenmesi dahil yeni assertion'larla), `Payments.TenderRouting` 28/28,
  `Cash.TenderHandler` 6/6, `Payments.CardSettlement` 8/8,
  `Architecture.ModuleBoundaries` 9/9 — hepsi kişisel olarak çalıştırılıp
  yeşil görüldü.
- **Doğrulanamayan tek adım**: tam `MigrationComposition` süiti (160+ test)
  arka planda çalışırken sistem düşük bellek nedeniyle harici olarak
  durduruldu (gerçek test hatası değil — bu ortamda daha önce de görülen,
  bilinen bir kalıp). Talimat gereği kendiliğimden tekrar başlatılmadı;
  yukarıdaki hedeflenmiş projelerin hepsinin yeşil olması ve
  `project_manifest_tool.py`'nin VALID dönmesiyle DI kompozisyonunun
  gerçekten sağlam olduğuna dair dolaylı ama güçlü kanıt var, ama tam
  süitin kendisi bu oturumda çalıştırılıp GÖRÜLMEDİ.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (919 markdown, 897 task dosyası, 2001 dependency edge).
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID (0 fark).

## Handoff

- V13-PUI-004
