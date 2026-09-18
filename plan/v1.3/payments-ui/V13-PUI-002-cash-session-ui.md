# V13-PUI-002 - Implement cashier CashSession UI

- Task ID: V13-PUI-002
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.44

## Goal

Aktif terminal/kasiyer için açma, sayma, kapatma ve fark teyit akışını uygulayın.

## Owned surface

- `src/Clients/Cashier/wwwroot/payments/cash-session/**` (2026-09-18: ilk
  taslak src/Clients/Cashier/Payments/CashSession/ altına yazılmıştı —
  deploy/docker/Dockerfile'ın web aşaması yalnız src/Clients/Cashier/wwwroot'u
  /out/app/cashier'a kopyalıyor, o dizinin dışındaki hiçbir dosya asla
  deploy edilmezdi; bu bulgu üzerine gerçek deploy edilebilir konuma
  taşındı).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot
  (cashier-app.js/css/index.html/manifest.json başka bir görevin sahipliğinde
  kalır) — yalnız yeni payments/cash-session/ alt dizini eklenir, mevcut
  hiçbir dosya değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier
  (01-04 numaralı mevcut spec'ler ve global-setup/lib V1-CUI-011'in
  sahipliğinde kalır) — yalnız iki yeni dosya eklenir:
  tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js ve
  tests/E2E/Cashier/specs/06-cash-session-conflict.spec.js.
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004'ün
  sahipliğinde kalır) — yalnız yeni `cash-movements`/`expected-cash`/
  tolerans-aşımı uç noktaları için 3 yeni test eklenir, mevcut hiçbir test
  değişmez.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Açılış bakiyesi, cash giriş/çıkış, sayım, beklenen/gerçek fark, izin ve eski sürüm yönetimi.
- PO:2026-09-16 kararı (en az iş ilkesi): açılış ekranı, V13-CSH-001'in
  eklediği öneri sorgusundan gelen tutarı alan içine ÖN-DOLU getirir;
  kasiyer bunu değiştirebilir ama genelde tek dokunuşla onaylar. Öneri
  `null` ise (ilk oturum/önceki kapanış yok) alan `0,00` ile başlar — bu
  görevin kendisi hiçbir hesaplama yapmaz, yalnız sunucunun verdiği sayıyı
  gösterir (backend akıllı, frontend aptal).

## Out of scope

- Banka/yemek kartı payment ekranları ve mutabakat kontrol paneli.

## Dependencies

- V13-CSH-001
- V13-CSH-002
- V1-CSH-001
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/wwwroot/payments/cash-session/**`: `index.html`,
  `cash-session.css` (mevcut Faz0 tasarım token'larının manuel kopyası,
  `cashier-app.css` ile aynı "elle senkron" deseni), `cash-session.js`
  (bundler'sız IIFE modül, `cashier-app.js` ile aynı konvansiyonlar —
  `credentials:'include'` fetch, Türkçe hata metinleri, `escapeHtml`,
  `Intl.NumberFormat('tr-TR', …)`).
- Durum makinesi: oturum kontrolü → giriş → açık oturum yoksa açılış
  formu (sunucu önerisiyle ön-dolu) → ikinci açık oturum çakışma ekranı →
  açık oturum kontrol paneli (nakit giriş/çıkış modalı dahil) → sayım →
  fark teyidi (beklenen tutar `GET .../expected-cash`'ten okunur, sabit
  bir tolerans eşiği asla client'ta kopyalanmaz — sunucu 409
  `CASH_VARIANCE_THRESHOLD_EXCEEDED` dönerse ancak o zaman süpervizör
  onay alanı gösterilir) → kapatma sonrası özet → "Yeni Vardiya Aç".
- V13-CSH-004'e Sınırlı ek olarak iki yeni uç nokta: `POST
  .../cash-movements` (nakit giriş/çıkış, task'ın kendi In scope'unda var
  ama V13-CSH-004'ün ilk halinde hiç yoktu) ve `GET .../expected-cash`
  (fark teyidi ekranının kapatmadan ÖNCE gerçek beklenen tutarı
  gösterebilmesi için salt-okunur önizleme — `CashSessionSnapshot.
  ExpectedCash` yalnız `/close`'un kendi yazma yolunda tazelenir, bu E2E
  testinde canlı olarak bulunan bir bug'tı).
- `tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js` (açılış →
  nakit giriş → sayım → sıfır farkla kapatma; ayrıca tolerans aşımı →
  süpervizör açıklaması zorunlu tutulur → override ile kapatma),
  `tests/E2E/Cashier/specs/06-cash-session-conflict.spec.js` (eşzamanlı
  ikinci açılış çakışma ekranını gösterir, "Mevcut Oturuma Git" gerçek
  oturuma döner) — gerçek Chrome + gerçek Host + gerçek Postgres.
- `tests/Host/Experience/CashSession/CashSessionHttpTests.cs`'e 3 yeni
  HTTP testi: `expected-cash` önizlemesinin `/close`'un kendi hesapladığı
  değerle eşleştiği, `cash-movements`in beklenen tutarı iki yönde de
  doğru değiştirdiği ve kapalı oturumda reddedildiği, tolerans aşan
  farkın override olmadan 409 döndüğü.
- Migration yok (veri şeması değişmedi).

## Acceptance evidence

- İkinci açık oturum engellenir (aynı terminalde eşzamanlı ikinci açılış
  409 döner, UI gerçek çakışma ekranını gösterir — E2E ile kanıtlandı).
- Kapatma sayım gerektirir (Sayıma Başla → Sayımı Kaydet olmadan Kapatma
  ekranına geçiş yolu yok).
- Fark üzerine yazılamaz: `ActualCash` yalnız gerçek sayım tutarından
  gelir; tolerans aşan fark sunucu tarafından reddedilir, süpervizör
  onayı ve açıklaması olmadan kapanış tamamlanmaz (E2E + HTTP testleriyle
  kanıtlandı).
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `tests/Host/Experience/CashSession` → 13/13 başarılı.
- `tests/E2E/Cashier` (tam suit, regresyon dahil) → 7/7 başarılı.

## Handoff

- V13-PUI-003
