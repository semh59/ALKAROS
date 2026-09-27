# V1-RMD-360 - Cashier ana ekran modül denetimi: kategori sekmesi stil hatası, kalıcılık, modal odak, onay eksiği

- Task ID: V1-RMD-360
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 1. modülü: Cashier vanilla
ana ekran (`src/Clients/Cashier/wwwroot/index.html` + `cashier-app.js` + `cashier-app.css`). Sekiz teknik
boyut (T1-T8) ve dört ürün boyutu (P1-P4) üzerinden tarandı; altı bağımsız, gerçek bulgu tespit edildi ve
düzeltildi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/22-category-tab-styling.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/23-active-ticket-and-modal-focus.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-360-cashier-main-screen-ui-audit.md`

## In scope

1. **[T5, Kritik] Kategori sekmeleri hiç stillenmemiş.** `641ab882` (2026-08-29, alakasız bir
   Waiter/Cashier sözleşme hizalama commit'i) JS tarafındaki sınıf adını `category-tab-btn`'den
   `tab-chip`'e değiştirmiş (hem render çağrısı hem kendi `.closest('.tab-chip')` tıklama seçicisi
   tutarlı şekilde), ama `cashier-app.css` hiç güncellenmemiş — her kategori sekmesi bir aydır
   stilsiz, varsayılan tarayıcı butonu olarak görünüyordu, "seçili" durumu hiç görünmüyordu. İki
   bağımsız denetim (05 Eylül, 26 Eylül) bunu hiç yakalamadı. CSS seçicileri `.tab-chip`/
   `.tab-chip.active` olarak yeniden adlandırıldı (JS zaten tutarlı olduğu için JS değil CSS
   düzeltildi).
2. **[T4, Yüksek] Aktif sepet hiç kalıcı değildi.** Yalnızca "Beklet" edilen fişler
   `localStorage`'da saklanıyordu; aktif (henüz beklemeye alınmamış) sepet bir sayfa
   yenilemesinde/tarayıcı çökmesinde/kiosk yeniden başlamasında sıfırdan kayboluyordu, hiçbir
   kurtarma yolu yoktu. Yeni `alkaros_cashier_active_ticket` anahtarı, `loadParkedTickets`/
   `saveParkedTickets`'ın aynı try/catch desenini izliyor; `renderTicket()`'a (her mutasyon
   noktasından sonra zaten çağrılıyor) ve not alanı düzenlemesine (tek başına render tetiklemiyor)
   kancalandı. `cash-session.js`'in vardiya kapanışı, V1-RMD-343'ün "beklet" anahtarını temizlediği
   aynı gerekçeyle bu yeni anahtarı da temizliyor.
3. **[T1, Orta] Modallerde (#parkedModal, #confirmModal) focus trap ve Escape yoktu.** İkisi de
   `role="dialog"`/`"alertdialog"` `aria-modal="true"` taşıyordu ama açılışta klavye odağını içine
   almıyor, Escape ile kapanmıyor, kapanışta odağı tetikleyen öğeye geri vermiyordu. Paylaşılan
   `trapModalFocus()` yardımcı fonksiyonu: ilk odaklanabilir kontrole odaklan, Tab/Shift+Tab'i
   modal içinde döngüye al (WAI-ARIA dialog deseni), Escape'te kapat, kapanışta önceki odağı geri
   ver.
4. **[T1, Düşük] Kategori sekmelerinde `role="tab"`/`aria-selected` yoktu.** Sarmalayıcı `<nav>`
   zaten `aria-label` taşıyordu ama düğmelerin kendisi ekran okuyucuya sıradan, ilişkisiz düğmeler
   olarak geliyordu, hangisinin seçili olduğu hiç anlaşılmıyordu. `role="tablist"`/`"tab"` +
   `aria-selected` standart WAI-ARIA sekme deseni eklendi.
5. **[P3/T5, Orta] "Fişi Temizle" onaysızdı, daha az yıkıcı olan "Geri Yükle" (V1-RMD-355) zaten
   onaylıydı.** Bu ekranın en yıkıcı, geri alınamaz eylemiydi (tüm sepet, undo yok) ama hiçbir
   onay yoktu — yoğun bir vardiyada tek bir yanlış dokunuş tüm girilen ürünleri sessizce siliyordu.
   Mevcut `showConfirmModal()` yeniden kullanıldı.
6. **[P2, Orta — saha gerçekliği] Arama kutusu hiç otomatik odaklanmıyordu.** Gerçek bir terminalde
   barkod okuyucu, odaklı olan HANGİ alan varsa ona yazar — ayrı bir cihaz hedefi yoktur. Sayfa
   yüklendiğinde ve her sipariş mutfağa iletildiğinde (bir sonraki müşterinin başlangıcı) arama
   kutusuna odaklanılıyor artık. Aynı düzeltmenin bir parçası olarak arama girişine 120ms debounce
   eklendi — önceden her tuş vuruşunda (bir barkod taramasının HER karakterinde) tüm ürün ızgarası
   yeniden çiziliyordu.

## Out of scope

Ürün katmanı gözlemleri (P1-P4) — kod değişikliği yapılmadı, Semih'in kararına bırakıldı:

- **P1 (rakip karşılaştırması):** Bu ekranda ürün varyant/modifikatör seçimi yok, yalnızca serbest
  metin not alanı var. Toast/Square gibi ürünlerde bu genellikle yapılandırılmış seçenek listesi
  olarak sunulur. Muhtemelen kasıtlı (ekran "hızlı sipariş" için tasarlanmış, PosTerminal'in kendi
  Cashier.tsx'i tam katalog akışını üstleniyor olabilir) — doğrulanmadı, ürün kararı gerektirir.
- **P3 (basitlik):** Bir kalemin miktarını değiştirmek yalnızca +/- düğmeleriyle tek tek yapılabiliyor,
  doğrudan sayı girişi yok (miktar=5 için 4 tıklama gerekiyor). Küçük bir verimlilik boşluğu, acil
  değil.
- **P4 (öğrenme eşiği):** "Servis eden" seçicisi ilk kullanımda kafa karıştırıcı olabilir ("neden
  başkasına atayayım?") ama varsayılan davranış ("Ben") zaten doğru ve dokümante edilmiş
  (V1-RMD-205); düşük öncelik.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (54 test, 22/23 numaralı yeni dosyalar dahil): 54/54 geçti, hiçbir
  regresyon yok.
- Mutation-check: TÜM bugünkü kaynak değişiklikleri (`cashier-app.js`, `cashier-app.css`,
  `cash-session.js`) `git stash` ile birlikte geri alındı, 22 ve 23 numaralı spesifikasyonlardaki
  TÜM 6 test durumu GERÇEKTEN kırmızı oldu (stilsiz buton, sayfa yenilemesinde kaybolan sepet,
  onaysız temizleme, odaklanmayan modal, eksik ARIA, odaklanmayan arama kutusu). `git stash pop`
  ile geri yüklendi, `git status` ile aynı dosyaların tekrar değiştiği doğrulandı, paket tekrar
  54/54 yeşile döndü.
- `dotnet build`/PosTerminal `corepack pnpm build`: sıfır hata.
- `node --check` ile her iki değiştirilen JS dosyası sözdizimi doğrulandı.

## Handoff

- None
