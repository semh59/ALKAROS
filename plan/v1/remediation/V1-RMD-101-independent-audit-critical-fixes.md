# V1-RMD-101 - Independent audit: three critical defects fixed

- Task ID: V1-RMD-101
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle (2026-09-05), V1 kapandıktan sonra 4 bağımsız (sıfırdan,
önceki oturumdan habersiz) ajanla tam bir denetim taraması yapıldı: sunucu
kablolaması/yetkilendirme, istemci tarafı, domain/veri bütünlüğü, test
paketi + plan/governance tutarlılığı. Her Critical bulgu, ajan raporlarından
bağımsız olarak bizzat kod okunarak (satır satır) yeniden doğrulandı önce.
Bu görev doğrulanan 3 Critical bulguyu giderir; High/Medium/Low bulgular
(ayrı, daha geniş bir remediasyon dalgasına bırakıldı) bu görevin
kapsamında değildir.

## Owned surface

- `plan/v1/remediation/V1-RMD-101-independent-audit-critical-fixes.md`
- `evidence/V1-RMD-101/**`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır):
  `src/Host/Experience/Orders/OrderManagementEndpoints.cs` (`V1-IAM-024`
  sahipliğinde kalır) — `/{orderId}/submit` rotası `/{orderId}/submit-draft`
  olarak yeniden adlandırıldı (aşağıdaki Bulgu 1); mevcut hiçbir başka
  endpoint değişmedi.
  `src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs`
  (`V1-IAM-027` sahipliğinde kalır) — `VoidAsync`'in yazma sırası
  değiştirildi ve `ConvertBillLineToWasteAsync` iki aşamaya bölündü
  (aşağıdaki Bulgu 3); iş mantığının kendisi (hangi durumda ne yapılacağı)
  değişmedi, yalnız SIRA değişti.
  `src/Clients/Cashier/wwwroot/cashier-app.js` (V1-CUI-004/V1-RMD-051/055/061
  tarihçesinde kalır — bkz. dosya geçmişi) — `isComplimentary` özelliği
  tamamen kaldırıldı (aşağıdaki Bulgu 2); sepet/bekletme/gönderme mantığının
  geri kalanı değişmedi.
  `tests/Clients/Cashier/Frontend/test_cashier_frontend.py` (mevcut
  sahiplikte kalır) — `isComplimentary` varlığını doğrulayan assertion,
  YOKLUĞUNU doğrulayan bir assertion'a çevrildi.
  `tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs`
  (`V1-RMD-020` sahipliğinde kalır) — yeni bir test eklendi
  (`NoTwoEndpointsShareTheSameHttpMethodAndRoutePattern`); mevcut testler
  değişmedi.
  `tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentTestDatabase.cs`,
  `OrderManagementVoidSentHttpTests.cs` (`V1-IAM-027` sahipliğinde kalır) —
  yeni bir seed metodu (`SeedOpenBillAsync`'e `status` parametresi, yeni
  `ReloadItemStateAsync`) ve yeni bir test eklendi; mevcut testler
  değişmedi.

## In scope

- **Bulgu 1 [Critical] — düzeltildi:** `POST .../orders/{orderId}/submit`
  hem `DualScreenApplication.Endpoints.cs`'te (terminal-geneli hızlı-satış
  gönderimi — PosTerminal'in gerçek "Sipariş gönder" butonunun çağırdığı,
  `SubmitOrderRequest{operationId, expectedRevision}` sözleşmesiyle
  eşleşen) hem `OrderManagementEndpoints.cs`'te (masa-taslağı akışı,
  `SubmitTableOrderRequest`) koşulsuz map ediliyordu — ikisi de aynı
  `WebApplication` üzerinde (`DualScreenApplication.Build`). Sonuç: her
  istek `AmbiguousMatchException` ile çıplak 500'e düşüyordu; PosTerminal'in
  asıl sipariş gönderme akışı üretimde hiç çalışmıyordu. Hangi endpoint'in
  gerçek bir çağıranı olduğu doğrulandı (grep ile — yalnız
  `DualScreenApplication.Endpoints.cs`'inki), ötekinin hiç çağıranı
  olmadığı doğrulandı (yalnız derlenmeyen yetim
  `OrderManagementExperienceTests.cs`, HTTP'yi hiç kullanmadan doğrudan
  store'u çağırıyor). Çağıranı olmayan rota `/{orderId}/submit-draft`
  olarak yeniden adlandırıldı.
- **Bulgu 2 [Critical] — düzeltildi:** Cashier hızlı-satış istemcisinde
  (`cashier-app.js`) "İkram" (comp) düğmesi bir kalemi ekranda ₺0 gösterip
  sunucuya `unitPrice: 0` gönderiyordu; ama `OrderManagementStore
  .CreateOrUpdateTableDraftAsync` bu alanı hiç okumuyor, kalıcı fiyatı her
  zaman katalogdan yeniden hesaplıyordu (kod okumasıyla doğrulandı, satır
  satır). Kasiyer ve ekran "ücretsiz" derken müşteri gerçekte tam fiyattan
  faturalanıyordu — hiçbir yetkilendirme kontrolüne uğramadan. Gerçek,
  yetkilendirilmiş ikram akışı (`bills.comp` grant'i, `V1-BIL-005`'in
  `POST .../items/{itemId}/comp` uç noktası) zaten var ama yalnız zaten
  var olan bir sipariş üzerinde çalışıyor; bu istemcinin tek-seferlik
  "sepeti bir kerede gönder" modeliyle uyumlu değil. `isComplimentary`
  özelliği (gösterim, toplam hesabı, payload alanı, buton) tamamen
  kaldırıldı — artık ekran her zaman gerçekte faturalanacak tutarı
  gösteriyor. Gerçek ikram akışının bu istemciye bağlanması ayrı bir
  görev (bu görevin kapsamında değil — bkz. Out of scope).
- **Bulgu 3 [Critical] — düzeltildi:** `SentItemVoidStore.VoidAsync`,
  Order iptalini ve mutfak bileti iptalini (ikisi de kalıcı, ayrı commit)
  Bill'in dönüştürülebilir olup olmadığını kontrol etmeden ÖNCE
  yazıyordu; Bill zaten kapalıysa (Paid/Allocated)
  `BillNotModifiableForWasteException` sonradan fırlıyor ve 409
  "BILL_NOT_MODIFIABLE" dönüyordu — bu mesaj "hiçbir şey olmadı" gibi
  okunuyordu ama gerçekte kalem zaten iptal edilmiş, mutfak asla
  hazırlamayacaktı; müşteri teslim edilmeyecek bir ürün için tam fiyat
  ödemeye devam ediyordu, hiçbir audit kaydı da düşmüyordu. Sıra
  değiştirildi: Bill'in dönüştürülebilirliği artık Order/Kitchen'a hiç
  dokunulmadan ÖNCE kontrol ediliyor (`FindBillLineForWasteAsync`); yalnız
  Bill gerçekten dönüştürülebilirse Order/Kitchen mutasyonuna geçiliyor.
  Kontrol ile gerçek yazı arasındaki dar pencerede bir eşzamanlı Bill
  durum değişikliği hâlâ mümkün ama bu, `ApplyBillWasteConversionAsync`'in
  kendi row_version kontrolüyle yakalanıp olağan 409
  CONCURRENCY_CONFLICT'e düşer — sessiz bir tutarsızlık değil.
- Yeni regresyon testleri: (a) tüm host kompozisyonundaki HİÇBİR
  (HTTP metodu, route şablonu) çiftinin tekrarlanmadığını doğrulayan genel
  bir test (Bulgu 1'in TÜM SINIFINI kapsar, yalnız bu örneği değil — geçici
  olarak eski rotayı geri getirip testin gerçekten kırıldığı doğrulandı,
  sonra düzeltme geri getirildi); (b) Cashier'ın `isComplimentary`
  içermediğini doğrulayan test; (c) Bill zaten kapalıyken void isteğinin
  409 döndüğünü VE sipariş kaleminin tamamen dokunulmamış kaldığını
  (Active/Preparing) doğrulayan test.

## Out of scope

- Bağımsız denetimde bulunan High/Medium/Low bulgular (Catalog modülünün
  eksik DI kaydı, çift-tıklama koruması, `IRoleManagementService`'in
  endpoint'siz kalması, `Order.CancelItem`'ın reason/actor parametrelerini
  yok sayması, Kitchen ticket item'ların sahte concurrency çakışması,
  `PostgresBillRepository`'nin item-level concurrency eksikliği,
  `plan/v1/README.md`'nin görev sayım hatası, eksik `evidence/` dizinleri)
  — ayrı bir remediasyon dalgasına bırakıldı, Semih onayı bekliyor.
- Gerçek, yetkilendirilmiş ikram akışının Cashier hızlı-satış istemcisine
  bağlanması — Bulgu 2'nin kapsamı yalnız sahte/tehlikeli özelliği
  kaldırmak; yeniden inşa etmek ayrı bir görev.

## Dependencies

- V1-GOV-078

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Host.Experience.Composition.Tests` 5/5 (4→5, yeni
  `NoTwoEndpointsShareTheSameHttpMethodAndRoutePattern` — eski rota adı
  geçici olarak geri getirilip testin gerçekten
  "AmbiguousMatchException...POST .../orders/{orderId:guid}/submit"
  mesajıyla kırıldığı doğrulandı, sonra düzeltme geri yüklendi);
  `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 8/8 (7→8, yeni
  `WhenTheBillIsAlreadyClosedNothingIsMutatedAndTheRequestIsRejected`);
  `ALKAROS.Host.Experience.Orders.Void.Tests` 5/5,
  `ALKAROS.Host.Experience.Orders.Comp.Tests` 7/7,
  `ALKAROS.Orders.OrderAggregate.Tests` 101/101,
  `ALKAROS.Billing.BillFoundation.Tests` 40/40,
  `ALKAROS.Kitchen.TicketLifecycle.Tests` 18/18,
  `ALKAROS.Architecture.Tests` 8/8 — hepsi regresyonsuz.
- `python -m pytest tests/Clients -v`: 10/10 (Cashier 4/4 dahil, güncellenen
  assertion'la).
- `python tools/consistency-audit/consistency_audit.py`: temiz (dört
  Türkçe karakter sızıntısı bulunup düzeltildi).
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- Ortam notu: test Postgres container'ı (`alkaros-test-pg`) oturum
  sırasında kendiliğinden durmuştu (bilinen bir tuhaflık, kod kusuru
  değil); `docker start alkaros-test-pg` ile yeniden başlatıldı.

## Handoff

- V1-GOV-080
