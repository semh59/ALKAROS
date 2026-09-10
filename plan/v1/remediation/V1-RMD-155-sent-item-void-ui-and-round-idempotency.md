# V1-RMD-155 - Gönderilmiş kalemin iptali ve tur başına idempotency

- Task ID: V1-RMD-155
- Status: Done
- Assignee: Claude Opus 5
- Work type: remediation
- Surface state: Existing

## Goal

Garsonun elindeki işi bugün bozan iki şey. Biri denetimin bulgusu, diğerini
V1-ORD-006'yı incelerken buldum ve **kendi işimin kusuru**.

**1. Gönderilmiş kalem hiç iptal edilemiyor.** V1-RMD-154 ucuz yolu
(`/void`) haklı olarak kapattı: mutfağa gitmiş kalem artık `Sent` bildiriyor
ve yönetici onayı olmadan iptal edilemiyor. Ama doğru yolun —
`POST .../items/{itemId}/void-sent` — **hiçbir istemcisi yok** (denetimin D1
bulgusu; `src/Clients` altında geçen tek satır benim yorumum). Yani güvenlik
açığı kapandı, yerine kullanılabilirlik boşluğu geldi: garson yanlış giden
bir tabağı sistemde hiçbir şekilde iptal edemiyor.

**2. Aynı hesabın ikinci turu mutfağa hiç ulaşmıyor.** İstemci her gönderimde
`operationId` olarak `{orderId}:submit` yolluyor — turlar V1-ORD-006 ile tek
siparişte birleştiği için bu **her tur için aynı anahtar**. İkinci tur aynı
anahtarla gelince `SubmitOrderHandler` saklı kaydı buluyor, hash'i
karşılaştırıyor (`ExpectedRowVersion` değiştiği için farklı) ve
`IDEMPOTENCY_KEY_REUSED` atıyor. İkinci tur 409 alıp "hatalı" listesine
düşüyor.

Testlerim bunu yakalamadı çünkü yazdığım yardımcı her tura rastgele bir
`operationId` veriyordu — **test gerçek istemciden saptığı için kusuru
maskeledi.** Yardımcı istemcinin şemasını birebir kullanacak şekilde
düzeltilir ki bu bir daha gizlenemesin.

Aynı hash, denetimin H3 bulgusunun da kökü: cevabı kaybolan bir gönderim
tekrarında `table-draft` siparişi doğru şekilde tekrar oynatıyor ama
*güncel* satır sürümünü döndürüyor, dolayısıyla tekrar her zaman farklı
hash üretip 409 alıyor; istemci 4xx'i kalıcı sayıp siparişi "hatalı"ya
atıyor, garson yeniden giriyor ve mutfağa aynı yemek ikinci kez gidiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-155-sent-item-void-ui-and-round-idempotency.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Modules/Orders/SubmitOrder/SubmitOrderRequestHash.cs (V1-ORD-004
    sahipliğinde) — satır sürümü hash'ten çıkar.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js,
    src/Clients/WaiterPwa/wwwroot/index.html (V1-WTR-010 sahipliğinde) —
    tur başına operationId ve gönderilmiş kalem iptali.
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs (V1-ORD-001
    sahipliğinde) — agreganın tek anlık görüntüyle okunması.
  - tests/Host/Experience/Orders/TableDraft/**,
    tests/Modules/Orders/SubmitOrder/** (ilgili görevlerin sahipliğinde).

## In scope

1. **`operationId` turu tanımlar.** İstemci, o turun `table-draft` gövdesinde
   zaten taşıdığı gönderim kimliğini kullanır: tekrar denemede aynı, yeni
   turda farklı. Böylece tekrar gerçekten tekrar oynatılır, yeni tur yeni
   işlem sayılır.
2. **Hash satır sürümünü içermez.** `SubmitOrderRequestHash` bugün
   `ExpectedRowVersion`'ı hash'liyor; oysa gerçek bir tekrarda bu değer
   zorunlu olarak değişiyor — `SubmittedAt` V1-RMD-113'te tam olarak aynı
   gerekçeyle çıkarılmıştı. Anahtar "hangi sipariş, kim, neden" der; iyimser
   eşzamanlılık jetonu kimliğin parçası değildir. Sürüm kontrolü zaten
   `SaveAsync`'in kendi `WHERE row_version = @expected` koşulunda yapılıyor,
   yani koruma kaybolmuyor.
3. **Test yardımcısı istemcinin şemasını kullanır.** Rastgele
   `operationId` üreten yardımcı, gerçek istemcinin ürettiği değeri
   üretecek şekilde düzeltilir.
4. **Gönderilmiş kalem iptali arayüzü.** Adisyonda mutfağa gitmiş satır için
   ayrı bir eylem: gerekçe seçilir, `void-sent` çağrılır. İki sonuç ayrı
   anlatılır — `Applied` (yetkisi olan rol) ve `Accepted`/`Pending` (yönetici
   onayı bekleniyor). Onay sonrası tekrar denemede **aynı `IdempotencyKey`**
   kullanılır ki aynı talebe çözülsün, ikinci bir talep açılmasın.

## Out of scope

- Yöneticinin onay ekranı (V1-IAM-020 karar yüzeyi): kendi görevi.
- `/comp` ve `/transfer-server`'ın istemcisizliği.
- Denetimin kalan bulguları (veritabanı kısıtları, erişilebilirlik, sınırsız
  girdi boyutları, hız sınırları, katalog sayfalaması).

## Dependencies

- V1-RMD-154

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata. Etkilenen 20 test projesinin
  tamamı yeşil (TableDraft 42, Kitchen'ın beşi, NFC, QR, Billing, Tables,
  WebPush, MigrationComposition 134 dahil).
- **Testlerin boş olmadığı deneyle kanıtlandı.** Eski `operationId` şeması
  (`{orderId}:submit`) geçici olarak geri konuldu: iki test düştü
  (`EachRoundOnACheckIsItsOwnOperation…` ve
  `ASecondRoundFiresOnlyItsOwnLine…`), sonra geri alındı. Bu sırada şu da
  görüldü: hash düzeltmesi **tek başına** olsaydı ikinci tur 409 yerine
  *sessizce hiç gönderilmez*di — aynı anahtar artık aynı hash'e denk geldiği
  için birinci turun cevabı tekrar oynatılırdı. İki düzeltme birlikte
  gerekiyor.
- Yeni testler: bir turun tekrarı 409 yerine tekrar oynatılıyor ve hiçbir şey
  iki kez pişmiyor/tüketilmiyor; aynı hesabın iki turu ayrı işlem sayılıyor.
- Arayüz tarayıcıda denendi (üç durumlu adisyon): mutfaktaki satırda
  *İptal iste*, gönderilmemiş satırda *İptal*, servis edilmiş satırda hiçbir
  düğme yok. Onay akışı: ilk deneme 202 → "İptal yönetici onayına
  gönderildi"; yönetici onayladıktan sonraki deneme **aynı
  `idempotencyKey`** ile gidiyor (ikinci bir talep açmıyor) ve
  "Ürün iptal edildi, stok geri alındı" dönüyor.

### Bu turda bulunan ve düzeltilen ikinci kusur (yırtık okuma)

Süpürmede `SubmitOrder` testlerinden biri düştü. **Önce "build yarışı" dedim,
yanlıştı** — tek başına tekrar çalıştırınca 5'te 1 düştü, yani gerçekten
dalgalıydı. `git stash` ile temel sürüme dönüldü ve aynı test orada **8/8
temiz** çıktı: dalgalanmayı benim değişikliğim getirmişti.

Sebep: `PostgresOrderRepository.GetByIdAsync` sipariş, kalemler, eklentiler ve
geçmişi **dört ayrı sorguda, hiçbir işlem içinde olmadan** okuyor. READ
COMMITTED altında her ifade kendi anlık görüntüsünü alır, dolayısıyla eşzamanlı
bir yazar araya girdiğinde **yırtık bir sipariş** kurulabiliyor: sipariş satırı
commit öncesinden, kalemleri commit sonrasından.

Eskiden zararsızdı, çünkü bu yoldaki tek okuyucu yalnız siparişin durumuna
bakıyor ve çakışmayı biraz sonra satır sürümü kontrolü yakalıyordu.
V1-ORD-006'nın `FireRound`'u kalemlere de baktığı için zararsız olmaktan
çıktı: hâlâ Draft görünen ama kalemleri başka bir işçi tarafından ateşlenmiş
bir siparişte "ateşlenecek kalem yok" diye sert hata veriyordu.

Düzeltme: dört okuma tek `RepeatableRead` işleminde, tek anlık görüntüyle
yapılıyor. **Düzeltmeden sonra aynı test 12/12 temiz.**

- `node --check waiter-app.js`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: denetim yine bir kez
  haklı çıktı (yorumumda Türkçe alıntı), düzeltildi. Kalan tek ihlal
  `InventoryAdjustmentService.cs:96`, diff'te değil.

## Handoff

- None
