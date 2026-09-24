# V13-RMD-003 - Cashier split-payment ekranı için gerçek tarayıcı (Playwright) E2E paketi

- Task ID: V13-RMD-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: validation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md`'nin Faz 1 önerisini
uygulamak: Faz 2 boyunca yazılan ve tek başına para hareketi yapan
`src/Clients/Cashier/wwwroot/payments/split-payment/` ekranının, bağımsız
denetimin "tarayıcıda doğrulanamadı" diye açık bıraktığı özelliklerini gerçek
Postgres + gerçek Host + gerçek Chromium ile kalıcı spec'lere dönüştürmek.
Özellikle: kart tahsilatının asla sahte onay üretmemesi ve çözülmemiş kart
tahsilatı kilidinin sayfa yenilemesinde korunması (`V1-RMD-258`/`V13-RMD-002`),
EFT onay kutusu kapısı (`V13-PUI-004`), odak korunumu (`V13-RMD-GOV-001`) ve
eşzamanlı aşırı-tahsis yarışının Türkçe 409'a eşlenmesi (`V1-RMD-258`).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/07-split-payment-bankcard-lock.spec.js
  (V1-CUI-011 sahipliğindeki Cashier E2E paketine eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/08-split-payment-eft.spec.js
  (aynı paket, yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/09-split-payment-race-and-edges.spec.js
  (aynı paket, yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/10-split-payment-cash-and-mixed.spec.js
  (aynı paket, yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/paymentHelpers.js
  (aynı paket, yeni yardımcı: gerçek HTTP uçlarıyla Bill üretir)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/lib/seed.js
  (V1-CUI-011 sahipliğinde kalır — yalnız ayrı bir `E2E Ödeme Ürünü` eklendi,
  mevcut iki ürün ve stokları değişmedi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/02-stock-badge-and-kitchen-dispatch.spec.js
  (V1-CUI-011 sahipliğinde kalır — yalnız bayat `dialog` beklentisi toast
  beklentisine çevrildi, aşağıya bakınız)
- `plan/v1.3/payments-ui/V13-RMD-003-cashier-split-payment-e2e-suite.md`

## In scope

1. Her senaryo kendi Bill'ini, üretim kodunun kullandığı gerçek HTTP
   uçlarıyla üretir (giriş, `table-draft`, `submit-draft`,
   `send-to-cashier`, `billing/bills/from-order`) — doğrudan SQL yok. Her test
   taze bir Playwright bağlamı ve taze bir `terminalId` ile başladığı için
   kasa oturumu durumu testler arasında sızmaz.
2. Spec 07 — kart tahsilatı: BankCard her zaman "Manuel mutabakat gerekiyor"
   döner, asla "Hesap Ödendi" görünmez, sunucuda tahsis oluşmaz ve
   `unsettledPayment` raporlanır; sayfa yenilemesi ve taze bir ikinci sekme
   aynı kilidi görür; arayüzü tamamen atlayan doğrudan istekler `409
   TENDER_UNSETTLED_PAYMENT_EXISTS` ile reddedilir.
3. Spec 08 — EFT: onay kutusu işaretlenmeden düğme pasif ve zorlanmış tıklama
   istek üretmez; para üstü hiçbir yerde yok; kasa oturumu olmadan da
   kullanılır; onay yöntem değişince ve her başarılı tahsilattan sonra
   sıfırlanır; klavye odağı yeniden çizimlerde korunur; not alanına yazılan
   öznitelik/HTML enjeksiyonu çalışmaz; sınır değerler (kalanı bir kuruş
   aşan, sıfır, boş) istek çıkmadan reddedilir.
4. Spec 09 — 4 eşzamanlı 40 TL'lik EFT tahsilatı 100 TL'lik hesaba karşı:
   tam olarak 2'si uygulanır, kalanlar ham 500 değil Türkçe tipli 409 alır;
   aynı idempotency anahtarı ikinci tahsis üretmez; eşit bölüşümün kuruş
   kalıntısı gizlenmez; ödenmiş hesap yenilemede formu açmaz; billId
   yok/bilinmeyen/oturumsuz durumları güvenli ekranlar gösterir.
5. Spec 10 — Nakit (açık kasa oturumuyla) ve Nakit+EFT karması yalnız sunucu
   tahsisleri hesabı tamamlayınca kapanır; kalanı aşan nakit tutar istek
   çıkmadan reddedilir; bir sekmede kapanan hesap ikinci sekmede sunucudan
   reddedilir.
6. Suite'i ilk kez bu görevde tam koşturmak, daha önce var olan bir spec'in
   (02) sessizce bozuk olduğunu ortaya çıkardı; düzeltildi (aşağıda).

## Out of scope

- `split-payment.js`'nin kendisinde herhangi bir davranış değişikliği —
  spec'ler mevcut davranışı sabitler, kodu değiştirmez (mutasyon kontrolleri
  geçici olarak bozup geri aldı, `git diff` boş).
- Master planın diğer fazları (WaiterPwa offline kuyruk/kiosk kilidi,
  PosTerminal, CustomerWeb).
- Madde-bazlı bölüşüm (V13-PUI-001'de kapsam dışı bırakıldı) ve kalıcı bir
  sunucu tarafı "Unknown" çözümü (gerçek terminal `V13-HUG-001` gerektiriyor).
- `V13-RMD-002`'de bulunan ve burada da doğrulanan davranışın ötesinde yeni
  bir ürün kararı.

## Dependencies

- V13-PUI-001
- V13-PUI-004
- V13-PAY-005
- V13-RMD-002
- V1-CUI-011

## Acceptance evidence

- Tüm Cashier suite'i (`tests/E2E/Cashier`, gerçek `alkaros-test-pg` port
  55432, gerçek `ALKAROS.Host.dll serve`, gerçek Chromium): **23/23 geçti**
  (önceki 6 spec dosyasından 7 test + bu görevin 16 yeni testi).
- **Mutasyon kontrolü (spec'lerin gerçekten bir şey yakaladığının kanıtı):**
  `split-payment.js` üzerinde geçici olarak (a) `loadEverything` kilit
  türetmesi `state.locked = false` yapıldı → spec 07'nin yenileme
  adımı beklenen satırda başarısız oldu; (b) yöntem-chip'inin yeniden odaklama
  satırı kaldırıldı → odak testi başarısız; (c) `escapeHtml`'den `"` ve `'`
  çıkarıldı → not enjeksiyonu testi başarısız; (d) EFT düğmesinin `disabled`
  koşulundan onay kutusu çıkarıldı → kapı testlerinin ikisi başarısız. Her
  mutasyon geri alındı ve `git diff -- src/` boş, testler tekrar yeşil.
- Yarış testi `--repeat-each=6` ile 6/6 geçti (flaky değil). Sunucu kilidi
  olmasaydı 4 isteğin 4'ü başarılı olup test kırılırdı.
- **Bulunan gerçek, önceden var olan kusur:** `V1-RMD-253` (`b7d46aae`)
  Cashier'ın engelleyici `alert()`'lerini toast'a çevirmişti ama
  `specs/02-stock-badge-and-kitchen-dispatch.spec.js` hâlâ bir `dialog`
  olayı bekliyordu; bu yüzden o commit'ten beri sessizce başarısızdı (bu
  suite CI'da koşmuyor). Toast (`role="status"`) beklentisine çevrildi.
- Dürüstçe belirtilen sınırlar: mutasyon kontrolü yalnız istemci tarafı
  düzeltmeler için yapıldı; eşzamanlı-yarış testinin dayandığı sunucu kilidi
  için C# mutasyonu yapılmadı (yeniden derleme maliyeti), yerine tekrarlı
  koşuyla kararlılık gösterildi. Gerçek cihaz/ekran okuyucu doğrulaması yok.
  Suite yalnız Chromium masaüstü görünümünde koşar (mevcut paketle aynı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
