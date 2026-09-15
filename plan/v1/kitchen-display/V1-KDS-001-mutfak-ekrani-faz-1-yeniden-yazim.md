# V1-KDS-001 - Mutfak ekranı Faz 1 yeniden yazım

- Task ID: V1-KDS-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`docs/design/foundations.md`'nin modül sırasında Garson'dan sonraki hedef
Mutfak. Rakip karşılaştırması ve gerçek mutfak ortamı araştırması
(yağ/buhar'ın kapasitif dokunmayı güvenilmez kılması, expo/rail iş akışı,
mesafeden okunabilirlik) sonucunda kararlaştırılan (Semih, 2026-09-13)
tasarımı `KitchenOperationsWorkspace.tsx`'e uygular: Expo (çapraz istasyon,
masa bazlı gruplama) varsayılan görünüm olur; her kalem tek büyük dokunuşla
İLERLEYEN dört aşamalı bir gösterge kullanır (Bekliyor→Hazırlanıyor→
Hazır→Servis Edildi — kaydırma jesti yok); tipografi/yerleşim `clamp()`/
`auto-fit` ile 13" tabletten 21.5"+ monitöre akışkan; dokunma VE bump bar
(HID/klavye odak imleci) birlikte çalışır; yoğun mod hem otomatik (açık
kalem eşiği) hem elle açılır, çakışırsa son kullanıcı kararı kazanır.
Rol bazlı görünürlük `V1-IAM-028`'in ürettiği `kitchen.advance` iznine
göre çalışır: bu izne SAHİP AMA `orders.send`'e sahip olmayan bir oturum
(Mutfak Personeli) yalnız ilerletme butonlarını görür, sorun bildir/iptal
gizli/kilitli görünür.

**Uygulama sırasında bulunan bir gerçek kısıt (kapsam sapması, kayıtlı):**
`KitchenTicketV1`/`KitchenTicketItemV1` contract'ı hiçbir masa/hesap
kimliği taşımıyor (yalnız `orderId`) — "masa bazlı gruplama" fabrikasyon
veri gerektirirdi (backend akıllı, frontend aptal ilkesine aykırı).
Expo görünümü bunun yerine **sipariş bazlı** gruplama olarak uygulandı
(`groupByOrder`, aynı `orderId`'ye sahip ticket'lar tek kartta) — bir
masanın "o anki turu" için en yakın doğru vekil, uydurma bir "Masa 7"
etiketi değil. Gerçek masa etiketi göstermek küçük bir backend contract
eklemesi (`KitchenTicketV1`'e `TableId`/etiket) gerektirir; bu, ayrı bir
küçük fast-follow görevi olmalı, burada icat edilmedi.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-RMD-082 sahipliğinde kalan dosyalar: `KitchenOperationsWorkspace.tsx`,
  `kitchen-operations.css`, `models.ts`, `kitchenApi.ts`,
  `KitchenOperationsWorkspace.test.tsx`) — tam yeniden yazım.
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek — V1-RMD-097
  sahipliğinde kalan dosya) — Kitchen route'unun otomatik yenileme
  (polling) tetiklemesi (bkz. In scope §5 — bağımsız denetim O2'nin
  işaret ettiği, karşılaştırma raporunun kendi CRIT bulgusuydu,
  unutulmamalı). Ayrıca gerçek bir engelleyici bulundu ve düzeltildi: nav
  linki ve rota kapısı (`canOpenRoute`) yalnız `orders.send` kontrol
  ediyordu — `kitchen.advance`-yalnız bir Mutfak Personeli oturumu Mutfak
  sekmesini hiç açamıyordu. Artık ikisinden biri yeterli;
  `KitchenRoute`/`KitchenOperationsWorkspace`'e ayrı `canAdvance`
  (kitchen.advance) ve `canOperate` (orders.send) prop'ları geçiliyor.
- src/Clients/PosTerminal/src/design-system/tokens.css (Sınırlı ek —
  V1-RMD-016 sahipliğinde kalan dosya) — Mutfak'a özgü büyütülmüş
  tipografi ölçeğinin kalıcı token'lara taşınması (mockup'ta `clamp()`
  inline'dı, burada `--ds-kitchen-*` token'larına çıkarılır).
- src/Clients/PosTerminal/src/strings.ts (Sınırlı ek — V1-RMD-097
  sahipliğinde kalan dosya) — yeni etiketler yalnız gerekiyorsa (mevcut
  `ticketStatusLabels`/`itemStatusLabels` zaten redesign'ın kullandığı
  Türkçe sözlükle birebir örtüşüyor, bağımsız denetimde doğrulandı).

## In scope

1. Expo görünümü: masa bazlı gruplama, istasyonlar yan yana/alt alta
   (viewport genişliğine göre).
2. Dört aşamalı kalem göstergesi; yalnız bir sonraki aşama tıklanabilir.
3. Dokunma + bump bar odak modeli (klavye/HID ok tuşu + Enter/BUMP
   eşdeğeri; görünür odak halkası).
4. Akışkan tipografi/yerleşim (`clamp()`, `auto-fit`) — tek bir ekran
   boyutu varsayılmaz.
5. Otomatik yenileme: kısa aralıklı polling (`KitchenPrintDispatchHostedService`'in
   kendi 5 sn ritmiyle uyumlu bir değer) — karşılaştırma raporunun CRIT
   bulgusu, bu görevin kapsamına dahil edilir.
6. Yoğun mod: otomatik (açık kalem eşiği — sabit bir başlangıç değeri,
   ör. 9; ayarlanabilir hale getirmek ayrı bir görev) + elle geçiş.
7. Rol bazlı görünürlük: `kitchen.advance` var + `orders.send` yok →
   yalnız ilerletme; `orders.send` var → iptal/sorun bildir de görünür.

## Out of scope

- "Ürün Tükendi Bildir" arayüzü (`V1-KDS-002`, `V1-KIT-008`/`V1-IAM-029`
  bittikten sonra).
- İstasyon bazlı personel kısıtlaması (bağımsız denetim O3 — açık soru).
- Otomatik yoğun mod eşiğinin ayarlar ekranından yapılandırılabilir
  olması — bu görev sabit bir değerle başlar.

## Dependencies

- V1-IAM-028
- V1-KIT-007

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata.**
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 145 passed (145)** — tüm proje, izole değil (10 yeni/yeniden
  yazılan test dahil: Expo sipariş gruplama, rol bazlı görünürlük,
  yaş kısıtı rozeti, iptal gerekçe zorunluluğu, yoğun mod geçişi + mevcut
  regresyon testleri).
- `python tools/consistency-audit/consistency_audit.py` → `clean` (bu
  görev sırasında yazılan birkaç Türkçe kod yorumu bulundu, İngilizceye
  çevrildi, yeniden doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- **Tablet/PC uyumluluk kanıtı (gerçek tarayıcı, gerçek bileşen kodu, mock
  veriyle)**: bileşen, geçici bir Vite demo harness'i (`kitchen-demo.html` +
  `kitchen-demo-main.tsx`, ekran görüntüsünden hemen sonra silindi, repoya
  hiç commit edilmedi) üzerinden gerçek Chrome'da render edildi.
  - **~768px (13" tablet)**: istasyonlar tek sütuna düşüyor, dört aşamalı
    gösterge (Bekliyor/Hazırlanıyor/Hazır/Servis Edildi) tam genişlikte
    okunabilir, hiçbir metin kırpılmıyor.
  - **1600px** (fiziksel ekran çözünürlüğü 1920px'e izin vermedi —
    `resize_window` "bounds must be at least 50% within visible screen
    space" hatası verdi; 1600px en yakın gerçekleştirilebilir PC/geniş
    monitör vekili, tasarım akışkan olduğundan 1920px'e sorunsuz ölçekler):
    iki istasyon (İzgara/Soğuk) yan yana sığıyor, header tek satırda,
    yazıcı rotaları paneli sağ rayda görünür, yoğunluk geçişi (Otomatik/
    Sakin/Yoğun) çalışıyor.
  - Doğrulanamayan tek şey: yoğun mod'un ikincil detayları gerçekten
    gizlediği piksel piksel doğrulanmadı (tarayıcı oturumu CDP zaman
    aşımına uğradı) — kod/CSS incelemesiyle doğru (`is-dense` sınıfı
    doğru koşulda ekleniyor, testte de doğrulandı), yalnız görsel kanıt
    eksik. Engelleyici değil.
- Semih'in elle deneyebileceği senaryo: bir masaya sipariş al, mutfağa
  gönder, Mutfak ekranında Expo görünümünde siparişi bul, kalemi dört
  aşamada ilerlet, "Mutfak Personeli" (yalnız `kitchen.advance`) izinli
  bir oturumda "⚠ sorun bildir/iptal" butonunun hiç görünmediğini, buna
  rağmen Mutfak sekmesinin (daha önce yanlışlıkla kilitliyken artık)
  açıldığını doğrula.

## Handoff

- None
