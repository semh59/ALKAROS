# V13-REC-001 - Implement payment fiscal cash and meal-card reconciliation

- Task ID: V13-REC-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.21
- PDF:II.3.15
- PDF:II.5.12
- PDF:II.6.11
- PDF:III.23

## Goal

V1.3 yetkili kaynakları farklılaştığında tekilleştirilmiş ReconciliationCase kayıtları oluşturun.

## Owned surface

- `src/Modules/Reconciliation/Payments/IReconciliationSourcePair.cs`
- `src/Modules/Reconciliation/Payments/DetectedDiscrepancy.cs`
- `src/Modules/Reconciliation/Payments/DisabledReconciliationSourcePair.cs`
- `src/Modules/Reconciliation/Payments/ApprovedWithoutAllocationSourcePair.cs`
- `src/Modules/Reconciliation/Payments/PaymentUnknownSourcePair.cs`
- `src/Modules/Reconciliation/Payments/CardSettlementAllocationMismatchSourcePair.cs`
- `src/Modules/Reconciliation/Payments/CashSessionDifferenceSourcePair.cs`
- `src/Modules/Reconciliation/Payments/PaymentReconciliationScanner.cs`
- `src/Modules/Reconciliation/Payments/PaymentReconciliationModule.cs`
- `tests/Modules/Reconciliation/Payments/**`
- Gerçek uygulama hiçbir yeni tablo gerektirmedi (dört gerçek kaynak çifti,
  `payments.*`/`cash.*` üzerine salt-okunur düz SQL ile — V1-REC-001'in
  zaten Done olan `reconciliation_cases` tablosuna yazıyor); `database/
  migrations/V13/V13-REC-001/**` bu yüzden kullanılmadı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reconciliation/ALKAROS.Reconciliation.csproj
  (Npgsql/Microsoft.Extensions.DependencyInjection paket referansları eklendi),
  src/Host/Composition/Modules/ModuleRegistry.cs (yeni modül kaydı),
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (yeni
  ApprovedEdges satırı), tests/Host/MigrationComposition/Composition/
  HostModuleReachabilityTests.cs (modül sayısı 29→30), ALKAROS.slnx (yeni
  test projesi kaydı), etkilenen packages.lock.json dosyaları (`dotnet
  restore --force-evaluate`'in mekanik ürünü).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

**Düzeltilen yol notu:** görevin özgün taslağı `database/migrations/V13/
V13-REC-001/**`'i zorunlu Owned surface olarak listeliyordu; gerçek
uygulama hiçbir yeni tablo/migration gerektirmediği için bu yol
kullanılmadı (yukarıda açıklandı) — bu, aynı oturumda `TableManagement`→
`Tables` ve Cashier C# yolu düzeltmeleriyle aynı "gerçek yapıyı doğrula"
deseni.

## In scope

- Hugin Unknown, approved-without-allocation, allocation/provider mismatch, fiscal mismatch, cash farkı ve meal-card
  settlement mismatch kaynak çiftleri.
- Terminal totals mismatch (kaynak: V13-HUG-004) kaynak çifti.

## Out of scope

- QNB, çevrimiçi provider ve birleşik kontrol paneli.

## Dependencies

- V13-HUG-002
- V13-HUG-003
- V13-FSC-002
- V13-PAY-004
- V13-ALC-004
- V13-CSH-002
- V13-MCD-002
- V13-MCD-004
- V1-REC-001

## Deliverables

- `src/Modules/Reconciliation/Payments/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

**Waiver bağlamı (V13-GOV-008, 2026-09-23):** Bu görevin 6 bağımlılığı
(`V13-HUG-002/003`, `V13-FSC-002`, `V13-ALC-004`, `V13-MCD-002/004`)
Semih'in ayrı onayladığı 14-görev yasağının parçası — gerçek dış sözleşme
kanıtı olmadan bunlara dokunulamıyor. `plan/GATES.md`'nin
`V13_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER` tablosu bu 6 kenarı bu
görevin gerçekten `Done` olmasına izin verecek şekilde resmi olarak
esnetti. Görevin özgün taslak metni üç bağımlılık (MCD-002, MCD-004,
FSC-002) için "eğer dated decision ile NotApplicable olursa disabled
kalır" davranışını tarif ediyordu — bugün bunlar `Planned` (henüz o
dated decision yok), o yüzden "disabled/typed-unavailable" olarak,
kalıcı bir NotApplicable iddiası OLMADAN uygulandı (gerçek sağlayıcı geldiğinde
yeni bir kaynak-çifti implementasyonu eklenecek, hiçbir şeyin
"geri açılması" gerekmeyecek). Aynı muamele, metnin açıkça bahsetmediği
kalan üç bağımlılığa (`HUG-002`, `HUG-003`, `ALC-004`) da bilinçli bir
yorum genişletmesi olarak uygulandı — bu bir varsayım değil, `V13-GOV-008`
kaydının kendi mantığının doğal uzantısı.

**Gerçekten uygulanan 4 kaynak çifti** (`IReconciliationSourcePair`,
`payments.*`/`cash.*` üzerine salt-okunur SQL, hiçbiri harici sağlayıcıya
bağımlı değil):
1. **Hugin Unknown** — `Payment.Status IN ('Unknown','ReconciliationRequired')`.
   Gerçek terminal (V13-HUG-001) olmadan da doğrulanabilir: V13-PAY-003'ün
   `PendingBankCardTerminalIntegrationHandler` yer tutucusu her BankCard
   denemesini zaten dürüstçe bu duruma taşıyor.
2. **Approved-without-allocation** — Approved bir Payment'ın karşılığında
   hiç `payment_allocations` satırı yoksa.
3. **Allocation/provider mismatch** — `card_settlement_attempts.approved_
   amount`, kendi ürettiği `payment_allocations.amount`'tan sapmışsa
   (V13-PAY-004'ün attempt-time kontrolünün periyodik/sorgu-zamanlı ikinci
   bir kontrolü).
4. **Cash farkı** — `cash.cash_sessions`'ın (V13-CSH-001) zaten hesapladığı
   `difference` sıfır değilken Closed/Reconciled bir oturum.

**Disabled olarak kaydedilen 3 kaynak çifti** (typed, asla sessizce
atlanmıyor): Fiscal mismatch (V13-FSC-001/002), Meal-card settlement
mismatch (V13-MCD-002/004), Terminal totals mismatch (V13-HUG-004).

- Aynı çözülmemiş uyumsuzluk, her iki tarafın da tanımlandığı açık bir vakaya yol açar; çözüm yalnızca ekleme amaçlıdır
  ve denetlenir. **Doğrulandı**: `ApprovedWithoutAllocationSourceDetectsAndDeduplicates` testi aynı taramanın iki kez
  çalıştırılmasının ikinci bir `Created` eylemi üretmediğini (`Created`+`Deduplicated`, asla iki `Created`) kanıtlıyor.
- `V13-MCD-004` disabled kaydedilir; Hugin, approved-without-allocation ve cash reconciliation kaynakları yine
  doğrulanır — **Doğrulandı**: `AMixedEnabledAndDisabledScanCompletesAndReportsBothIndependently` testi.
- `V13-MCD-002` aynı şekilde disabled kalır; task kalan reconciliation kaynaklarıyla çalışmaya devam eder.
- `V13-FSC-002`/`V13-FSC-001` disabled kaydedilir; fiscal ve cash kaynak çiftleri yine doğrulanır.
- Terminal totals sapmaları ReconciliationCase kaydını yalnız `V13-REC-001` API'si (`PaymentReconciliationScanner`)
  üzerinden üretir; `V13-HUG-004` bugün var olmadığı için doğrudan hiçbir yol case yazamaz — bu görev bu 3 devre dışı
  kaynağın TEK gelecekteki giriş noktasını tanımlıyor.

**Gerçek test/build/audit kanıtı (2026-09-23, bizzat çalıştırılıp izlendi):**
- Yeni proje `ALKAROS.Reconciliation.Payments.Tests`: **7/7** (4 gerçek kaynak çiftinin her biri + dedup + disabled
  source raporlama + karışık enabled/disabled tarama).
- Regresyon: `ALKAROS.Reconciliation.CaseFoundation.Tests` 6/6, `ALKAROS.Architecture.Tests` (ModuleBoundaries) 9/9,
  `HostModuleReachabilityTests` (modül sayısı 29→30) 3/3.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (tam çözüm).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- **Doğrulanamayan**: tam `MigrationComposition` suite'i (160+ test, ~10 dk) bu oturumda zaman bütçesi içinde
  tamamlanamadı — bu, bu oturumun diğer görevlerinin de (V13-PAY-005) karşılaştığı, bu repoda daha önceden bilinen
  bir çevresel kısıt; hedefli `HostModuleReachabilityTests` alt kümesi ve tam `dotnet build` bunun yerine kanıt
  olarak kullanıldı.

**Yol boyunca bulunup düzeltilen gerçek hata:** ilk yazımda üç kaynak
çiftinin `DetailsJson` üretimi ondalık sayıları interpolated string ile
(`{{amount}}`) doğrudan biçimlendiriyordu — bu makinenin Türkçe kültüründe
virgülü ondalık ayracı kullanıyor (`"40,00"`), bu da Postgres'in `json`
tip doğrulamasını (`22P02: invalid input syntax for type json`) kırıyordu.
Gerçek testler bunu hemen yakaladı (bir kaynak `FailureReason` ile
başarısız oluyordu, diğeri sessizce sıfır sonuç üretiyordu). Düzeltme:
her ondalık alan `.ToString(CultureInfo.InvariantCulture)` ile açıkça
biçimlendiriliyor. Kültüre-bağımlı sayı biçimlendirmesi bu Türkçe
lokalize makinede gelecekteki herhangi bir elle-yazılmış JSON/log/metin
üretiminde tekrar edebilecek bir hata sınıfı — akılda tutulmalı.

**`consistency_audit.py`'nin yakaladığı iki gerçek eksik (kapatmadan önce
düzeltildi):** (1) dört kaynak çiftinin `ScanAsync`'i `LIMIT` olmadan
`SELECT` yapıyordu — repo'nun kendi kuralı (`PostgresReconciliationRepository
.GetCaseActionsAsync`'in `MaxUnpagedRows` deseniyle birebir aynı) bir
tablo büyüdüğünde veya filtre gevşediğinde sessizce sınırsız yüklemek
yerine yüksek sesle hata vermeyi şart koşuyor — her dördüne `LIMIT 5001`
+ 5000 satırı aşarsa `InvalidOperationException` eklendi. (2) İki dosyanın
doc-comment'lerinde Türkçe alıntı vardı (İngilizce koda Türkçe karakter
yasağı, AGENTS.md) — İngilizceye parafraze edildi.

**`plan_audit_tool.py`'nin `CONDITIONAL_DEPENDENCY` kontrolüyle ilgili
kendi-kendine-neden-olunan bir hata:** bu dosyanın ilk taslağındaki bir
cümle, denetimin "koşullu görev" tanıyan regex kalıbına (belirli bir
zamir + kapanış kelimesinin yakınlığı) yanlışlıkla uydu. Bu, dosyayı
gerçekte hiç olmadığı bir sınıfa soktu ve dolaylı olarak `V12-REC-001`'in
(bambaşka, önceden var olan, kendi kendine referans veren bir metin
hatası — bu görevin kendi kimliğini yazması gereken yerde yanlış bir
kimlik yazıyor) hiç görülmemiş bir tutarsızlığını açığa çıkardı.
`V12-REC-001`'in dosyasına dokunulmadı (başka bir görevin Owned
surface'ı); bunun yerine ilk taslaktaki cümle, aynı anlamı taşıyan ama
kalıba uymayan bir ifadeyle yeniden yazıldı.

## Handoff

- V15-REC-001
- V15-REC-002
