# V1-RMD-443 - Kasiyerin cari hesap arayüzü: hesaba yaz, müşteriler, ekstre, nakit tahsilat ve kredi limiti

- Task ID: V1-RMD-443
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in kararı (2026-09-29, "Yapalım arayüzü de", "nakitle tam özellik"): V1-RMD-442'nin uçlarını kasiyer
ekranına bağlamak. Planlı V14-UI-001 bağımlılıkları (kartla tahsilat, mutabakat, mali belge) dış kararları beklediği
için bu görev yalnız nakitle çalışan kısmı yapar; kartla cari tahsilat ekranda yoktur.

Bu görev:

- Hesap ödeme ekranına (`split-payment`) "Hesaba yaz (cari)" yöntemi: müşteri arama (ad veya telefon), müşteri
  seçimi (borç ve kullanılabilir kredi görünür, limiti olmayan müşteri için uyarı), onaylanınca adisyon kapanır;
  kredi reddinde sunucunun Türkçe gerekçesi gösterilir.
- Yeni "Cari Hesaplar" sayfası (`/cashier/payments/customer-accounts/`): müşteri listesi ve arama, yeni müşteri, ekstre
  (hareketler ve makbuzlar), açık kasa oturumuna nakit tahsilat (makbuz numarası ve kalan borç gösterilir, borçtan
  fazlası istemci ve sunucuda reddedilir), yönetici oturumuyla kredi limiti ve vade.
- Kasa başlığında "Cari hesaplar" bağlantısı (vanilla kasa ve PosTerminal, `payments.take` yetkisiyle).

## Owned surface

- `plan/v1/remediation/V1-RMD-443-customer-account-ui.md`
- `evidence/V1-RMD-443/**`
- `src/Clients/Cashier/wwwroot/payments/customer-accounts/**` (yeni sayfa)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js,
  src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css ve src/Clients/Cashier/wwwroot/index.html —
  yalnız hesaba yaz yöntemi, müşteri seçici ve başlık bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx — yalnız başlık
  bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/30-customer-accounts.spec.js (yeni) ve
  tests/E2E/Cashier/global-setup.js — yalnız E2E Host'una zarf anahtarı verilmesi

## In scope

- Hesaba yaz, Cari Hesaplar sayfası ve başlık bağlantıları.

## Out of scope

- Kartla cari tahsilat, tahsilat mutabakatı, fatura ve müşteri düzenleme/anonimleştirme ekranları.

## Dependencies

- V1-RMD-442

## Acceptance evidence

- Cashier E2E (gerçek Host, gerçek PostgreSQL, Chromium): yeni `30-customer-accounts.spec.js` 2/2 — müşteri
  eklenir, telefon maskeli görünür, yönetici oturumuyla limit ve vade kaydedilir, hesap ödeme ekranında hesaba yazılan
  adisyon kapanır, ekstrede borç ve hareket görünür, 60 TL nakit tahsilat makbuz numarası ve 40 TL kalan borçla
  sonuçlanır; limiti olmayan müşteride sunucunun Türkçe gerekçesi görünür ve adisyon açık kalır. Tam Cashier paketi
  70/70 (`evidence/V1-RMD-443/e2e.log`).
- PosTerminal tip denetimi geçer, `vitest` 300/301 (`evidence/V1-RMD-443/posterminal.log`); tek hata bu görevden
  bağımsız, bu konteynerde zamanlamaya bağlı `stale.test.ts` eşleştirme penceresi testidir (V1-RMD-438 kanıtında
  değişiklikler geri alınınca da aynı şekilde düştüğü gösterildi; CI'da geçiyor).
- Ekran görüntüleri: `evidence/V1-RMD-443/cari-hesaplar.png`, `evidence/V1-RMD-443/hesaba-yaz.png`.
- Semih'in elle deneyebileceği senaryo: kasada "Cari hesaplar"dan müşteri ekleyin, yönetici oturumuyla limit girin;
  bir adisyonu Hesap Ödeme'de "Hesaba yaz (cari)" ile bu müşteriye yazın; Cari Hesaplar'da borcu görüp bir kısmını
  nakit tahsil edin ve makbuz numarasını görün.

## Handoff

- None
