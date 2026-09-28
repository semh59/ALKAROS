# V14-ACC-002 - Implement CustomerAccount balance projection

- Task ID: V14-ACC-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Değişmez hesap defterinden mevcut bakiyeyi ve tarihli anlık görüntüleri hesaplayın.

## Owned surface

- `src/Modules/CustomerAccounts/BalanceProjection/**`, `tests/Modules/CustomerAccounts/BalanceProjection/**`,
  `database/migrations/V14/V14-ACC-002/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/CustomerAccountsModule.cs
  (V14-ACC-001 sahipliğinde kalır) — yalnız iki yeni servis kaydı eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `162` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"161"` → `"162"` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız sabit sayılar güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde kalır) — yalnız
  yeni bir `<Project Path>` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-278 sahipliğinde kalır) — yalnız bu görevin 4 yeni tipi eklenir (bilinçli olarak hiçbir
  HTTP endpoint eklenmiyor, V14-ACC-001 ile aynı gerekçe).
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `IAccountBalanceProjection`/`PostgresAccountBalanceProjection`: `customer_account.balances` —
  atomik projeksiyon güncellemesi, GERÇEK bir Postgres `apply_transaction_to_balance` tetikleyicisi
  ile sağlanıyor (`account_transactions` üzerine `AFTER INSERT`, aynı transaction içinde,
  V0-DAT-004'ün "Same tx" kuralı). `RebuildAsync`: defterden tam yeniden hesaplama — projeksiyonun
  gerçekten türetilmiş olduğunu kanıtlıyor.
- `IBalanceSnapshotStore`/`PostgresBalanceSnapshotStore`: `customer_account.balance_snapshots` —
  (customer_id, snapshot_date) benzersiz anahtarı, idempotent alma (aynı gün ikinci bir deneme
  ilk bakiyeyi korur, sessizce üzerine yazmaz). Yaşlanma temeli: her müşteri için tarihli, kalıcı
  bir bakiye geçmişi — ileride bir yaşlanma/tahsilat raporu bunun üzerine kurulabilir.

## Out of scope

- Yeni hesap işlemlerinin yayınlanması ve invoice kaynak seçimi.

## Dependencies

- V14-ACC-001
- V0-DAT-004

## Deliverables

- `src/Modules/CustomerAccounts/BalanceProjection/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projeksiyonun silinmesi/yeniden oluşturulması mevcut dengeyi ve anlık görüntüleri yeniden üretir; karma borç/alacak
  örneği beklenen sonuçla eşleşiyor.
- `dotnet test tests/Modules/CustomerAccounts/BalanceProjection/ALKAROS.CustomerAccounts.BalanceProjection.Tests.csproj`
  (gerçek Postgres'e karşı): 12/12 geçti. Kapsanan: tek bir ledger kaydı, HİÇBİR balans API'si
  çağrılmadan, tetikleyici aracılığıyla `balances` satırını otomatik oluşturuyor; karar kaydının
  kendi karma borç/alacak örneği (100+150-80=170 → -20 ayarlamayla 150 → +30 iadeyle 120) birebir
  yeniden üretildi; `last_transaction_at` asla geriye gitmiyor; `RebuildAsync` tetikleyicinin
  hesapladığı değeri birebir yeniden üretiyor; elle bozulmuş bir projeksiyon satırı `RebuildAsync`
  ile doğru değere geri dönüyor (gerçek "drift" kurtarma senaryosu); işlemi hiç olmayan bir
  müşteriyi rebuild etmek satırı kaldırıp sıfır döndürüyor. Anlık görüntü: aynı gün ikinci bir
  alma denemesi ilk bakiyeyi koruyor (idempotent); farklı tarihler gerçek ayrı kayıtlar; sıralama
  en eskiden yeniye.
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 geçti.
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (hedefli, sistemin
  bellek baskısı altında olması nedeniyle yalnızca sayıma duyarlı test): 1/1 geçti (161 migration;
  modül sayısı değişmedi — yeni bir `IModule` eklenmedi).
- `dotnet build src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj` ve
  `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences across
  Solution, Disk, and ProjectReferences)`.

## Handoff

- V14-INV-001
- V14-ACC-005
- V14-ACC-006
