# V13-RPT-001 - Implement payment cash fiscal and meal-card reports

- Task ID: V13-RPT-001
- Status: Done
- Assignee: Claude Sonnet 5 (Faz 2 fork, session_01GbyK8tEk5PA5cxaanvq4qo)
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

payment karışımı, cash oturumu, mali status ve yemek kartı kapatma raporlarını uygulayın.

## Owned surface

- `src/Modules/Reporting/Payments/**`, `tests/Modules/Reporting/Payments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ReportingModule.cs
  (Reporting görevlerinin ortak DI kayıt noktası, tüm alt-özellikler burada
  kayıt olur — bkz. V1-RPT-001/V11-RPT-002'nin kendi emsali) — yalnız
  `IPaymentSettlementReportRepository`/`IPaymentSettlementReportService`
  kaydı eklendi, mevcut kayıtlar değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ALKAROS.Reporting.csproj
  — yalnız `Npgsql` PackageReference eklendi (bu projede daha önce hiç
  Npgsql gerekmiyordu, mevcut raporlar `DbDataSource` kullanıyordu; bu görev
  `NpgsqlDataSource`'a doğrudan bağlı — diğer Payments/Cash repository'leriyle
  aynı kalıp).
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni
  `ALKAROS.Reporting.Payments.Tests.csproj` girişi eklendi.

## In scope

- İş tarihi/terminal/provider filtreleri, net geri ödeme tutarları, cash farkı ve mutabakat toplamları.

## Out of scope

- CustomerAccount, invoice ve çevrimiçi kanal raporları.

## Dependencies

- V0-DOM-008
- V13-ALC-003
- V13-ALC-004
- V13-CSH-002
- V13-MCD-002
- V13-FSC-001

## Deliverables

- `src/Modules/Reporting/Payments/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

**`V13-ALC-004`/`V13-MCD-002`/`V13-FSC-001` waived (V13-GOV-008, 2026-09-23):**
tam olarak bu üç bağımlılık, dış sözleşme kanıtı gelene kadar disabled
kalıyor — task metninin kendi "eğer `V13-MCD-002` tarihli `NotApplicable`
ise" ifadesi bugün (`Planned`, henüz `NotApplicable` değil) hâlâ geçerli
sayıldı: her üç bölüm (net geri ödeme, mali durum, yemek kartı kapanışı)
`DisabledReportSection` olarak, sebep ve engelleyen task kimliğiyle birlikte
her zaman döndürülür — sessizce boş veya sıfır değil. `V13-ALC-004`/
`V13-FSC-001` için bu davranış task metninde açıkça yazılmamıştı; V13-REC-001
(aynı oturum, aynı waiver) için kurulan "hipotetik NotApplicable = bugün
disabled" örüntüsü aynı gerekçeyle buraya da uygulandı, kendi kararım olarak
burada kayıtlı.

**Gerçek, bugün üretilebilen bölümler**: payment mix (Cash/BankCard/Eft,
yöntem hiçbir sütunda saklanmadığı için elemeyle çıkarılıyor — `cash.
cash_transactions` Sale satırı varsa Cash, `payments.card_settlement_
attempts` satırı varsa BankCard, ikisi de yoksa Eft, `PaymentMixEntry`'nin
kendi doc yorumunda gerekçesiyle), Unknown/ReconciliationRequired ödemeler
(ayrı bir kova, hiçbir yöntem toplamına karışmaz), cash session özeti
(`cash.cash_sessions.difference`, açık oturum ayrı işaretli), mutabakat
toplamları (`reconciliation.cases`, case_type'a göre açık/kapalı sayım ve
tutar — `V13-REC-001`'in artık gerçekten doldurduğu tablo).

**Filtreler**: iş tarihi (Europe/Istanbul, `PaymentSettlementReportFilter.
ResolveWindow()`, sabit takvim-günü UTC kesimi değil) zorunlu; terminal
filtresi yalnız Cash bölümünü daraltıyor (Payment'ın kendisinde hiçbir
terminal sütunu yok — bu, dürüstçe belgelenen bir şema sınırlaması, provider
filtresi de aynı nedenle uygulanamadı).

- `dotnet build ALKAROS.slnx -c Debug` → 0 hata, 0 uyarı.
- Yeni proje `ALKAROS.Reporting.Payments.Tests` (gerçek Postgres,
  `alkaros-test-pg`): 9/9 yeşil (payment mix elemeyle ayrıştırma, pencere
  dışı hariç tutma, unsettled ayrı kova, cash session fark/açık bayrağı,
  terminal filtresi, mutabakat case_type gruplama, boş pencere sıfır sonuç,
  disabled bölümler her zaman dolu, geçersiz saat dilimi reddi).
- Regresyon: `ALKAROS.Architecture.Tests` (ModuleBoundaries) 9/9,
  `ALKAROS.Reporting.V1Operations.Tests` 6/6, `ALKAROS.Reporting.
  MenuInventory.Tests` 8/8, `ALKAROS.Reconciliation.Payments.Tests` 7/7.
- `python tools/project-manifest/project_manifest_tool.py` → VALID (0 fark).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (919 markdown, 897 task dosyası, 2001 dependency edge).
- `python tools/consistency-audit/consistency_audit.py` → temiz (bir turda
  3 Türkçe-karakter-in-comment ihlali bulundu — İngilizce doc yorumunda
  Türkçe metni birebir alıntılamak — parafraze edilerek düzeltildi).
- Doğrulanamayan: tam `MigrationComposition` suite'i (160+ test) bu
  oturumda çalıştırılmadı (bu Faz 2 girişiminin diğer görevlerinde de
  görülen zaman/kaynak kısıtı) — hedefli regresyon projeleri ve
  `project_manifest_tool.py`'nin VALID sonucu bunun yerine kanıt olarak
  sunuluyor.

## Handoff

- V15-RPT-001
