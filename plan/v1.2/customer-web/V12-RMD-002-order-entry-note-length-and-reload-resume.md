# V12-RMD-002 - Sipariş notu uzunluğu ve sekme yenilemede devam etme düzeltmesi

- Task ID: V12-RMD-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md` (§0, madde 4 ve 5)
yazılırken bulunan iki gerçek/olası boşluğu kapatmak: Order Entry not
alanında istemci-taraflı uzunluk sınırının eksik olması, ve sayfa
gönderim sırasında yenilenirse misafirin devam eden siparişinin
kaybolup kaybolmadığı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.js
  (V12-CWB-002 sahipliğinde kalır) — yalnız not alanı `maxlength` ve
  sunucu-kabullü devam etme dalı eklendi; mevcut gönderim/polling akışı
  değişmedi.
- `plan/v1.2/customer-web/V12-RMD-002-order-entry-note-length-and-reload-resume.md`

## In scope

- Her iki bulguyu da hiçbir şey değiştirmeden önce gerçek koda bakarak
  doğrulamak.
- Not alanına `maxlength="200"` eklemek (WaiterPwa/Cashier'ın kendi eş
  alanlarıyla aynı, tutarlılık için) — gerçekten eksikti, doğrulandı.
- Yeniden-açılışta kaybolan sipariş sorunu gerçekse düzeltmek — gerçek
  olduğu doğrulandı, aşağıya bakınız.

## Out of scope

- `SpecialInstructions` için backend/sunucu-taraflı uzunluk doğrulaması —
  araştırıldı, yalnız bu QR yolunda değil, Orders domain'inin tamamında
  hiçbir yerde böyle bir sınır olmadığı doğrulandı. Bunu düzeltmek başka
  görevlerin owned surface'ına dokunur ve ayrı, daha büyük bir bulgu —
  burada kayda geçirildi, düzeltilmedi.
- Tam bir CustomerWeb Playwright suite'i — bu küçük düzeltmenin kapsamı
  dışında; ana test planının Faz 6'sı bunu zaten kapsıyor.
- "Sunucu aynı submissionId altında yeniden gönderilen düzenlenmiş bir
  sepeti yok sayıyor" davranışı — araştırıldı, gerçek (uç noktanın
  idempotency'si yalnız `submissionId`'ye göre, sepet içeriğine göre
  değil), ama bu düzeltme artık sepeti yeniden düzenlenebilir halde
  göstermek yerine doğrudan polling'e döndüğü için bu senaryoyu
  arayüzden pratik olarak erişilemez kılıyor. Sunucu katmanında ayrıca
  düzeltilmedi.

**Araştırma bulguları — Bulgu 4 (not uzunluğu) — gerçek, düzeltildi.** `order-entry.js`'nin sepet
satırı not `<input>`'unda hiç `maxlength` yoktu, WaiterPwa/Cashier'ın kendi
eş alanlarının (ikisi de 200 sınırlı) aksine. Sunucu tarafı:
`src/Host/Experience/**` ve `src/Modules/Orders/**` altında
`SpecialInstructions`/`Notes`'a referans veren her gerçek dosya tarandı —
**bu domain'in hiçbir yerinde**, yalnız bu QR yolunda değil, uzunluk
doğrulaması bulunmuyor. Bu, bu görevden daha büyük, ayrı bir bulgu
(Orders'ın tüm not alanı sunucu tarafında sınırsız); Out of scope'ta
kayda geçirildi, bu görevin owned surface'ı altında sessizce
genişletilmedi.

**Bulgu 5 (yeniden açılışta devam etme) — kısmen gerçek, ulaşılabilir
senaryo için düzeltildi.** `SUBMISSION_ID_STORAGE_KEY`/`CART_STORAGE_KEY`
ikisi de `sessionStorage`'da yaşıyor — bu, gerçek bir sekme kapatmasında
**hayatta kalmaz** (yalnız aynı sekmede yenileme veya sekme-içi
navigasyonda kalır) — yani denetimin "misafir sekmeyi kapatıp yeniden
açar" ifadesi birebir gerçekleşmiyor: gerçekten yeni bir sekme gerçekten
boş bir `sessionStorage` alır, dolayısıyla boş bir sepet, dolayısıyla
devam ettirilecek hiçbir şey yok. GERÇEK, ulaşılabilir boşluk şu:
**gönderim devam ederken aynı sekmede yenileme** (kopan bağlantı,
sabırsız bir yenileme): `init()` daha önce hiç kalıcılaşmış bir
`submissionId` olup olmadığını kontrol etmiyordu ve her zaman (henüz
temizlenmemiş) sepet formunu yeniden çiziyordu, misafiri yeniden
göndermeye davet ediyordu. `GET /api/v1/qr/orders/{submissionId}`
(`QrOrderingEndpoints.cs`) üzerinden doğrulandı: bilinmeyen/süresi
dolmuş bir id asla 404 vermiyor — sentetik bir `"Pending"` yanıtı
dönüyor (`store.FindResultingOrderAsync`'in null dönmesi `Pending`'e
eşleniyor, "henüz `orders.orders` satırı yok" için kullanılan aynı yer
tutucu) — yani devam ettirilen bir polling, gerçekten bayat/yabancı bir
id'ye karşı bile çökmüyor (en sonunda "biraz uzun sürüyor" mesajı).
**Ancak bu, ilk taslak düzeltmede gerçek bir regresyon riskini gizledi:**
`readOrCreateSubmissionId()` id'yi istek gönderilmeden ÖNCE kalıcılaştırır.
Gönderim ağ hatasıyla başarısız olursa id yine de kalır; yalnızca "id var mı"
diye bakan bir devam-etme kontrolü, sunucunun hiç görmediği bir siparişi 60
sn boyunca poll'lar, sepeti ve gönder düğmesini gizler ve misafiri yeniden
deneyemez halde bırakırdı (eski davranışta sepet görünür kalır, aynı id ile
güvenle yeniden denenebilirdi). Bu, bağımsız incelemede yakalandı.

**Düzeltme**: sunucu gönderimi gerçekten kabul ettiğinde (202 yanıtı
geldiğinde) ayrı bir `alkaros.qr.submissionAccepted` bayrağı yazılıyor
(`markSubmissionAccepted()`); `init()` yalnızca hem id hem bu bayrak varsa
(`readAcceptedSubmissionId()`) sepeti çizmeden `pollUntilMaterialized`'a
dönüyor. Başarısız/yarım kalmış bir gönderimde bayrak yok, sepet eskisi gibi
görünür ve aynı id ile idempotent olarak yeniden denenebilir.
`clearSubmissionId()` bayrağı da temizliyor. Sunucunun `submissionId`-anahtarlı
idempotency'sinin yok sayacağı farklı içerikle yeniden gönderilebilecek
düzenlenebilir bir sepet gösterilmemiş oluyor.

## Dependencies

- None

## Deliverables

- Sınırlı ek olarak sahiplenilen order-entry.js dosyasında:
  `readAcceptedSubmissionId()`, `markSubmissionAccepted()`, `init()`'in
  yeni devam-etme dalı (yalnız sunucu kabulünden sonra), not alanına
  `maxlength="200"`.

## Acceptance evidence

- `node --check src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.js` →
  temiz.
- Manuel kod izleme (bu istemci için mevcut bir test altyapısı yok —
  doğrulandı, ana test planının kendi bulgusuyla aynı; tam bir suite
  oradaki Faz 6'nın kapsamında, bu görevin değil): sunucu tarafından
  KABUL EDİLMİŞ bir `submissionId` (bayrak dahil) ile sayfa yüklemesi
  doğrudan `pollUntilMaterialized`'a atlıyor; id var ama bayrak yokken
  (gönderim hiç tamamlanmadı) sayfa yüklemesi sepeti öncekiyle birebir aynı
  şekilde gösteriyor; ikisi de yokken davranış değişmedi. Gerçek tarayıcıda
  sürülmedi — yalnız `node --check` ve kod izleme.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
