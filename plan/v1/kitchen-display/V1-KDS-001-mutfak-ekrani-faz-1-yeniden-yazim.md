# V1-KDS-001 - Mutfak ekranı Faz 1 yeniden yazım

- Task ID: V1-KDS-001
- Status: Planned
- Assignee: Unassigned
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

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-RMD-082 sahipliğinde kalan dosyalar: `KitchenOperationsWorkspace.tsx`,
  `kitchen-operations.css`, `models.ts`, `kitchenApi.ts`,
  `KitchenOperationsWorkspace.test.tsx`) — tam yeniden yazım.
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek — V1-RMD-097
  sahipliğinde kalan dosya) — Kitchen route'unun otomatik yenileme
  (polling) tetiklemesi (bkz. In scope §5 — bağımsız denetim O2'nin
  işaret ettiği, karşılaştırma raporunun kendi CRIT bulgusuydu,
  unutulmamalı).
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

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (izole
  değil), yeni testler dahil.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 yeni ihlal.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- Semih'in elle deneyebileceği senaryo: bir masaya sipariş al, mutfağa
  gönder, Mutfak ekranında Expo görünümünde masayı bul, kalemi dört
  aşamada ilerlet, "Mutfak Personeli" izinli bir oturumda iptal/sorun
  bildir butonlarının kilitli göründüğünü doğrula.

## Handoff

- None
