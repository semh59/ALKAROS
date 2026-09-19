# V1-RMD-248 - Fix Cash/Payments module-boundary architecture test drift

- Task ID: V1-RMD-248
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`'un
tam koşusu (bu görev serisinin, V1-RMD-244'ün kendi değişikliğini
doğrulamak için) çalıştırılırken üç bağımsız, bu görevin kendi
değişikliğinden kaynaklanmayan (`git status` ile doğrulandı, bu dosyalar
oturumun bu bölümünden önce hiç değiştirilmemişti) test hatası bulundu:

1. `HostOrchestrationEdgesStayWithinTheApprovedList` — V1-RMD-244'ün kendi
   `ALKAROS.Audit` referansından kaynaklanan gerçek regresyon; ayrı olarak
   zaten düzeltildi (V1-RMD-244'ün kendi Owned surface'ında kayıtlı).
2. `DeclaredDependenciesStayWithinTheApprovedEdgeList` — `CashSessionLifecycleModule`,
   `CashTransactionLedgerModule`, `CashTenderHandlerModule` ve
   `PaymentAllocationPersistenceModule` (hepsi V13-CSH-001/002/003/004 ile
   gerçek, zaten `ModuleRegistry.DefaultCatalog`'a kayıtlı modüller) hiçbir
   zaman testin `ApprovedEdges` sözlüğüne eklenmemişti — bu sözlük
   oluşturulduğundan beri (`Activator.CreateInstance` ile tüm katalog
   modülleri örneklendiği an) bu paketin ilk `dotnet test` koşusunda
   patlıyordu.
3. `ActualAssemblyDependenciesAreDeclaredInDependsOn` — testin kendi
   `assembly adı -> tek modül id` sözlük inşası, aynı fiziksel derlemeyi
   (`ALKAROS.Cash.csproj`, `ALKAROS.Payments.csproj`) paylaşan birden çok
   `IModule`'ün (kasıtlı bir desen — bir görevin başka bir görevin modül
   dosyasına yazmasını önler) ikinci anahtarında `ArgumentException` ile
   patlıyordu. Bu düzeltilince, gerçek bir ikinci bulgu ortaya çıktı:
   `CashTenderHandlerModule`'ün kendi kaynak dosyası (`CashTenderHandler.cs`)
   `ALKAROS.Billing.BillFoundation` içe aktarıyor (tender allocation
   hedefi için Bill okuması), ama kendi `DependsOn` listesi hiç `Billing`
   içermiyordu — gerçek, önceden var olan bir V0-ARC-001 ihlaliydi
   (undeclared derleme bağımlılığı), yalnız test kırığı değil.

## Owned surface

- `evidence/V1-RMD-248/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (V0-ARC-001/V1-RMD-159 sahipliğinde kalır) — `ApprovedEdges`'e
  `Cash.SessionLifecycle`/`Cash.TransactionLedger`/`Cash.TenderHandler`/
  `Payments.Allocations.Persistence` eklendi; `ActualAssemblyDependenciesAreDeclaredInDependsOn`
  testinin gövdesi, derleme referanslarını modül başına değil bir
  derlemenin barındırdığı TÜM modüllerin `DependsOn` birleşimine göre
  kontrol edecek şekilde yeniden yazıldı (paylaşılan derleme deseniyle
  uyumlu olması için); test mantığının amacı değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TenderHandler/CashTenderHandlerModule.cs
  (V13-CSH-003 sahipliğinde kalır) — `DependsOn`'a gerçek, kullanılan
  `Billing` eklendi; bayat "not yet added to DefaultCatalog" yorumu
  gerçek duruma (V13-CSH-004 ile zaten kayıtlı) güncellendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md
  (V0-ARC-001 sahipliğinde kalır) — satır 7 (Cash) "Bill (tender
  allocation target)" ile genişletildi.

## In scope

- Test-only ve dokümantasyon düzeltmesi: gerçek, zaten Done olan
  V13-CSH-001/002/003/004 modül kenarlarını `ApprovedEdges`'e ve karar
  kaydına yansıtmak.
- `CashTenderHandlerModule.DependsOn`'a eksik `Billing` bağımlılığını
  eklemek (gerçek kod bağımlılığı zaten vardı, yalnız beyan eksikti).

## Out of scope

- `ApprovedEdges`/`ActualAssemblyDependenciesAreDeclaredInDependsOn`'un
  altta yatan "paylaşılan derleme + tek girişli sözlük" tasarım tuzağının
  gelecekte tekrar oluşmasını yapısal olarak imkânsız kılacak daha büyük
  bir refactor — kapsamı aşan ayrı bir mimari karar, bu görev yalnız
  mevcut drift'i ve gerçek eksik bağımlılığı düzeltir.

## Dependencies

- None

## Acceptance evidence

- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
  → 9/9 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
