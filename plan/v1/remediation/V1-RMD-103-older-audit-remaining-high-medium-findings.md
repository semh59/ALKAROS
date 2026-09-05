# V1-RMD-103 - Older independent audit: remaining High/Medium defects fixed (B1, B5, B6, H1-H3)

- Task ID: V1-RMD-103
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("tümünü düzelt", 2026-09-05), `docs/engineering/v1-independent-audit.md`
(daha eski, ayrı bir bağımsız denetim turu — 2026-09-05 4-ajan taramasından
farklı) içindeki hâlâ açık altı bulgu giderildi: **B1 [HIGH]** bill
adjustments (indirim) tamamen bağlanmamıştı, **H1 [MED]** `BillingSplitStore`
finansal yolda geniş `catch (Exception)` kullanıyordu, **H2 [LOW-MED]** /
**H3 [LOW]** `AuditSanitizer`'ın anahtar-tabanlı redaksiyonu, **B5 [LOW]**
`SplitEngine`'in kısmi kalem bölmesinde kalan-dengeleme eksikliği, **B6
[LOW]** `catalog.products`'ta hiç `row_version` olmaması. Denetimin diğer
maddeleri (B2 kasiyer oturumu kasıtlı V1.2 kapsam kesimi, B3/B4 zaten daha
önce giderilmiş, B7 kasıtlı V1→V1.5 kapsam sınırı) bu görevin kapsamı
dışındadır — hepsi ayrıca doğrulandı, gerçek defekt değiller.

## Owned surface

- `plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md`
- `database/migrations/V1/V1-RMD-103/053-catalog-products-row-version.up.sql` (yeni)
- `database/migrations/V1/V1-RMD-103/053-catalog-products-row-version.down.sql` (yeni)
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Modules/Billing/Adjustments/DiscountReasonCatalog.cs` (yeni dosya,
  ama dizin `V1-BIL-004` tarafından `src/Modules/Billing/Adjustments/**`
  olarak sahiplenilmiş — `plan_audit_tool.py`'nin `SURFACE_PREFIX_OVERLAP`
  kontrolü bunu doğruladı) — `ComplimentaryReasonCatalog`'un (Orders modülü)
  aynı deseni, Bill-seviyesi indirim gerekçe kataloğu.
  `src/Modules/Billing/SplitDesign/SplitEngine.cs` (`V1-RMD-027`
  sahipliğinde kalır) — `CreateItemSplit`'in kalan-dengeleme dalı artık
  kısmi gruplarda da çalışıyor (Bulgu B5); tam-tahsis davranışı değişmedi.
  `tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs` (`V1-RMD-027`
  sahipliğinde kalır) — yeni bir regresyon testi eklendi.
  `src/Modules/Audit/EventStore/IAuditSanitizer.cs` (`V1-RMD-072`
  sahipliğinde kalır) — `IsSensitiveKey` token-sınırlı eşleşmeye geçti
  (Bulgu H3), `SanitizeNode` yapılandırılmış JSON'da string leaf'leri de
  taramaya başladı (Bulgu H2); `FallbackSanitizeText`'in kendi mantığı
  değişmedi, yalnız ortak bir `RedactEmbeddedSecrets`'e çıkarıldı.
  `tests/Modules/Audit/EventStore/AuditSanitizerTests.cs` (`V1-RMD-072`
  sahipliğinde kalır) — iki yeni regresyon testi eklendi.
  `src/Host/Experience/Billing/BillingSplitStore.cs` (`V1-RMD-057`
  sahipliğinde kalır) — geniş `catch (Exception)` daraltıldı (Bulgu H1);
  `ApplyDiscountAsync`/`GetAdjustmentsAsync` eklendi (Bulgu B1);
  `IOrderRepository` eksik DI kaydı (H1 regresyon testi yazarken bulundu,
  aşağıya bakın) düzeltildi.
  `src/Host/Experience/Billing/BillingSplitApplication.cs` (`V1-IAM-024`
  sahipliğinde kalır) — grant-akışının tüm bağımlılıkları eklendi
  (`V1-BIL-005`'in aynı deseni), yeni `POST .../bills/{billId}/discount` ve
  `GET .../bills/{billId}/adjustments` uç noktaları eklendi; mevcut hiçbir
  endpoint değişmedi.
  `src/Host/Experience/Billing/BillingSplitContracts.cs` (`V1-RMD-054`
  sahipliğinde kalır) — yeni discount request/result/summary kayıtları
  eklendi.
  `tests/Host/Experience/Billing/BillingSplitHttpTests.cs` (`V1-RMD-040`
  sahipliğinde kalır) — 6 yeni test eklendi (H1 eşzamanlılık + B1 discount
  akışının tamamı); mevcut testler değişmedi.
  `src/Modules/Catalog/ProductCatalog/Product.cs`,
  `PostgresProductRepository.cs`,
  `tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs`
  (`V1-RMD-076` sahipliğinde kalır) — `row_version` alanı ve item-seviyesi
  optimistic concurrency eklendi (Bulgu B6); mevcut alan/davranış değişmedi.
  `src/Modules/Catalog/ProductCatalog/Repositories.cs` (`V1-CAT-001`
  sahipliğinde kalır) — `IProductRepository.UpdateAsync`'e
  `expectedRowVersion` parametresi eklendi.
  `src/Host/Experience/Catalog/CatalogManagementStore.cs`,
  `CatalogManagementEndpoints.cs`,
  `tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs`,
  `ALKAROS.Host.Experience.Catalog.Tests.csproj` (`V1-RMD-076` sahipliğinde
  kalır) — `SetProductAvailabilityAsync` yeni `expectedRowVersion`'ı geçiyor;
  filter yeni `InvalidOperationException`→409 eşlemesi kazandı; yeni bir
  regresyon testi ve migration 053 fixture bağlantısı eklendi.
  `database/MigrationComposition/order.json`,
  `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` (`V1-IAM-025`
  sahipliğinde kalır) — migration `053` eklendi, `phaseBRange.max`
  052→053, manifest testinin sabit listeleri güncellendi.
  `src/Host/Composition/Migrations/MigrationManifest.cs` içindeki
  `PhaseBMax` sabiti `V1-FND-004` sahipliğinde kalır — `053`'e güncellendi.

## In scope

- **Bulgu B1 [HIGH] — düzeltildi:** `src/Modules/Billing/Adjustments/**`
  (`AdjustmentCalculator`, `BillAdjustment`, `IBillAdjustmentRepository`,
  `PostgresBillAdjustmentRepository`, migration 021) tam uygulanmış ve
  14/14 testle doğrulanmıştı ama hiçbir yere bağlı değildi: DI kaydı yok,
  `AdjustmentCalculator.Calculate`'ın hiç çağıranı yok, hiçbir HTTP
  endpoint'i yok — bir indirim hiçbir yerden girilemiyordu.
  `ApplicationPermissions.BillsDiscount` (`bills.discount`) izin kodu ve
  onun tüm grant-class altyapısı (policy/delegation/behavioural-tightening)
  zaten vardı ve kullanılmayı bekliyordu — `V1-BIL-005`'in `bills.comp` için
  yaptığı ilk gerçek HTTP çağrısıyla aynı sınıf. `POST
  .../bills/{billId}/discount` eklendi: `bills.discount`'u doğrudan tutan
  bir rol direkt uygular; tutmayan bir rol `IAuthorizationGrantService
  .RequestAsync` ile bir istek yükseltir (politika motoru, aktif bir
  delegasyon veya bir yönetici karar verir). Yazmadan ÖNCE
  `AdjustmentCalculator.Calculate` aday listeyle (mevcut + yeni) çağrılıp
  doğrulanıyor — toplam indirim ödenecek tutarı aşarsa hiçbir şey
  kalıcılaşmadan reddediliyor. `GET .../bills/{billId}/adjustments` uygulanan
  indirimleri ve düzeltilmiş özet toplamları (`AdjustedPayableAmount`) okur.
  Bir bill'in kalemleri farklı vergi oranları taşıyabildiğinden, indirimin
  net/vergi ayrımı bill'in kendi ağırlıklı-ortalama efektif vergi oranıyla
  yapılıyor (tek bir kalemin oranı yerine).
- **Kapsam dışı bırakılan (B1'in bir parçası, ayrı bir tasarım kararı
  gerektirir):** İndirimin `SplitEngine`/gerçek ödeme akışına
  entegrasyonu — bir indirim bölünmüş bir hesapta katılımcılar arasında
  nasıl dağıtılmalı sorusu henüz kasıtlı olarak yanıtlanmamış (denetimin
  kendi ifadesiyle "ambiguous scope"). Bugün indirim uygulanabilir ve
  görülebilir (`GetAdjustmentsAsync`), ama `SplitEngine` hesaplamaları hâlâ
  `Bill.PayableAmount`'ın kendisini kullanıyor, düzeltilmiş tutarı değil.
  Fee/Kuver/Tip ayarlamaları da aynı nedenle kapsam dışı — bunlar için henüz
  `bills.discount`'a karşılık gelen bir grant-class izin kodu tanımlı değil.
- **Bulgu H1 [MED] — düzeltildi:** `BillingSplitStore.CreateBillFromOrderAsync`
  bill ekleme etrafında geniş bir `catch (Exception)` kullanıyordu — bu,
  DOĞRULAMA HATALARINI, BAĞLANTI ARIZALARINI, `AddAsync`'teki bir kusuru
  ayırt etmeden yakalayıp, sipariş için zaten var olan herhangi bir bill'i
  istek başarılıymış gibi geri veriyordu (finansal yol). `bill_number` sipariş
  başına deterministik (`$"BILL-{order.OrderNumber}"`) olduğundan, tek
  beklenen eşzamanlı hata iki isteğin aynı bill_number'ı eklemeye
  çalışmasıdır; catch artık yalnız `bills_bill_number_key` unique
  violation'ına daraltıldı. Bu düzeltme için bir regresyon testi yazılırken
  `AddBillingSplitExperience()`'ın hiç `IOrderRepository` kaydetmediği
  (Catalog'un daha önce bulunan DI boşluğuyla aynı sınıf) bulunup ayrıca
  düzeltildi — onsuz endpoint standalone kompozisyonda hiç test
  edilemiyordu.
- **Bulgu H2 [LOW-MED] — düzeltildi:** `AuditSanitizer.SanitizeNode`,
  iyi biçimli JSON'da yalnız özellik ADINA göre redakte ediyordu; bir
  ANAHTAR-DIŞI (masum görünen) alanın string DEĞERİ içine gömülü bir sır
  (`{"detail": "auth failed for token=eyJ..."}`) denetim kaydına olduğu gibi
  geçiyordu. Değer-seviyesi regex taraması (`FallbackSanitizeText`'in
  `RedactEmbeddedSecrets` olarak çıkarılan çekirdeği) artık yapılandırılmış
  yoldaki her string leaf üzerinde de çalışıyor, yalnız JSON-parse-hatası
  fallback'inde değil.
- **Bulgu H3 [LOW] — düzeltildi:** `IsSensitiveKey` ham bir substring eşleşmesi
  yapıyordu — `"pin"` `"shipping"` içinde, `"pan"` `"company"`/`"expansion"`
  içinde yanlışlıkla eşleşip ilgisiz alanları redakte ediyor, denetim izinin
  kullanışlılığını/bütünlüğünü bozuyordu. Anahtar artık `_`/`-`/camelCase
  sınırlarında token'lara bölünüp tam-token eşleşmesiyle kontrol ediliyor;
  gerçekten iki kelimenin birleşimi olan kalıplar (`cardnumber`,
  `creditcard`, `apikey`) ayrı, dar bir substring listesinde kaldı çünkü
  token bölme onları "card"+"number" gibi ikiye ayırır ve ne "card" ne
  "number" tek başına hassastır.
- **Bulgu B5 [LOW] — düzeltildi:** `SplitEngine.CreateItemSplit`, kısmi
  tahsis edilmiş bir grupta (`totalAllocatedQty < billItem.Quantity`) son
  hedefe düz per-target fraction uyguluyordu, grubun kendi kalanını değil —
  tahsis edilen kısmı gerçek orantısal değerden ~1 kuruşa kadar eksik
  bırakabiliyordu. Kalan-dengeleme artık her grubun son hedefinde çalışıyor
  (yalnız grup tam tahsisliyken değil), grubun kendi tahsis edilmiş payına
  (`allocatedGross`/`allocatedTax`) karşı.
- **Bulgu B6 [LOW] — düzeltildi:** `catalog.products`'ta hiç `row_version`
  yoktu; `PostgresProductRepository.UpdateAsync` eşzamanlı bir düzenlemeyi
  (örn. iki suspend/restore çağrısı) sessizce son-yazan-kazanır şeklinde
  çözüyordu. Yeni `row_version` sütunu (migration 053) ve `UpdateAsync`'in
  WHERE fıkrasına `AND row_version = @expected_row_version` eklendi;
  `Product` domain modeli ve `SetProductAvailabilityAsync` güncellendi.

## Dependencies

- V1-RMD-102

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`, port 55432):
  `ALKAROS.Catalog.ProductCatalog.Tests` 81/81 (80→81, yeni
  `UpdateAsyncThrowsWhenTheProductWasConcurrentlyModified` — düzeltme geçici
  geri alınıp "No exception was thrown" ile kırıldığı doğrulandı, sonra geri
  getirildi); `ALKAROS.Host.Experience.Catalog.Tests` 7/7 (6→7, yeni
  `SettingAvailabilityChangesRowVersionAndReturnsTheProduct`);
  `ALKAROS.Billing.SplitDesign.Tests` 28/28 (27→28, yeni
  `PartiallyAllocatedItemSplitRemainderBalancesTheLastTarget` — düzeltme
  geçici geri alınıp "Expected 33.34 Actual 33.33" ile kırıldığı doğrulandı,
  sonra geri getirildi); `ALKAROS.Audit.EventStore.Tests` 22/22 (20→22, iki
  yeni H2/H3 regresyon testi — her ikisi de ayrı ayrı geçici geri alınıp
  kırıldıkları doğrulandı, sonra geri getirildi);
  `ALKAROS.Host.Experience.Billing.Tests` 8/8 (3→8, beş yeni test: H1
  eşzamanlılık + dört B1 discount testi); `ALKAROS.Billing.Adjustments.Tests`
  14/14 (regresyonsuz); `ALKAROS.Host.MigrationComposition` (ManifestTests
  filtresi) 16/16; `ALKAROS.Host.Experience.Composition.Tests` 5/5 (rota
  çakışması yok, yeni discount/adjustments endpoint'leri dahil) — hepsi
  regresyonsuz.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `validate-coverage`: sıfır hata.
- Ortam notu: `tests/Host/MigrationComposition` süitinin `psql`-bağımlı
  kısmı (G2, önceki dalgalardan bağımsız bilinen ortam boşluğu) bu dalgada
  da aynı şekilde etkilenmemiş durumda kaldı; `ManifestTests` (bu dalganın
  değiştirdiği kısım) doğrudan filtrelenerek ayrıca yeşil doğrulandı.

## Handoff

- V1-GOV-084
