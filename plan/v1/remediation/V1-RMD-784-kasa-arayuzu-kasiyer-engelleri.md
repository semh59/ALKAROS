# V1-RMD-784 - Kasa arayüzü: kasiyeri engelleyen üst çubuk, gezinme, masa ve ekran metni kusurları

- Task ID: V1-RMD-784
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

Canlı Docker kasa arayüzü bir kasiyer gibi denendi; kod ve arayüzde yeniden doğrulanan kusurlar:

1. PosTerminal ana kasa ekranı (`Cashier.tsx`) üst çubuğu 929 px genişlikte sığmıyor: "Çıkış" düğmesi x=1250'ye taşıyor, gövde `overflow-x: clip` olduğu için erişilemiyor. Üst çubuk dar pencerede sarılır, hiçbir eylem erişilemez kalmaz.
2. `/cashier/payments/split-payment` ve `/cashier/payments/cash-session` sayfalarında kasaya dönen bağlantı yok (çıkış yolu yalnız tarayıcı geri tuşu). `customer-accounts` sayfasında "← Kasaya dön" zaten var ve çalışıyor; dokunulmaz.
3. `/tables` ekranı "Tüm bölgeler" görünümünde ilk bölgenin kat planını istiyor; kat planı çizilmemiş bölge için sunucu tasarım gereği 404 döndürüyor ve istemci bunu her yenilemede (8 sn'de bir sessiz yenileme dahil) yeniden istiyor. Kat planı olmadığı bilinen bölge bir kez sorulur, sessiz yenilemelerde tekrar sorulmaz. Masa kartındaki hızlı işlem düğmesi (ör. "Müsait yap") kartı seçmeden işlem penceresini açtığı için yan panel önceki masada kalıyor; hızlı işlem aynı masayı seçer. (Kartın kendi seçme düğmesi zaten doğru çalışıyor, canlıda doğrulandı.)
4. Ham teknik metinler: yönetim ekranlarında "Son güncelleme 2026-11-01T08:02:49.436Z" (ISO), mutfak başlığında ham istasyon kimliği (`{stationId} istasyonu`, CSS büyük harfte "KİTCHEN-MAİN"), "Sürüm 4" / "v4" / "tablo v3" etiketleri, "775.0 GB" (nokta) ve "12.3 dk" / "%4.5" gibi ondalık nokta. Türkçe yerel saat, "Mutfak ekranı" başlığı, etiketlerin kaldırılması ve Türkçe ondalık virgül.
5. Mutfak ekranında "Canlı" ile "Canlı senkron kapalı" birlikte çelişiyor: rozetin anlattığı şey garson bildirimidir (başlığı "Kalem hazır olduğunda garsona bildirim gitmiyor"); ekran kendi kendine yenilenmeye devam eder. Rozet metni "Garson bildirimi kapalı" olur.
6. `/billing` hesap olmayan boş durumda "Hesap alınamadı" hata başlığı gösteriyor; "Aktif adisyon yok" bilgi durumu olur.
7. Vanilla kasa (`/cashier`) dokunma hedefleri 48 px altında: kalem özel talimat alanı (25 px), "Servis eden" seçimi (36 px), "Kupürüm yok" düğmesi (33 px). Projenin dokunma standardı 48 px'tir (`--touch-target`).

Doğrulanmayan veya iş kuralı belirsiz bulgular bu görevde yapılmaz, raporlanır: "Masayı aç" (`SetOccupied`) sunucunun verdiği, siparişsiz Dolu yapan açık bir komuttur; "Masaları birleştir" listesindeki Müsait masalar sunucunun birleştirmede kabul ettiği durumdur (Reserved/Cleaning/OutOfService reddedilir); müşteri ekranı bulguları V1-RMD-785'tedir.

## Owned surface

- `evidence/V1-RMD-784/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — üst çubuk sarma.
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-469) — saat ve ondalık biçim yardımcıları.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-469) — yardımcı testleri.
  - src/Clients/PosTerminal/src/shell/layout-contract.test.ts (V1-RMD-025) — üst çubuk sarma sözleşmesi.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-114) — "Sürüm" etiketi kalkar.
  - src/Clients/PosTerminal/src/routes/workspace.tsx (V1-KDS-001) — kat planı bellek, saat biçimi, boş hesap durumu.
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx (V1-IAM-025) — testler.
  - src/Clients/PosTerminal/src/features/tables/ (V1-RMD-039) — hızlı işlem seçimi, sürüm etiketi.
  - src/Clients/PosTerminal/src/features/kitchen-operations/ (V1-KDS-001) — başlık, rozet, saat, ondalık.
  - src/Clients/PosTerminal/src/features/system-health/ (V1-RMD-372) — saat, ondalık.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx (V1-RMD-368) — saat biçimi.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (V1-RMD-368) — saat testi.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx (V1-RMD-366) — boş durum.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx (V1-RMD-366) — boş durum testi.
  - src/Clients/PosTerminal/src/features/billing/models.ts (V1-RMD-106) — durum türü.
  - src/Clients/Cashier/wwwroot/payments/split-payment/index.html (V1-RMD-553) — kasaya dön bağlantısı.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — bağlantı biçimi.
  - src/Clients/Cashier/wwwroot/payments/cash-session/index.html (V1-RMD-553) — kasaya dön bağlantısı.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css (V1-RMD-377) — bağlantı biçimi, düğme yüksekliği.
  - src/Clients/Cashier/wwwroot/cashier-app.css (V1-RMD-114) — dokunma hedefleri.
  - tests/Clients/StaticApps/ (V1-RMD-109) — kasaya dön ve dokunma testleri.
  - tests/E2E/Cashier/specs/ (V1-RMD-265) — gerçek tarayıcı üst çubuk ve gezinme.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. 768-1024 px genişlikte tüm üst çubuk eylemleri görünür ve tıklanabilir; geniş ekranda düzen bozulmaz.
2. Split-payment ve cash-session sayfalarında her durumda görünen "← Kasaya dön" bağlantısı, 48 px hedef.
3. Bilinen-kat-planı-yok bölge belleği; hızlı işlem düğmesi masayı seçer.
4. Türkçe saat, ondalık virgül, "Mutfak ekranı", etiket temizliği.
5. Rozet metni, boş adisyon durumu, 48 px hedefler.
6. Her düzeltme için önce kırmızı test, sonra yeşil.

## Out of scope

- Müşteri ekranı (V1-RMD-785), kart ödemesi, demo KDV/ürün adları, kasa oturumu beklenen nakit alanı.
- "Masayı aç" ve "Masaları birleştir" iş kuralları; vanilla kasa siparişinin müşteri ekranına düşmesi (iş kuralı kararı bekler).
- Mutfak kimliğinin insan okur adı: yapılandırmada ad alanı yok; kimlik başlıktan çıkarılır, ad kaynağı eklenmez.

## Dependencies

- V1-RMD-783

## Acceptance evidence

1. Her düzeltme için kırmızı sonra yeşil: vitest (PosTerminal ve tests/Clients/StaticApps), Playwright E2E (üst çubuk 929 px, kasaya dön).
2. PosTerminal lint 0 hata, tsc temiz.
3. Mutasyon: düzeltmeyi bozan geçerli mutant testleri kırar; kaynak `cmp` ile geri alınır.
4. Önce/sonra ekran görüntüleri `evidence/V1-RMD-784/` altında.
5. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
6. qa-frontend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None
