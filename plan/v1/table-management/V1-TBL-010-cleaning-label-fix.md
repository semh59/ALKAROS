# V1-TBL-010 - "Temizlik" etiketinin gerçek davranışla çelişmesi

- Task ID: V1-TBL-010
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in bulgusu ("Aslında hesabı gönder dediğinde masa temiz oluyor",
2026-09-12): `Cleaning` masa durumunun her iki istemcide de gösterdiği
"Temizlik" etiketi, sistemin gerçek davranışıyla çelişiyordu.

**Bulgu:** `SendCheckToCashierAsync` (V1-ORD-006) bir hesap kasiyere
gönderildiğinde masayı `Cleaning`'e çeviriyor VE `current_order_id`'yi
temizliyor — bilinçli olarak, yeni bir parti kasadaki ödeme
tamamlanmadan hemen o masaya oturtulabilsin diye (kod yorumunun kendi
sözü: "the table has already been re-seated by the time the guest
reaches the till"). `CreateOrUpdateTableDraftAsync` (garson tarafı) ve
WaiterPwa'nın kendi `openTable()`'ı masanın durumuna HİÇ bakmıyor — bir
"Temizlik" etiketli masaya da tıklayıp anında sipariş girilebiliyor,
hiçbir uyarı/onay yok.

Yani "Temizlik" etiketi "dokunma, hazır değil" diyor ama sistem sıfır
engelle "tamamen kullanılabilir" gibi davranıyor — etiket kendi
davranışını yalanlıyor. Bir garson ekrana bakıp masayı boş bırakabilir
(gereksiz yere), ya da başka biri hiç tereddüt etmeden sipariş girebilir
— iki farklı personelin aynı ekranı farklı yorumlaması riski.

**Kök neden:** Tek bir `Cleaning` durumu iki farklı gerçek senaryoyu
karşılıyor — (1) sık: hesap kasiyere gitti, masa ANINDA yeniden
kullanılabilir (asıl önemli olan, günde onlarca kez); (2) nadir: masa
`Servis dışı`ndan çıkıp gerçekten fiziksel temizleniyor (`SetCleaning`
aksiyonu). Etiket ikinci senaryonun diliyle yazılmış, birinci (çok daha
sık) senaryoda yanıltıcı.

**Çözüm (Semih'in seçimi, AskUserQuestion ile onaylandı):** Davranış
DEĞİŞMEDİ (zaten doğruydu, V1-ORD-006'nın bilinçli tasarımı) — yalnız
etiket metni değişti: "Temizlik" → **"Toplanıyor"**. Bilgilendirici bir
ton ("az önce boşaldı, muhtemelen toplanmamış"), yasaklayıcı değil. İki
farklı senaryoyu ayrı bir `TableState` değerine bölmek (daha "doğru" ama
çok daha büyük bir iş — yeni enum, yeni geçişler, iki istemcide ayrı
render) bilinçli olarak reddedildi; bu oturumun "pratik kal, iş yükü
artırma" temasıyla uyumlu en küçük, doğru düzeltme metin değişikliğiydi.

## Owned surface

- Sınırlı ek:
  - src/Clients/PosTerminal/src/strings.ts (PosTerminal sahipliğinde) —
    `tableStatusLabels.Cleaning`: "Temizlik" → "Toplanıyor".
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (WaiterPwa sahipliğinde)
    — `TABLE_STATUS.cleaning.label`: aynı değişiklik.

## Out of scope

- İki farklı senaryoyu (hesap kasiyere gitti / gerçek fiziksel bakım-
  sonrası temizlik) ayrı bir `TableState` değerine bölmek — Goal'de
  gerekçelendirildiği gibi bilinçli olarak reddedildi.
- `SetCleaning` aksiyon etiketi ("Temizliğe al") değişmedi — o, hâlâ
  gerçek senaryo 2'yi (Servis dışından çıkarken elle işaretleme)
  doğru anlatıyor.
- Masaya tıklarken durum bazlı bir onay/uyarı eklemek (ör. "Bu masa
  toplanmamış olabilir, yine de sipariş almak istiyor musunuz?") —
  bu ekstra bir tıklama/karar noktası eklerdi, Semih'in bu oturum
  boyunca tekrarladığı "iş yükünü artırma" tercihiyle çelişir.

## Dependencies

- V1-ORD-006
- V1-TBL-009

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → 141/141 (tüm proje,
  regresyon — bu metin değişikliğine bağlı hiçbir test yoktu, hepsi
  değişmeden geçti).
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host):
  tam paket 17/18 (tek başarısız olan, V1-WTR-028'in kendi
  Out-of-scope'unda zaten belgelenen, bu görevle ilgisiz makine/CPU
  rekabeti flake'i — `05-load-and-timing.spec.js` izole koşulduğunda
  2/2 temiz).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (bu koşuda V1-TBL-009'un kendi Owned-surface bölümündeki bir
  format hatası — iki yeni migration dosyasının "Sınırlı ek" altında
  virgülle bölünmüş tek satır olarak yazılmış olması, UNOWNED_PRODUCTION_FILE
  — da fark edilip düzeltildi, bu görevle aynı commit'te).

## Handoff

- None
