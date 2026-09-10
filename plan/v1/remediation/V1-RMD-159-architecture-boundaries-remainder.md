# V1-RMD-159 - Mimari sınırlar bölümünün dört bulgusu

- Task ID: V1-RMD-159
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Mimari sınırlar bölümündeki
4 bulguyu kapatır (2 gerçek bulgu + 2 kör nokta):

1. **`ModuleBoundaryTests` yalnız `src/Modules/**`'ı tarıyor** —
   `src/Host/Experience/**` altına yazılan yeni bir modüller-arası bağ
   (`OrderStockConsumptionService`, `SentItemVoidStore` — Host'tan birden
   fazla modülün public sözleşmesini tek bir aynı-transaction akışta
   çağıran orkestratörler) hiçbir zaman onaylı-kenar denetiminden
   geçmiyordu; bu modüller `ModuleRegistry.DefaultCatalog`'da kayıtlı
   değil, dolayısıyla mevcut iki test (`DeclaredDependenciesStayWithinTheApprovedEdgeList`,
   `ActualAssemblyDependenciesAreDeclaredInDependsOn`) onları hiç görmüyordu.
   Yazımlar zaten her hedef modülün kendi repository sözleşmesinden geçtiği
   için bu bir V0-ARC-001 ihlali değildi — ama kenar hiç kayıtlı ve hiç
   denetlenmiyordu, sessizce genişleyebilirdi. Yeni bir NetArchTest tabanlı
   test (`HostOrchestrationEdgesStayWithinTheApprovedList`) eklendi; her
   orkestratörün kendi ad alanı, onaylı modül listesinin dışına
   bağımlılık alamıyor. Bilerek yanlış bir onaylı liste ile çalıştırılıp
   testin gerçekten düştüğü görüldü (vacuous değil), sonra doğru listeye
   geri alındı.
2. **`consistency_audit.py` yalnız ham SQL'de şema adı arıyor** — repository
   üzerinden yapılan çapraz modül yazımını (yukarıdaki gibi) göremiyor.
   Bu sınır artık `docs/CONSISTENCY_AUDIT.md`'de yazılı: bunun neden bir
   izin boşluğu değil bir *kayıt* boşluğu olduğu, ve hangi test'in
   (`HostOrchestrationEdgesStayWithinTheApprovedList`) bu sınıfı gerçekten
   kapattığı.
3. **Ölü istemci motorları: `WaiterManagerDecisionEngine` ve
   `WaiterOrderStatusEngine`** — yalnız kendi test projelerine derleniyordu,
   hiçbir yerden referans alınmıyordu; gerçek istemci `waiter-app.js`
   zaten kendi JS'inde `kitchenState`/sipariş durumunu doğrudan işliyor,
   ve yönetici onay yüzeyi gerçekte PosTerminal'in `/authorization`
   React ekranı (V1-IAM-020) — WaiterPwa'nın C# "motoru" hiçbir zaman
   gerçek bir arayüze bağlanmadı. Daha önce iki kez silinen kalıbın
   (`WaiterOfflineQueueEngine`, `OrderEntryEngine`) üçüncüsü — aynı
   şekilde tamamen kaldırıldı: 4 kaynak dosyası (motor + model, iki
   ağaçta), 2 test projesi, `ALKAROS.slnx`'ten 2 proje girişi.

## Owned surface

- `plan/v1/remediation/V1-RMD-159-architecture-boundaries-remainder.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Clients/WaiterPwa/ManagerDecisions ağacı ve
    tests/Clients/WaiterPwa/ManagerDecisions ağacı (V1-IAM-020
    sahipliğinde) — tamamen kaldırıldı (2 kaynak + test projesi, testleri
    dahil).
  - src/Clients/WaiterPwa/OrderStatus ağacı ve
    tests/Clients/WaiterPwa/OrderStatus ağacı (V1-WTR-003 sahipliğinde) —
    tamamen kaldırıldı (2 kaynak + test projesi, testleri dahil).
  - ALKAROS.slnx (V1-FND-001 sahipliğinde) — yukarıdaki iki silinen test
    projesinin girişleri kaldırıldı.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V0-ARC-001
    sahipliğinde) — ApprovedHostOrchestrationEdges sözlüğü ve
    HostOrchestrationEdgesStayWithinTheApprovedList testi eklendi
    (V11-RMD-002'nin bu dosya için bıraktığı sahiplik emsaliyle aynı).
  - docs/architecture/module-dependency-rules.md (V0-ARC-001 sahipliğinde)
    — "Host/Experience orchestration edges" bölümü eklendi, Enforcement
    bölümü üçüncü kapıyı anıyor.
  - docs/CONSISTENCY_AUDIT.md (bu dosyanın açık bir görev sahibi yok —
    tools/consistency-audit betiğinin kendisiyle birlikte yaşıyor) —
    "Known blind spot" bölümü eklendi.

## Out of scope

- API uç noktaları (11) ve Frontend (30) bölümleri — ayrı görevler.

## Dependencies

- V1-RMD-158

## Acceptance evidence

- `dotnet build src/Clients/WaiterPwa/ALKAROS.WaiterPwa.csproj -c Debug` →
  0 uyarı, 0 hata (ölü ağaçlar kaldırıldıktan sonra istemci hâlâ derleniyor).
- Silinen tiplerin (`WaiterManagerDecisionEngine`, `WaiterOrderStatusEngine`,
  `ManagerDecisionModels`, `OrderStatusModels` ve iç kayıt tipleri) tüm
  repoda başka referansı olmadığı `grep` ile doğrulandı.
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj -c Debug`
  → 9/9 yeşil (yeni `HostOrchestrationEdgesStayWithinTheApprovedList` dahil).
- Yeni test, `SentItemVoid`'in onaylı listesinden bilerek Kitchen çıkarılıp
  çalıştırılarak **gerçekten düştüğü** kanıtlandı (boş/vacuous bir test
  değil), sonra doğru listeye geri alındı ve tekrar 9/9 yeşil doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None
