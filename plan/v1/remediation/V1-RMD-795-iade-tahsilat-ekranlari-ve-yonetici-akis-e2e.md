# V1-RMD-795 - İade ve tahsilat ekranları, hata sözlüğü, yönetici gün sonu ve nakit çıkış onayı E2E

- Task ID: V1-RMD-795
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G4 grubunun ekran tarafı (`7-birlesik-dogrulama.md`); sunucu tarafı V1-RMD-793 ve V1-RMD-794'te bitti. PO kararları değişmeden:

1. İade ekranları (PosTerminal `RefundPanel` ve kasiyer `split-payment.js`): sunucunun yeni alanlarına uyum. İade yöntemi etiketleri `Nakit`, `Kart`, `Havale / EFT`, `Cari hesap` (eski `Manual` yok); kart iadesi fiş numarası ister, havale iadesi istemez ("Gönderildi" onayı yeter); iade isteği satırı terminal bilgisini taşır, yönetici iade bütçesini (`cashierTerminalId` ile) isteğin geldiği terminalde açık oturum için verir; kendi onayıyla verilmiş kayıtlar "Kendi onayı" rozetiyle işaretlenir; kapalı ayarda "kendi isteğini başka yetkili onaylamalı, ayarı Ayarlar'dan açabilirsiniz" ve "isteyenin o terminalde açık kasa oturumu yok" Türkçe cümleleri koda göre sabit gelir. Fişteki `Manual` etiketi `Havale / EFT` olur.
2. Hata sözlüğü (F0076): ham sunucu mesajı ekrana basılmaz. Kasiyer ekranı (`describeHttpFailure`) ve hesap bölme çalışma alanı (`billingApi.ts`, `BillSplitWorkspace.tsx`) hata koduna göre sabit Türkçe cümle gösterir; bilinmeyen kod durum koduna göre genel cümleye düşer. Sunucudaki her tahsilat/iade/hesap bölme hata kodunun istemci sözlüğünde karşılığı olduğu bir testle kilitlenir.
3. F0424: nakit tahsilatta alınan para tutardan azsa "Ödemeyi Ekle" düğmesi pasif olur (şimdi yalnız önizleme uyarısı ve tıklamada ret var).
4. F0435: `AbortSignal.timeout` olmayan tarayıcıda zaman aşımsız kalan `fetch` çağrıları (`cash-session.js`, `customer-accounts.js`, `split-payment.js`) kendi zaman aşımı denetleyicisiyle kapanır.
5. İstemci sınırları: V1-RMD-794'ün kilit testleri yeşil kalır; ekranlarda sabit kopya sınır eklenmez.
6. Tarayıcıda uçtan uca (Playwright, gerçek Host + Postgres): (a) yönetici "Gün sonu ve mutabakat" ekranı: iş günü aç, kapanış özeti, açık işlem varken bilinçli onay kutusu, kapatma; (b) nakit çıkış onayı: kasiyer eşik üstü çıkış ister, ikinci müdür (tohumlu `e2e.mudur.ikinci`) kendi oturumuyla onaylar, tutar kasadan düşer; kasiyer kendi talebini onaylayamaz (ayar kapalıyken).

Kapsam dışı: sunucu davranışı (793/794); gün sonu rapor şekli (V1-RMD-791); ödeme-uzlaşma raporu (G10).

## Owned surface

- `evidence/V1-RMD-795/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx (V1-RMD-510) — yöntem, fiş, rozet, bütçe terminali.
  - src/Clients/PosTerminal/src/features/billing/refundApi.ts (V1-RMD-510) — alanlar, `cashierTerminalId`, hata cümleleri.
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts (V1-RMD-366) — sabit Türkçe hata sözlüğü.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx (V1-RMD-366) — ham mesaj yerine sözlük.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — iade yöntemi etiketleri ve yeni cümleler.
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx (V1-RMD-510) — yeni davranış testleri.
  - src/Clients/PosTerminal/src/features/billing/refundApi.test.ts (V1-RMD-510) — alan ve cümle testleri.
  - src/Clients/PosTerminal/src/features/billing/billingApi.test.ts (V1-RMD-366) — sözlük testleri.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx (V1-RMD-366) — ham mesaj basılmaz testi.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — iade ekranı, sözlük, düğme, zaman aşımı, fiş etiketi.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js (V1-RMD-241) — zaman aşımı.
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js (V1-RMD-443) — zaman aşımı.
  - tests/Clients/StaticApps/split-payment-refunds.test.js (V1-RMD-512) — iade yöntemi ve fiş testleri.
  - tests/Clients/StaticApps/split-payment-change.test.js (V1-RMD-515) — pasif düğme testi.
  - tests/Clients/StaticApps/split-payment-receipt.test.js (V1-RMD-517) — fiş etiketi.
  - tests/Clients/StaticApps/split-payment-failures.test.js (V1-RMD-264) — sözlük testleri.
  - tests/Clients/StaticApps/error-dictionary-lock.test.js (V1-RMD-264) — yeni dosya: sunucu hata kodlarının sözlükte karşılığı.
  - tests/Clients/StaticApps/client-timeout-fallback.test.js (V1-RMD-734) — yeni dosya: eski tarayıcıda zaman aşımı.
  - tests/E2E/Cashier/specs/32-manager-end-of-day-and-cash-out-approval.spec.js (V1-CUI-011) — yeni dosya: iki tarayıcı akışı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (istemci kaynağı bozulunca ilgili test kırılır, `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe, hata metinleri koda göre sabit.
3. `docs/UI_STYLE_GUIDE.md` §4 İngilizce sızıntı taraması dokunulan dosyalarda temiz.
4. E2E: gerçek tarayıcıda iki akış (yukarıda 6a, 6b), ekran görüntüsü ve iz yalnız hata durumunda.

## Out of scope

- Sunucu, veritabanı, geçiş.

## Dependencies

- V1-RMD-793
- V1-RMD-794

## Acceptance evidence

1. Vitest (PosTerminal) ve StaticApps vitest tam paket yeşil; oxlint ve tsc temiz; Playwright yeni spec yeşil (`evidence/V1-RMD-795/`).
2. Mutasyon: yöntem etiketi, fişin yalnız kartta istenmesi, bütçe terminali, rozet, sözlük, pasif düğme, zaman aşımı yedeği bozulunca test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-frontend ve qa-ux-tasarim incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
5. Semih'in elle deneyeceği senaryo: EFT ile ödenmiş hesapta iade iste, onayla; "Havale / EFT" görünür, fiş numarası sorulmaz, "Gönderildi" ile biter. Nakit tahsilatta alınan parayı tutardan az yaz; "Ödemeyi Ekle" pasif kalır. Gün sonu ekranında açık hesap varken kapatmaya çalış; bilinçli onay kutusu çıkar.

## Handoff

- None
