# Kasa (Cashier + PosTerminal) Faz 0 Redesign — Kapsam Kararı

- Decision task: V1-CUI-007
- Tarih: 2026-09-16
- Approver: Semih

## 1. Neden şimdi

`docs/design/foundations.md` (Faz 0, onay 2026-09-07) modül sırasını
"Garson → Mutfak → **Kasa/POS** → Masa Yönetimi → Müşteri (QR) →
Yönetim/Arka ofis" olarak sabitledi. Garson ([[garson redesign]],
V1-WTR-010/011) ve Mutfak (V1-KDS-001..009) tamamlandı; sıradaki modül Kasa.

## 2. Mevcut durum tespiti

İki ayrı, birbirinden bağımsız çalışan yüzey "Kasa" adı altında toplanıyor:

### 2.1 `src/Clients/Cashier` (vanilla JS, WaiterPwa'nın ikizi)

- `wwwroot/index.html`, `cashier-app.css` (622 satır), `cashier-app.js`
  (734 satır).
- V1 kapsamı: hızlı ürün/barkod girişi, adisyon taslağı, park/geri çağırma,
  mutfağa iletim. **Ödeme/nakit tahsilat almaz** — V1-CUI-005 bunu kasıtlı
  olarak kaldırdı (V1 sözleşmesine aykırı sahte tahsilat iddiasıydı).
- CSS hâlâ Faz 0 öncesi token seti kullanıyor: `--bg-primary`, `--text-main`,
  `--bg-surface-elevated`, `--border-color`, `--touch-target` vb. —
  `--color-ink/--color-brand/--color-accent/--color-canvas` yok.
- İkon sprite'ı `PosTerminal/src/design-system/Icon.tsx` ile elle senkron
  tutuluyor (dosya içi yorum: "Mirror of ... Keep the two in sync").

### 2.2 `src/Clients/PosTerminal` (React/Vite, ayrı bir uygulama)

- `src/routes/Cashier.tsx` (672 satır): kasiyer terminal girişi, oturum
  durumu, sistem sağlığı rozeti, help-alerts. **Nakit tahsilat/tender/para
  üstü ekranı bu dosyada da, `features/billing/BillSplitWorkspace.tsx`'te de
  yok** — çünkü V1.3'ün `CashSession`/tender backend'i
  (`V13-CSH-001/002/003`, `V13-PUI-002`) hâlâ `Status: Planned`, hiç
  uygulanmadı.
- `src/design-system/tokens.css`: PosTerminal'in **tüm** route'ları
  (Cashier, Tables/FloorPlan, Catalog, Billing, Kitchen-operations,
  Authorization-decisions, System-health, Reservation-station) tarafından
  paylaşılan tek token kaynağı. Faz 0 öncesi kendi paleti var:
  `--ds-color-ink: #17212b`, `--ds-color-brand: #283a4a`,
  `--ds-color-accent: #b73c20` — Faz 0'ın `#0B2135 / #1B4D7B / #00CFFF`
  paletinden tamamen farklı, bağımsız bir tasarım dili.

**Sonuç:** "Kasa" görsel olarak da, davranışsal olarak da Faz 0'a hiç
uğramamış tek modül. Gerçek nakit/ödeme işlevi bugün var olan bir şey değil,
V1.3'te gelecek bir şey — bu redesign'ın onu icat etmesi gerekmiyor
(bkz. §5 kapsam dışı).

## 3. Rakip/pazar taraması (bağlayıcı değil, sadece girdi)

Kısa bir web taraması (Toast, Square, genel POS UI/UX kaynakları,
Adisyo) şu ortak örüntüleri doğruladı — hiçbiri backend'de karşılığı
olmayan bir özellik icat etmek için değil, **var olan** akışların
(ürün ızgarası, adisyon/fiş paneli, gönder butonu) hangi hiyerarşiyle
sunulduğunu doğrulamak için kullanıldı:

- Yüksek hacimli kasa ekranları ürün ızgarasını solda/ortada büyük dokunma
  hedefleriyle, aktif fişi sağda/sabit bir sütunda tutuyor — ALKAROS
  Cashier'ın mevcut `pos-catalog-panel` / `pos-ticket-panel` ikili
  düzeni bu örüntüyle zaten uyumlu; **düzen değişmiyor, yalnız token'lar
  değişiyor**.
- Nakit tahsilat ekranları (ileride V1.3 ile gelecek) tipik olarak: girilen
  tutar → otomatik para üstü hesabı → sık kullanılan banknot/tutar kısayolu
  düğmeleri şeklinde kuruluyor. Bu, backend `CashSession`/tender contract'ı
  (`V13-CSH-003`) teslim edildiğinde `V13-PUI-002` için referans alınacak;
  **bu kararın kapsamına girmiyor**, sadece ileride not olarak bırakılıyor.

Kaynaklar (bilgi amaçlı, contract kanıtı değil): Toast POS ürün sayfaları,
Square POS ürün sayfaları, "POS UX Benchmarking 2026" (creative.navy),
"POS System Design: Principles" (agentestudio.com).

## 4. Kapsam kararı

**Source basis:** `PO:2026-09-16` (Semih — bu oturumda doğrudan onay,
foundations.md'nin zaten kilitli olan Faz 0 kurallarının Kasa'ya
uygulanma sırası ve payload genişliği kararı).

1. `src/Clients/Cashier` (vanilla JS): `cashier-app.css` Faz 0
   token'larına (`--color-ink/--color-brand/--color-accent/--color-text/
   --color-canvas/--color-surface/--color-border`) geçirilir. Akış/HTML
   yapısı, JS davranışı **değişmez** — bu saf bir görsel dil migrasyonu.
   FOH kaydırma jesti kuralı (foundations.md §5.1) zaten Kasa'yı kapsıyor;
   bu ekranda şu an kaydırmalı bir etkileşim yok, yeni eklenmeyecek (kapsam
   dışı, §5).
2. `src/Clients/PosTerminal/src/design-system/tokens.css` **doğrudan**
   Faz 0 değerlerine güncellenir (yerel/scoped override değil — tek
   palet ilkesi). Bunun kapsamı Cashier route'unun ötesine, PosTerminal'in
   tüm route'larına yayılacağı bilinerek onaylandı: bu, iki paralel
   tasarım dilinin kalıcılaşmasındansa tercih edildi.
3. `src/routes/Cashier.tsx` ve `login-shell`/`cashier-shell`/`pos-header`
   sınıflarının tanımlandığı CSS, token geçişinden sonra görsel olarak
   doğrulanır (kontrast, `--ds-color-accent` dolgu/metin kuralı Faz 0
   §1.1 ile aynı WCAG kısıtına tabi).

## 5. Kapsam dışı

- V1.3 `CashSession`/tender/para üstü ekranı — backend (`V13-CSH-001/002/003`)
  `Planned`; UI'ı (`V13-PUI-002`) önden tasarlamak spekülatif olur
  (TASK_STANDARD.md: "speculative future hook" yasak). Backend teslim
  edildiğinde ayrı görev.
- PosTerminal'in diğer route'larının (Tables, Catalog, Billing,
  Kitchen-operations, Authorization-decisions, System-health,
  Reservation-station) kendi bileşen/akış tasarımı — yalnız paylaşılan
  `tokens.css` üzerinden **renk** mirası alırlar, kendi ekran görevleri
  ayrı modüllerin (Masa Yönetimi, Yönetim/Arka ofis) sırasında ele alınır.
- İkon sprite senkronizasyon mekanizmasının otomasyonu (elle senkron kalmaya
  devam eder, bu kararın konusu değil).

## 6. Reddedilen alternatif

"Sadece Cashier route'una yerel override" seçeneği (tokens.css'e dokunmadan
Cashier.tsx içinde scoped Faz 0 renkleri) değerlendirildi ve reddedildi:
iki paralel token sistemi (`--ds-color-*` eski + yerel Faz 0 override)
aynı uygulamada bir arada yaşardı, bu foundations.md'nin "hiçbir modül
ekranı bu kurallardan sapmaz" ilkesine ve önceki modüllerde (Garson,
Mutfak) izlenen "tek palet" pratiğine aykırı olurdu.

## 7. Etkilenen görev kimlikleri

- V1-CUI-007 (bu karar)
- V1-CUI-008 (Planned) — `src/Clients/Cashier` token migrasyonu
- V1-CUI-009 (Planned) — `src/Clients/PosTerminal/src/design-system/tokens.css`
  Faz 0'a geçiş + Cashier route görsel doğrulama
