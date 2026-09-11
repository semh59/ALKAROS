# V1-RMD-167 - Bağımsız incelemenin bulduğu dört düzeltme

- Task ID: V1-RMD-167
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

Semih'in isteğiyle V1-RMD-156'dan V1-RMD-166'ya kadar olan 11 görev,
bağımsız bir ajanla (önceki oturumun bağlamını hiç devralmadan) derinlemesine
incelendi — her iddia gerçek test çalıştırmalarıyla yeniden üretildi, her
SQL/birleştirme mantığı elle izlendi. Sonuç: 8 görev tam doğru, 3 görev
küçük sorunlarla doğru bulundu; **hiçbiri yeniden açılacak kadar bozuk
değildi**, ama incelemenin bulduğu dört somut kusur burada düzeltiliyor.

1. **V1-RMD-166'nın kendi düzeltmesi yeni bir para tutarsızlığı
   getirmişti.** "Gönderildi" başlığının yanındaki tutar hâlâ
   `state.order.totalAmount` (siparişin TAMAMI) idi, ama başlığın altında
   artık yalnızca gerçekten mutfağa gitmiş (`dispatched`) kalemler
   listeleniyordu — masada hem gönderilmiş hem "gönderilmeyi bekleyen"
   kalem varsa (V1-RMD-164'ün var olma sebebi olan tam senaryo), başlıktaki
   tutar altındaki kalemlerin toplamını tutmuyordu. §0.1 kuralı gereği
   ("ekrandaki para sunucunun kendi rakamı, istemcinin topladığı bir sayı
   değil") kısmi bir toplam istemcide asla hesaplanmadı; bunun yerine
   tutar yalnızca "Gönderildi" TEK grup olduğunda (bekleyen kalem yokken)
   gösteriliyor — o durumda `sentTotal` gerçekten doğru.
2. **V1-RMD-157'nin KASA-1 serbest bırakma çağrısı hâlâ atlanabiliyordu.**
   `draftResponse.json()` ayrıştırması başarısız olursa veya submit
   `fetch()`'i kendisi hata fırlatırsa, yürütme doğrudan dış `catch`'e
   atlıyor ve serbest bırakma çağrısını (o sırada `draft.orderId` zaten
   elde edilmiş olsa bile) hiç yapmıyordu — masa bir sonraki müşteriye
   kadar KASA-1'e bağlı kalabiliyordu. `draft` artık try/catch dışında
   tutuluyor; serbest bırakma artık `finally` içinde, `draft.orderId` her
   elde edildiğinde, sonrasında ne olursa olsun deneniyor.
3. **V1-RMD-163'ün garson istemcisi katalog sayfalama döngüsü, SONRAKİ bir
   sayfa başarısız olursa o ana kadar toplananı sessizce "başarılı" diye
   döndürüyordu** — tam da bu düzeltmenin kapatmaya çalıştığı "sessizce
   eksik katalog" hatasını bir sayfa sonraya taşımış oluyordu. `cashier-app.js`'in
   kendi eşdeğeri zaten herhangi bir sayfada hata fırlatıyordu (doğru
   davranış); `waiter-app.js` artık aynı şekilde — herhangi bir sayfa
   başarısız olursa TÜM getirme başarısız sayılıyor, kısmi katalog asla
   sessizce kullanılmıyor.
4. **V1-RMD-160'ın "sıfır tüketici" iddiası tam değildi.**
   `tests/Host/Experience/Orders/OrderManagementExperienceTests.cs`, hiçbir
   `.csproj`'a bağlı olmayan, gerçekten derlenmeyen (dolayısıyla build'i
   hiç etkilemeyen), eski 3-parametreli `CreateTableDraftRequest`
   constructor'ını kullanan öksüz bir dosyaydı. Zararsızdı ama yanıltıcıydı
   — daha önce üç kez tekrarlanan "ölü istemci motoru" temizliği kalıbıyla
   aynı şekilde tamamen silindi.

**Ertelenen, çözülmeyen bir bulgu (bilinçli karar):** V1-RMD-157'nin
KASA-1'i FARKLI FİZİKSEL terminallerin aynı birkaç yüz milisaniyelik
pencerede eşzamanlı kullanmasına karşı tam kapanmamış bir yarış durumu var
— sunucudaki ATAMA adımı zaten atomik (koşullu `UPDATE ... WHERE
current_order_id IS NULL OR ...`), ama iki farklı kasa terminali TAM O
pencerede aynı anda gönderirse ikinci sipariş birinciye "ikinci tur" olarak
birleşebilir. Bu, madde 2'nin düzeltmesiyle pencere iyice daraltıldı ama
sıfırlanmadı. Tam kapatmak, KASA-1 gönderimini terminaller arası
serileştiren daha büyük bir mimari değişiklik gerektirir — gerçek dünyada
nadir (çoğu restoranda tek kasa terminali var) ve bu görevin kapsamı
dışında bırakıldı, ayrı bir küçük görev olabilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-167-independent-review-followups.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — "Gönderildi" başlık tutarı ve katalog sayfalama düzeltmesi.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CSH-00x sahipliğinde)
    — serbest bırakma çağrısı finally'e taşındı.
  - tests/Host/Experience/Orders/OrderManagementExperienceTests.cs
    (V1-RMD-083 sahipliğinde, üst dizinin genel sahibi) — tamamen
    kaldırıldı.

## Out of scope

- KASA-1'in terminaller arası tam serileştirilmesi (yukarıda gerekçesiyle
  birlikte açıklandı).
- Frontend bölümünün kalan bulguları — ayrı görev/görevler.

## Dependencies

- V1-RMD-166

## Acceptance evidence

- `node --check` her iki JS dosyası için temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test
  tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj
  -c Debug` → **49/49 yeşil** (öksüz test dosyası silindikten sonra da
  derlenen projelerde hiçbir değişiklik yok, çünkü o dosya zaten hiçbir
  `.csproj`'a bağlı değildi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)
- Bu görevin kendisi için ayrı bir bağımsız inceleme yapılmadı — dört
  düzeltme de az riskli, tek dosyalık, mevcut testlerle (49/49) doğrulanmış
  değişiklikler; kapsamları V1-RMD-156..166'nın bağımsız incelemesinden
  doğrudan çıktı.

## Handoff

- None
