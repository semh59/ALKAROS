# V1-WTR-010 - Garson ekranı Faz 1: foundations.md'ye göre yeniden yazım

- Task ID: V1-WTR-010
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Frontend yeniden yazımının (Faz 0, `docs/design/foundations.md`) ilk modülü.
Bugünkü Garson PWA kendi ad-hoc paletini kullanıyor (`#090d16`, `#3b82f6`,
system-ui), tek bir `@media` kuralı taşımıyor, `alert()`/`confirm()` ile
konuşuyor ve her etkileşimde listeyi `innerHTML` ile baştan kuruyor. Bu
görev onu foundations.md'nin diline ve 2026-09-10'da Semih'le
kararlaştırılan akışa göre yeniden yazar; arayüzün bağlanacağı sunucu
tarafı bu oturumda V1-RMD-144…152 ile zaten hazırlandı.

Onaylanmış prototip:
<https://claude.ai/code/artifact/573359b1-111d-41a5-a193-6882f1850208>

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-010-waiter-screen-phase-1-rewrite.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Clients/WaiterPwa/wwwroot/index.html,
    src/Clients/WaiterPwa/wwwroot/waiter-app.css,
    src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-006/008
    sahipliğinde) — ekranın kendisi baştan yazılır.
  - src/Clients/WaiterPwa/wwwroot/manifest.json (V1-WTR-00x sahipliğinde) —
    marka ikonları.
  - src/Clients/WaiterPwa/wwwroot/brand/alkaros-logo-on-dark.png (yeni) —
    üst barın ve kilit perdesinin koyu zemininde kullanılan logo; kaynağı
    docs/design/brand/, oradan kopyalanır.
  - src/Clients/WaiterPwa/wwwroot/icon-192.png,
    src/Clients/WaiterPwa/wwwroot/icon-512.png (V1-WTR-006 sahipliğinde) —
    bugünkü hâlleri boş bir #3ab5f4 karesi, yer tutucu. Marka monogramıyla
    değiştirilir; manifest zaten "any maskable" diyor, bu yüzden güvenli
    alan payı bırakılır.
  - docs/design/foundations.md (Faz 0 dokümanı) — §4'ün karara bağlanması.

## In scope

1. **Tasarım dili.** foundations.md §1-§3'ün token'ları tek bir `:root`
   bloğunda: `--color-ink/brand/accent/text/canvas/surface/border`, AAA
   doğrulanmış semantik renkler, Inter, 4/8/12/16/24/32 boşluk ölçeği,
   `--target-min` 48px ve `--target-primary` 56px. §4 kararı gereği tek
   tema; ikinci bir token seti yazılmaz.
2. **Akış (prototipte onaylandı).** Masaya dokunmak tek adımdır: adisyon
   açılır, üstünde *Hızlı gönder* (son turun tekrarı ve hazır setler) durur
   — en sık iş iki dokunuş. Menü ayrı ekrandır, "Sık" kategorisi
   varsayılandır, alt şeritte Gönder her zaman erişilebilir. Seçenekli ürün
   miktar ve eklenti sayfası açar; miktar 0,5 adımlıdır.
3. **Bu oturumda açılan sunucu yüzeylerine bağlanır.** Kesirli miktar ve
   kalem durumu (V1-RMD-146), eklenti seçimi ve katalog eklenti grupları
   (V1-RMD-147/148), eklenti adedi (V1-RMD-150), misafir siparişi bildirimi
   ve bekleyen liste (V1-RMD-149), PIN'li kilit (V1-RMD-151).
4. **Tablet düzeni.** Geniş ekranda masa ızgarası çok sütun, menü iki
   sütun, adisyon sağda sabit sütun — "adisyonu aç/kapat" dokunuşları
   kalkar. Bugünkü dosyada tek `@media` kuralı yok.
5. **Dil ve bildirim.** Ekrandaki her metin Türkçe ve kanalın adını değil
   masada olanı anlatır ("M-05 masası sipariş verdi"). `alert()`/`confirm()`
   yerine 5 saniyelik toast + geri alma; aksiyon isteyen bildirim kalıcı
   banner (`docs/UI_STYLE_GUIDE.md`, foundations §5.3).

## Out of scope

- Mutfak, Kasa/POS, Masa Yönetimi, Müşteri (QR) ve Yönetim ekranları:
  foundations.md'nin kendi sırası, her biri kendi görevi.
- Paylaşılan bir bileşen kütüphanesi çıkarmak: ikinci modül yazılırken ne
  kadarının gerçekten ortak olduğu görülür; şimdi soyutlamak erken olur.
- Coursing: `tokens.css` renk tanımlıyor ama backend'de karşılığı yok
  (2026-09-10 taraması); ayrı karar.
- Web Push (uygulama kapalıyken bildirim) ve gerçek kiosk cihaz ayarı
  (Android Ekran Sabitleme / iOS Rehberli Erişim).

## Dependencies

- V1-RMD-149
- V1-RMD-151
- V1-RMD-152

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: exit 0, 0 Uyarı, 0 Hata.
- `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: 32/32 başarılı.
- `tests/Host/MigrationComposition` (`ALKAROS.Host.Tests.dll`, sözleşme
  testlerinin bulunduğu yer): 134/134 başarılı. İkisi de
  `ALKAROS_TEST_PG_PORT=55432` ile; değişken verilmezse fixture 5432'yi
  dener ve her test bağlantı hatasıyla düşer — regresyon değil, ortam.
- `node --check waiter-app.js`: temiz.
- `docs/UI_STYLE_GUIDE.md` §4 İngilizce sızıntı taraması: dosyadaki bütün
  dizeler çıkarılıp tarandı; ekrana basılan tek bir İngilizce metin yok
  (yalnız `console.error` teknik, kullanıcıya görünmüyor). Ham
  `error.message` hiç basılmıyor: `api()` sunucunun kendi Türkçe
  `error.message`'ını, yoksa `describeHttpFailure()` sözlüğünü kullanır.
- Ekran gerçekten açılıp denendi (yerel HTTP sunucusu + sözleşmeleri birebir
  taklit eden geçici API stub'ı, ikisi de scratchpad'de, repoya girmedi):
  - Tablet 1706px: adisyon sağda sabit sütun, masa ızgarası çok sütun.
  - Telefon 430px: adisyon alttan açılan sayfa, üstünde *Turu tekrarla*.
  - Her iki genişlikte `scrollWidth == clientWidth` — yatay taşma yok.
  - ½ → miktar 0,5 seçili açılıyor; 1,5 porsiyon + zorunlu grup + ücretli
    eklentiyle tutar ₺467,50 = 285×1,5 + 20×2, sunucunun hesabıyla aynı.
  - Zorunlu grup seçilmeden onay reddediliyor.
  - Tel üstünde giden gövde: `quantity: 1.5`, `modifiers: [{modifierId}]` —
    istemci ne fiyat ne adet iddia ediyor.
  - Misafir siparişi bandı, PIN tuş takımı ve kilit perdesi çalışıyor.
- Bu turda bulunup düzeltilen üç gerçek kusur: (1) menü ekranı
  `translateX(100%)` ile park edince sayfa 2669px'e taşıyordu —
  `body`'nin `overflow:hidden`'ı viewport'a devrolduğu için kırpmıyordu,
  `.screens`'e taşındı; (2) ücretsiz talimat eklentisi adisyonda ve mutfak
  biletinde "2× Az pişmiş" olarak görünüyordu — eklenti adedi artık hiç
  gönderilmiyor, kararı V1-RMD-150'nin kendi kuralıyla sunucu veriyor;
  (3) masa seçili değilken adisyon başlığındaki ekle/taşı düğmeleri
  duruyordu.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: bu görevden yeni
  ihlal yok (kalan tek ihlal `InventoryAdjustmentService.cs:96`, bu
  görevin diff'inde değil, öncesinden geliyor).

## Handoff

- None
