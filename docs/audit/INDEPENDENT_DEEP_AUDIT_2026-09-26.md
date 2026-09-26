# ALKAROS — Çok Ajanlı Bağımsız Derin Denetim Raporu (2026-09-26)

> **Denetim Tarihi:** 26 Eylül 2026
> **Denetim Türü:** Sıfır ön bilgili 13 bağımsız ajan, iki tur (8 + 5), her biri kod tabanının farklı bir katmanını hiçbir varsayımda bulunmadan, gerçek dosyaları okuyarak inceledi. En kritik 10 iddia ayrıca 14. bir ajanla ÇÜRÜTME amaçlı (adversarial) yeniden doğrulandı.
> **Kapsam:** Orders/Kitchen, Billing/Payments, Identity/Authorization/Security, Inventory/Purchasing/Recipes, WaiterPwa+Cashier, PosTerminal, DI/Migration/Hosted Services, v1.2 Yemeksepeti/QR (Faz 3), Reporting/Observability/Support/Settings/Operations, QrRelay/Integrations/Reconciliation, NfcOrdering+CustomerWeb+Cashier tam, Deployment/Docker/CI.
> **Kapsam dışı (bu turda denetlenmedi):** performans/yük testi, otomatik erişilebilirlik taraması (axe-core), veritabanı şema/index tasarımının derinlemesine incelenmesi, bulguların gerçek ortamda (test yazarak) çalıştırılması.
> **Metod notu:** Bulguların tamamı statik kod okumasına dayanır. 10 kritik iddia bağımsız bir ikinci ajanla doğrulandı (10/10 CONFIRMED); geri kalan bulgular tek ajanın okumasına dayanır, ikinci bir doğrulama turu geçmemiştir.

---

## 1. Yönetici özeti

Toplam **~53 gerçek, kanıtlanmış bulgu** (dosya:satır referanslı), bunlardan **18'i kritik**. Aynı denetimde daha önce bu oturumda açılan `V1-RMD-298` (indirim/bahşiş tahsilat tavanı) görevinin **yazıldığı şekliyle yetersiz** olduğu da ortaya çıktı — aynı hata sınıfı üç ayrı yerde daha canlı.

En önemli iki tema:

1. **"Kayıt var, ama etkisi yok" kalıbı** en az 6 ayrı yerde tekrarlanıyor: bir değer/karar veritabanına yazılıyor ama onu asıl kullanması gereken alt sistem (tahsilat tavanı, birim dönüşümü, mutabakat durumu, sağlık kontrolü, izin onayı) o kaydı hiç okumuyor veya hiç doğrulamıyor.
2. **Kalite kapılarının kendisinde yapısal boşluklar var**: `plan_audit_tool.py` bir görevin kanıt metninin *doğruluğunu* değil yalnız *biçimini* kontrol ediyor; `consistency_audit.py`'nin Türkçe-identifier tespiti regex hatası yüzünden hiç çalışmıyor; CI'nin Owned-surface denetimi yalnız PR'da çalışıyor, bu depodaki gerçek iş akışı olan doğrudan push'ta hiç çalışmıyor.

## 2. Kapsam ve yöntem

| Ajan | Alan | Bulgu sayısı |
| --- | --- | --- |
| 1 | Orders & Kitchen (sunucu) | 5 |
| 2 | Billing & Payments (sunucu) | 6 |
| 3 | Identity, Authorization & Security | 3 |
| 4 | Inventory, Purchasing & Recipes | 6 |
| 5 | WaiterPwa & Cashier (vanilla JS) | 8 |
| 6 | PosTerminal (React/TS) | 11 |
| 7 | DI, Migration, Hosted Services | 7 |
| 8 | v1.2 Yemeksepeti/QR (Faz 3) | 4 |
| 9 | Reporting, Observability, Support, Settings, Operations | 10 |
| 10 | Doğrulama (10 kritik iddianın çürütülmeye çalışılması) | 10/10 CONFIRMED |
| 11 | NfcOrdering, CustomerWeb, Cashier (tam) | 9 |
| 12 | Deployment, Docker, CI pipeline | 9 |
| 13 | QrRelay, Integrations, Reconciliation | 13 |

## 3. Kritik bulgular (18)

### Para / tahsilat

#### K1. `V1-RMD-298` yazıldığı şekliyle yetersiz — aynı ham tavan hatası 3 yerde daha canlı (doğrulandı)

- `src/Modules/Cash/TenderHandler/CashTenderHandler.cs:103-107` ve `src/Modules/Payments/EftTender/EftTenderHandler.cs:116-121`: `PaymentAllocationFactory`'den bağımsız kendi ham `bill.PayableAmount` tavan kontrolünü tekrarlıyor.
- `src/Modules/Billing/SplitDesign/SplitEngine.cs:93,283` (`CreateAmountSplit`/`CreateCustomSplit`): bölüm toplamını ham `bill.PayableAmount`'a karşı doğruluyor, `AdjustmentCalculator`'ı hiç kullanmıyor — indirimli/bahşişli bir hesap asla bölünemiyor.
- `src/Host/DualScreen/DualScreenApplication.Payments.cs:144-146`: "kalan tutar" GET endpoint'i de aynı ham tavanı döndürüyor.
- **Sonuç:** `V1-RMD-298`'i mevcut Owned surface'ıyla (`PaymentAllocationFactory.cs`, `BillPaymentClosureCalculator.cs`) kapatmak yeterli değil; bahşiş nakit/EFT'te hâlâ tahsil edilemez.

#### K2. İade (refund) akışı fiilen çalışmıyor (doğrulandı)

- `docs/domain/refund-ledger.md`'nin tanımladığı `payment_reversals` tablosu kodda hiç yok (`grep` sıfır sonuç, `RefundIntentFactory.cs:33-34`'ün kendi yorumu bunu doğruluyor).
- `src/Modules/Payments/PaymentAggregate/Payment.cs:142-151` (`CanTransitionTo`) `Refunded`/`PartiallyRefunded`'ı desteklemiyor.
- Bir iade talebi onaylansa bile para asla fiilen geri dönmüyor, sonsuza kadar `Pending` kalıyor.

#### K3. Kasa ödeme ekranlarında idempotency key her tıklamada yeniden üretiliyor (doğrulandı)

- `src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js:457` (`submitTender`), `:368,417`; `cash-session.js:222` (`submitCashMovement`): `crypto.randomUUID()` fonksiyon içinde her çağrıda yeniden üretiliyor, önceki denemenin anahtarı saklanmıyor.
- Senaryo: sunucu isteği işleyip yanıt kaybolursa, "tekrar dene" gerçek bir mükerrer tahsilat/nakit hareketi/indirim oluşturur.

#### K4. `alkaros.current-bill-id` çıkışta temizlenmiyor (doğrulandı)

- `src/Clients/PosTerminal/src/routes/workspace.tsx:288,317,344` bu anahtarı yazıyor/okuyor; `Cashier.tsx`'teki `logout()` (satır 315-331) localStorage'a hiç dokunmuyor (grep sıfır sonuç).
- Senaryo: vardiya değişiminde yeni kasiyer, eski kasiyerin açık bıraktığı bölünmüş hesabı otomatik görebilir/değiştirebilir.

### Mutfak / sipariş

#### K5. Çok kurslu siparişlerde mutfak fişi asla "Hazır" olamıyor (doğrulandı)

- `src/Modules/Orders/OrderAggregate/Order.cs:439-442` (`FireRound`): `Held` kalemler de ateşlenen listeye ekleniyor. `KitchenTicket.CreateFromOrder` (`KitchenTicket.cs:363-368`) bunu `Queued`/`isHeld:true` olarak fişe yazıyor. `CanBeMarkedReady()` (`KitchenTicket.cs:119-127`) bu bayrağı dışlamıyor.
- Senaryo: `fire-course` orijinal fişi güncellemiyor, yeni ayrı fiş açıyor; orijinal fiş diğer tüm kalemler servis edilse bile sonsuza kadar tamamlanmamış görünüyor.

#### K6. Bekleyen (Held) kurs iptal edilirse stoğu asla geri verilmiyor (doğrulandı)

- `src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs:133`: stok iadesi yalnız `item.KitchenState == KitchenState.Sent` için yapılıyor.
- Senaryo: hiç mutfağa çağrılmamış bir kursun iptalinde tükettiği stok kalıcı olarak envanterden düşük kalır — gerçek mali/stok kaybı.

### Stok

#### K7. Özel birim dönüşümleri hiçbir zaman çalışma zamanına yüklenmiyor (doğrulandı)

- `src/BuildingBlocks/Measurements/UnitConverter.cs:33` (`RegisterConversion`) kod tabanında hiçbir yerden çağrılmıyor (grep sıfır sonuç). `IUnitConverter` `Transient` kaydedildiği için her çözümlemede yalnız `StandardUnits.All` yüklü taze bir örnek oluşuyor.
- Senaryo: yöneticinin `recipe.unit_conversions` tablosuna eklediği "1 koli = 12 adet" gibi dönüşümler DB'de duruyor ama hiç uygulanmıyor; aynı boyuttaki (Count) birimler arasında sessizce 1:1 dönüşüm yapılıyor. Mal kabul, sayım, üretim tüketimi hiçbir hata vermeden yanlış miktarla stoka yazılabiliyor.

#### K8. `RebuildAllBalancesAsync` rezervasyonları sıfırlıyor

- `src/Modules/Inventory/BalanceProjection/StockBalanceProjector.cs:37-75`, `PostgresStockBalanceRepository.cs:314-347` (`SetExactBalanceAsync`): rebuild sonrası her stok kaleminin `reserved_quantity`'si 0'a dönüyor.
- Şu an hiçbir HTTP endpoint/hosted service'ten çağrılmıyor (ölü kod), ama ileride bir bakım aracına bağlanırsa aktif rezervasyonlar "müsait" görünmeye başlar → çift satış riski.

### Yetki / güvenlik

#### K9. Yetki talebinde kendi kendini onaylama koruması yok (doğrulandı)

- `src/Host/Experience/Authorization/AuthorizationDecisionStore.cs:51-68` → `PostgresAuthorizationGrantRepository.cs:81-111`: `ResolveAsync`'in SQL'i yalnız `WHERE grant_id = @id AND status = 'pending'`, `RequesterUserId` ile hiçbir karşılaştırma yok.
- Senaryo: bir kullanıcı hem garson hem yönetici rolüne sahipse (küçük işletmelerde olası), kendi açtığı indirim/iptal talebini kendi yönetici oturumuyla onaylayabiliyor. Aynı zayıflık davranışsal kısıtlama temizlemede de var (`ClearTighteningAsync`).

#### K10. 95+ yönetim uç noktası istemciden hiç erişilemiyor — güvenlik-kritik örnekler (doğrulandı)

- `grep -r "revoke-sessions"` ve `grep -r "force-unlock"` PosTerminal istemcisinde sıfır sonuç veriyor (`SecurityAdministrationEndpoints.cs:69,80`).
- Ayrıca: rol/izin yönetimi (`RoleManagementEndpoints.cs`), tedarikçi CRUD (`PurchasingManagementEndpoints.cs`), mutabakat raporu ve manuel onaylar (`PaymentSettlementEndpoints.cs`), kritik stok/gerçek-vs-teorik raporu (`InventoryReportingEndpoints.cs`) — hiçbiri istemciden çağrılmıyor.
- **En kritiği:** şüpheli oturum iptali veya kilitli kullanıcı kurtarma PosTerminal'den yapılamıyor, yalnızca doğrudan HTTP ile.

### Kalite kapıları (kendi denetim araçlarımız)

#### K11. `plan_audit_tool.py`'nin "Done" kanıtı doğrulaması yalnızca biçimsel

- `tools/plan-audit/plan_audit_tool.py:2521-2570, 4146-4222`: bir görevin "Acceptance evidence" bölümünün var olup olmadığını, Türkçe olup olmadığını, yasaklı ifade içerip içermediğini kontrol ediyor; ama içeriğin *doğruluğunu* (test gerçekten çalıştı mı) hiçbir yerde doğrulamıyor. Tek istisna tek bir görev için özel bir git-commit kontrolü (`v3_interrupted_closure_errors`).
- Sonuç: "23/23 test geçti" gibi tamamen kurgusal bir metin, biçim kurallarına uyduğu sürece sıfır hatayla geçer.

#### K12. `consistency_audit.py`'nin Türkçe-identifier tespiti yapısal olarak asla çalışamıyor

- `tools/consistency-audit/consistency_audit.py:68-71` (`IDENTIFIER_RE`): `[A-Za-z_][A-Za-z0-9_]*` deseni Türkçe harfleri (ş,ğ,ı,ö,ü,ç) içermiyor; `var müşteriAdi` gibi bir identifier Türkçe harften önce kesiliyor, kalan parçada Türkçe harf aranmadığı için hiçbir zaman flag'lenmiyor.
- Aracın ana amacının (kod identifier'larının İngilizce olmasını zorlamak) tam tersini üretiyor.

#### K13. `task-scope.yml`'nin Owned-surface kontrolü yalnız PR'da çalışıyor

- `.github/workflows/task-scope.yml:26-29`: `enforce` job'ı yalnız `pull_request`/`workflow_dispatch` olaylarında çalışıyor. Bu depodaki gerçek iş akışı (doğrudan `master`'a push) hiçbir otomatik Owned-surface denetiminden geçmiyor.

### Gözlemlenebilirlik / altyapı

#### K14. Sağlık kontrolleri tamamen kendinden-bildirim, gerçek prob yok

- `src/Host/Experience/Observability/ObservabilityEndpoints.cs:168-182`, `src/Modules/Observability/Foundation/ObservabilityService.cs:48-54`: `POST /health-checks` istemciden gelen `Status`'u doğrudan kaydediyor, gerçek DB/disk/dış servis pingleyen hiçbir `BackgroundService` yok.
- `src/Modules/Operations/BackupHealth/BackupHealthService.cs:135-156` (`CaptureSystemHealthSnapshotAsync`) da hiçbir yerden çağrılmıyor.

#### K15. EOD raporu ile ödeme mutabakat raporu gün sınırı tutarsız

- `OperationalReportService.CalculateServiceWindow` (`06:00 → ertesi 05:59`, ama ölü kod) vs. `PaymentSettlementReportFilter.ResolveWindow()` (`00:00 → 00:00`).
- Senaryo: gece 01:00'te ödenen bir hesap, iki farklı raporda iki farklı günün cirosuna düşer.

#### K16. QR Relay: belgelenen "kuyruk + tampon" mimarisi kodda yok

- `docs/architecture/qr-relay-topology.md:14-20,56` "Durable Queue, at-least-once delivery" vaat ediyor; `src/Integrations/QrRelay/PublicGateway/CloudflareApiClient.cs:76-90` doğrudan reverse-proxy ingress kuruyor, kuyruk/tampon mekanizması hiç yok.
- `RelayProvisioningService.cs:52`: LAN/yerel erişim yolu yayınlanmıyor. Cloudflare tüneli düşerse tüm QR sipariş kanalı sıfıra iner, hiçbir yedek yok.

#### K17. Relay connector öldüğünde durum paneli sonsuza kadar "Bağlı" gösteriyor

- `src/Integrations/QrRelay/LocalConnector/RelayConnectorStatus.cs:4-9`, `PostgresRelayConnectorStatusReporter.cs:35-59`: `updated_at` ile bayatlık kontrolü hiç yapılmıyor; connector container çökerse son yazılan "Running" durumu kalıcı olur.

#### K18. "Çözüldü" işaretlenen mutabakat vakaları sürekli yeniden açılıyor

- `src/Modules/Reconciliation/Payments/PaymentUnknownSourcePair.cs:39-49`, `PostgresReconciliationRepository.cs:83-88`: bir vaka `Resolved` yapıldığında altta yatan `payments.payments.status` değişmiyor; bir sonraki tarama aynı ödeme için yeni bir vaka açıyor. Tarama da tamamen manuel tetiklemeye bağlı (zamanlayıcı yok).

## 4. Orta seviye bulgular (özet, ~20)

- Void/comp/accept/reject audit kaydı ana işlemle aynı transaction'da değil (`ItemExceptionHandler.cs:126-136,275-285`, `PendingOrderConfirmationStore.cs:129-136,181-188`).
- Yemeksepeti sipariş kalemi fiyatı iç katalogla hiç karşılaştırılmıyor (`YemeksepetiOrderNormalizer.cs:82`).
- Online (Yemeksepeti) siparişleri asla `Completed` durumuna erişemiyor (`OrderSource.Online` hiçbir Billing/Payment kodunda yok).
- NFC'de sayfa yenilenirse/arka plana düşerse kopya sipariş riski (`NfcOrder.tsx:31-35`, `submissionIdRef` yalnız bellekte; QR tarafı `sessionStorage` ile korunuyor, NFC korunmuyor).
- Garson PIN ekranında çifte dokunuşa karşı koruma yok (`kiosk-lock.js:173-211`); ücretsiz iptalde (`confirmVoid`) idempotency key eksik, kardeş akışlarla tutarsız.
- PosTerminal'de üst navigasyon tamamen gizliyor, Mutfak modülü "kilitli ama görünür" yapıyor — tutarsız yetki UX'i (`shell/models.ts:45-47` vs `workspace.tsx:562`).
- QNB/Token kimlik bilgisi formlarında biçim doğrulaması yok (`QnbCredentialSettings.tsx:83`, `TokenTerminalSettings.tsx:102-104`).
- `PhysicalPrintRecoveryService`'in ölü kod yüzeyi bilinenden geniş: 4 metod hiç çağrılmıyor (`PhysicalPrintRecoveryService.cs:106-160`).
- Aynı arayüz için çelişen DI kaydı: `RegisterTransient` + `TryAddSingleton` (ikincisi sessizce yutuluyor, `KitchenModule.cs:28` vs `KitchenOperationsEndpoints.cs:102`); `ISecretProvider` 3 modülde bağımsız kayıtlı.
- Production modülü kendi birim dönüşüm mantığını ayrı yazmış, merkezi `IUnitConverter`'ı hiç kullanmıyor (`ProductionStockEffectService.cs:530-575`); stok kilitleme sırası global değil (deadlock riski).
- Reçete maliyet anlık görüntüsü stok birimini istemcinin isteğe bağlı sözlüğünden alıyor; unutulursa maliyet ~1000 kat yanlış hesaplanabilir, hatasız (`IRecipeCostSnapshotService.cs:44-108`).
- .NET güvenlik açığı taraması build'i hiç düşürmüyor — dekoratif (`task-scope.yml:299-304`).
- Caddy'de hiçbir güvenlik başlığı yok (`deploy/docker/Caddyfile:20-131`).
- GitHub Actions bağımlılıkları SHA değil değişebilir tag ile pinlenmiş.
- `consistency_audit.py`'nin cross-schema yazma kontrolü çok satırlı SQL'i, LIMIT kontrolü ise yorum/string içindeki "limit" kelimesini kaçırıyor.
- CustomerWeb sayfalarında hata/durum bölgeleri `aria-live`/`role="alert"` içermiyor.
- Kasa "beklet" (park) sepetleri vardiya/oturum sınırı olmadan kalıcı `localStorage`'da kalıyor.
- Cloudflare tüneli yeniden provizyon edildiğinde önceki tünel öksüz kalıyor; `DeleteTunnelAsync` tanımlı ama hiç çağrılmıyor.
- QNB "Bağlantıyı Test Et" her zaman sabit test-tenant URL'sine gidiyor, üretime geçiş noktası kodda işaretli değil.

## 5. Düşük seviye bulgular (özet, ~15)

Kasa statik sayfalarında erişilebilirlik eksikleri (etiketsiz form alanları, klavye ile etkinleşmeyen ürün kartları, sembol butonlarda aria-label eksik), native `confirm()` kullanımı, `localStorage.setItem` hatalarının yakalanmaması, `MaintenanceJobHostedService`'in diğer 7 hosted service'ten farklı hata toleransı deseni, NFC kabul akışında QR'a özel stok ön-kilidinin uygulanmaması (teorik deadlock), bazı `.down.sql`'lerde `DROP CONSTRAINT` için `IF EXISTS` eksik, PWA manifest'inde olmayan ikon referansı, `cloudflared` sürecinin graceful kapatma denenmeden öldürülmesi, relay/QNB kimlik bilgisi rotasyon mekanizması yok, senkron blocking DB çağrısı (`PostgresRelayConnectorStatusReporter.cs:35-59`).

## 6. Olumlu / sağlam çıkan alanlar

- **Faz 3 (Yemeksepeti/QR, diğer oturum tarafından yazıldı):** webhook güvenliği (sabit-zamanlı imza karşılaştırması), idempotency (`event_key` + `ON CONFLICT DO NOTHING`), çapraz kanal arbiter atomikliği (global sıralı kilitler) — beklenenden disiplinli.
- **Kimlik doğrulama/oturum güvenliği:** PBKDF2 + sabit-zamanlı karşılaştırma, kullanıcı adı numaralandırmaya karşı dummy-hash yutması, cookie ayarları (HttpOnly/Secure/SameSite=Strict) doğru.
- **Migration sistemi:** tam idempotent, SHA-256 checksum korumalı, `ValidateHistoryPrefix` ile bitişik-önek kuralı zorlanıyor.
- **Restore doğrulama:** gerçek indirme/şifre çözme/`pg_restore`/integrity check zinciri, sahte-başarı riski yok.
- **QNB draft/gerçek kod ayrımı:** taslak adaptör `evidence/` altında, ana çözüme hiç bağlı değil — production'a sızma riski yok.
- **Para hesapları:** tüm alanlar `decimal`, merkezi `BillMath.RoundCurrency` tutarlı kullanılıyor, `double` yok.
- **Eşzamanlılık:** sipariş kabul/tahsilat/çapraz kanal arbitrajında kilitleme sırası genel olarak tutarlı ve doğru (Production modülü hariç, bkz. Orta bulgular).
- **NFC/CustomerWeb:** yaş kısıtı gerçekten sunucuda zorlanıyor, XSS/IDOR bulunamadı.

## 7. Öneri

Bu rapor yalnızca tespit amaçlıdır, hiçbir kod değişikliği içermez. Önerilen sıralama:

1. **K1-K4, K9, K10** (para/tahsilat + kendi kendini onaylama + erişilemeyen güvenlik uç noktaları) — en yüksek finansal/güvenlik riski.
2. **K5-K6** (mutfak/stok kaybı) — operasyonel olarak günlük fark edilen hatalar.
3. **K11-K13** (kalite kapıları) — düzeltilmezse gelecekteki tüm "Done" iddiaları güvenilirliğini kaybeder.
4. **K7-K8, K14-K18** — sessiz veri bütünlüğü riskleri, tetiklenme ihtimaline göre önceliklendirilebilir.
5. Orta/düşük bulgular — ayrı görevler olarak zamanla ele alınabilir.
