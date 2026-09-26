# V12-REC-001 - Implement online order reconciliation

- Task ID: V12-REC-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.21
- PDF:II.3.15
- PDF:II.5.12
- PDF:II.6.11
- PDF:III.23

## Goal

Local/provider Order, status, cancellation ve stock outcome farklılıklarını tespit etmek ve izlemek.

## Owned surface

- `src/Modules/Reconciliation/OnlineOrders/**`, `tests/Modules/Reconciliation/OnlineOrders/**`,
  `database/migrations/V12/V12-REC-001/**`
- `src/Host/Experience/Reconciliation/OnlineOrderReconciliationEndpoints.cs` — tarama, retry ve çözüm uçları; bu
  görevle oluşturulan yeni dosya.
- `src/Host/Experience/Reconciliation/OnlineOrderingInboxReprocessing.cs` — yeniden işleme portunu OnlineOrdering
  inbox sözleşmesine bağlayan adaptör; bu görevle oluşturulan yeni dosya.
- `evidence/V12-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Host/Composition/Modules/ModuleRegistry.cs ve tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs —
    yeni `Reconciliation.OnlineOrders` modülünün kaydı ve onaylı bağımlılığı (yalnız `Reconciliation`).
  - src/Host/DualScreen/DualScreenApplication.cs — yeni uçların ve adaptörün kaydı.
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/YemeksepetiInboxProcessingStore.cs (V12-ONL-002
    sahipliğinde) — yalnız `ReopenForReprocessingAsync`; Reconciliation başka modülün satırına kendisi yazmaz.
  - tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — katalog sayısı 32 → 33.
  - tests/Host/Experience/Reconciliation/ (V1-RMD-250 sahipliğinde) — yeni OnlineOrderReconciliationHttpTests.cs.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 149 numaralı migration konumu.
  - ALKAROS.slnx ve `dotnet restore`'un ürettiği packages.lock.json.
- Tasarım notu: kaynaklar V13-REC-001'in okuma modeli deseniyle, düz SQL ile ve şemalar arası salt okunur; modül
  yalnız `Reconciliation`'a bağlıdır. Beş kaynak çifti vardır, her biri V1-REC-001 vakası (`OnlineOrderMismatch`)
  üretir ve tekilleştirmeyi V1-REC-001 servisi yapar:
  - Sağlayıcı kabul etti / yerelde reddedildi. Sonraki eylem: eşleme düzeltilip olayın yeniden işlenmesi, iptal
    istenmişse iptalin yeniden gönderilmesi.
  - Yerelde kabul edildi / sağlayıcı bilmiyor, yani durum bildirimi outbox'ta `dead`. Sonraki eylem: yeniden
    gönderim.
  - İşlenemeyip kapanan sağlayıcı olayı. Sonraki eylem: yeniden işleme.
  - Teslimden sonra iptal. Yeniden denenecek bir şey yoktur; kişi kararıyla kapanır ve bir daha açılmaz.
  - Kanala ulaşmayan stok adedi. Yayıncı zaten her turda yeniden dener; sonraki eylem kanal bağlantısını
    kontrol etmektir.
  Olayı yeniden işleme OnlineOrdering sözleşmesinden (`IProviderEventReprocessing` portu, Host adaptörü) geçer;
  outbox yeniden kuyruğa alma ise paylaşılan outbox tablosu üzerindedir.
  Retry, kaynağın hâlâ hatalı durumda olmasına koşullu tek bir etki yapar ve etkisiyle aynı transaction'da
  `reconciliation.online_order_retry_attempts` tablosuna yazılır; ardından vakaya not düşülür. Çözüm, kaynak yeniden
  okunarak yapılır: kaynak hâlâ farklılık gösteriyorsa vaka kapatılamaz. Çözüm her zaman not ister ve V1-REC-001
  geçişiyle, sürüm kontrollü ve denetimli yapılır. Sağlayıcı tarafı V12-ONL-003'ün doğrulanmamış taslağıdır
  (V0-YSP-001 `Blocked`, `V12-GOV-004`). Burada yalnız yerel kanıt (inbox, outbox, sipariş, stok durumu)
  okunur; hiçbir şey "sağlayıcıda doğrulandı" sayılmaz.

## In scope

- Eşleştirilmiş referanslar, açık vaka tekilleştirme, retry eylemi ve denetlenmiş çözüm.

## Out of scope

- Birleşik kontrol paneli ve genel mutabakat yaşam döngüsü.

## Dependencies

- V12-ONL-002
- V12-ONL-003
- V12-ONL-005
- V12-STK-001
- V13-REC-001

## Deliverables

- `src/Modules/Reconciliation/OnlineOrders/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider kabul edildi/yerel reddedildi ve yerel kabul edildi/provider bilinmiyor her biri güvenli bir sonraki eylemle
  bir vaka oluşturur.
- `V12-REC-001` kanıtlı `NotApplicable` ise online order reconciliation vaka üretimi yerel/provider Order, status ve
  stock outcome kaynaklarıyla bağımsız olarak yine doğrulanır.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): modül testleri 15/15, Host HTTP testleri 10/10,
  (2 tanesi yeni), MigrationComposition 161/161 yeşil. Sonuçlar:
  - Sağlayıcının kabul edip yerelin reddettiği sipariş tek bir vaka açar; ikinci tarama yeni vaka açmaz. Sonraki eylem
    eşleme hatasında "yeniden işle", iptal istenmişse "iptali yeniden gönder"dir.
  - Yerelde kabul edilip sağlayıcıya ulaşmayan her ölü durum bildirimi, sipariş tutarıyla bir vaka açar. Hâlâ
    denenen bildirim ve yerel siparişi olmayan bildirim bu tarafta vaka açmaz.
  - Sağlayıcının zaten sonlandırdığı ret vaka açmaz: sipariş sonradan oluşmuş, sağlayıcı iptali gelmiş ya da iptal
    bildirimi ulaşmış olabilir.
  - İki yönetici aynı anda retry yaparsa tek etki olur: biri `Requeued`, diğeri `NothingToRetry` alır ve iki deneme
    de iz tablosuna ve vaka notlarına yazılır.
  - Kaynak hâlâ farklıyken vaka çözülemez. Kaynak düzelince çözülür; eski sürümle yapılan çözüm reddedilir.
  - Yeniden işlenip bu kez iptal istenerek yine reddedilen olay ikinci kez yeniden işlenmez.
  - Başarısız sağlayıcı olayı vaka açar ve retry onu yeniden işlemeye alır.
  - Teslimden sonra iptal Critical vaka açar ve retry edilemez. Not olmadan çözülemez, çözüldükten sonra bir daha
    açılmaz.
  - Stok farkı yalnız tolerans veya tekrarlanan başarısız teslimden sonra vaka olur ve teslim olunca çözülür.
  - Reddedilen (Dismissed) ret vakası yeniden açılmaz.
  - Online sipariş olmayan vakada retry/çözüm reddedilir.
  - Eş zamanlı dört tarama bir farklılık için tek vaka bırakır.
  - Çöken bir kaynak yalnız kendi sonucunda ve Türkçe raporlanır.
  - Migration 149 geri alınıp yeniden uygulanır.
  - HTTP tarafında oturumsuz çağrı 401, izinsiz ve yalnız görüntüleme yetkili çağrı 403 alır. Beş kaynağın hepsi
    gerçek şemaya karşı hatasız çalışır.
  - Sağlayıcı tarafının gerçek davranışı doğrulanmadı (V0-YSP-001 `Blocked`); yalnız yerel kanıt okunur.
- Mutasyon kontrolü (dosya yedekten geri yüklenip `cmp` ile doğrulandı): 13 mutasyonun 13'ü de en az bir testi
  kırmızıya çevirdi. Denenen mutasyonlar:
  - ölü bildirim koşulu kaldırıldı;
  - çözümdeki kaynak kontrolü atlandı;
  - ulaşmış iptal koşulu kaldırıldı;
  - teslim sonrası iptalde kapalı vaka koruması kaldırıldı;
  - başarısız teslim eşiği kaydırıldı;
  - ölü bildirim filtresi kaldırıldı;
  - Türkçe hata yerine ham istisna mesajı gösterildi;
  - deneme sayacı sıfırlanmadı;
  - aktif vaka kontrolü kaldırıldı;
  - Dismissed koruması kaldırıldı;
  - iptal istenmiş olayı yeniden işleme koruması kaldırıldı;
  - sonraki eylem seçimi sabitlendi;
  - not zorunluluğu kaldırıldı.
- Kanıt: `evidence/V12-REC-001/`.

## Handoff

- V15-REC-001
- V15-REC-002
