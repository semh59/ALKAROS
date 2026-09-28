# V14-CST-002 - Implement customer anonymization state transitions

- Task ID: V14-CST-002
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

Legal olarak korunan financial reference'ları silmeden Requested, RetentionBlocked, Pending ve Anonymized durumlarını
uygulamak.

## Owned surface

- `src/Modules/CustomerData/AnonymizationState/**`, `tests/Modules/CustomerData/AnonymizationState/**`,
  `database/migrations/V14/V14-CST-002/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/ALKAROS.CustomerData.csproj,
  src/Modules/CustomerData/CustomerDataModule.cs (V14-CST-001 sahipliğinde kalır) — yalnız
  `ALKAROS.Audit` referansı ve yeni servis kayıtları eklenir, `DependsOn` `[]` → `["Audit"]` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md
  (V0-ARC-001 sahipliğinde kalır) — yalnız satır 30'a Audit kenarı eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `ApprovedEdges["CustomerData"] = ["Audit"]` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `160` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"159"` → `"160"` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız sabit migration sayısı `158` → `159`, `RuntimeManifestIds`'e
  `"160"`, `LastEntryTables`'a `"anonymization_requests"` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-278 sahipliğinde kalır) — yalnız bu görevin 5 yeni tipi eklenir (bilinçli olarak hiçbir
  HTTP endpoint eklenmiyor, V14-CST-001 ile aynı gerekçe).
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `AnonymizationRequestStatus`/`CustomerAnonymizationTransitions`: Requested (geçici, hiç
  kalıcılaşmaz) → {RetentionBlocked, Pending} → Anonymized (terminal, V0-DOM-001'in "terminal
  durumlar sessizce yeniden açılmaz" kuralı). Açık, wildcard olmayan geçiş matrisi.
- `IAnonymizationRetentionGuard`/`NoKnownBlockingReferencesGuard`: finansal referans sahibi bir
  modülün (V14-ACC/V14-INV, ikisi de henüz `Planned`) bir engelleme nedeni raporlayacağı genişletme
  noktası; şu an dürüst bir yer tutucu (hiçbir zaman engellemiyor) — Faz 2'nin "RequiresReconciliation
  yer tutucu" deseniyle aynı gerekçe.
- `ICustomerAnonymizationRequestStore`/`PostgresCustomerAnonymizationRequestStore`: optimistic
  concurrency ile CRUD, iş kuralı doğrulaması yapmaz (bu servis katmanının işi).
- `CustomerAnonymizationService`: `RequestAsync` (guard kontrolü + Requested→{Blocked,Pending} tek
  çağrıda), `ReevaluateAsync` (RetentionBlocked→Pending, idempotent no-op), `ExecuteAsync`
  (Pending→Anonymized, `ICustomerProfileStore.AnonymizeAsync`'e delege eder, idempotent). Her geçiş
  `ALKAROS.Audit`'e bir `AuditEvent` yazar (CustomerAnonymizationRequested/Blocked/Unblocked,
  CustomerAnonymized) — denetim event gereksinimi.
- Bilinçli olarak HİÇBİR HTTP endpoint eklenmedi — V14-CST-001 ile aynı gerekçe.

## Out of scope

- Modüller arası yük temizleme ve planlı saklama yürütme.

## Dependencies

- V14-CST-001
- V1-OPS-001
- V0-DOM-001

## Deliverables

- `src/Modules/CustomerData/AnonymizationState/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrarlanan anonimleştirme stabildir; alıkoyma-blocked isteği PII'yi korur ve nedenini kaydeder; izin verilen istek
  yalnızca yapılandırılmış alanları kaldırır.
- `dotnet test tests/Modules/CustomerData/AnonymizationState/ALKAROS.CustomerData.AnonymizationState.Tests.csproj`
  (gerçek Postgres'e karşı — profil store, request store VE audit store'un hepsi gerçek; yalnızca
  saklama guard'ı kontrollü bir test double, çünkü gerçek engelleyici sahibi henüz kod olarak yok):
  35/35 geçti. Kapsanan: engelsiz istek doğrudan Pending'e gider ve denetlenir; engelli istek
  RetentionBlocked'a gider, nedeni kaydeder ve denetlenir; hâlâ engelliyken yeniden değerlendirme
  hiçbir şeyi değiştirmez; engel kalkınca yeniden değerlendirme Pending'e taşır; Pending'i
  çalıştırmak profili GERÇEKTEN anonimleştirir (yalnızca 4 yapılandırılmış alan, kayıt/id hayatta
  kalır) ve denetlenir; zaten Anonymized olan bir isteği çalıştırmak idempotent'tir; hâlâ
  RetentionBlocked bir isteği çalıştırma girişimi reddedilir (`CustomerAnonymizationInvalidTransitionException`).
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 geçti
  (`CustomerData` → `Audit` kenarı `ApprovedEdges`'e eklendi).
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (hedefli, sistemin bellek
  baskısı altında olması nedeniyle yalnızca sayıma duyarlı iki test): 2/2 geçti (34 modül, 159
  migration).
- `dotnet build src/Modules/CustomerData/ALKAROS.CustomerData.csproj` ve
  `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz (5 yeni tip
  `unreachable_services_allowlist.json`'a eklendi — V14-CST-001 ile aynı gerekçe).
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences across
  Solution, Disk, and ProjectReferences)`.

## Handoff

- V15-KVK-001
- V15-KVK-002
