# V1-RMD-792 - Kasa ve gün sonu kapanış arayüzü: uyarı ve onay, fark açıklaması, sahiplik hataları, gün sonu önizleme

- Task ID: V1-RMD-792
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-790 ve V1-RMD-791'in sunucu kurallarını kasiyer ve yönetici ekranlarına bağlar; kasa incelemesinin 19. satırı ve 789 RESULT'taki "bırakıldı" denen iki küçük kapanış ekranı sorunu (`index.html` içindeki `aria-live` kapsamı; kapanış ekranındaki çift uyarı) ile "Süpervizör/yönetici" ifadesi kapanır. PO kararı 8:

1. Kasa kapanış ekranı (`cash-session.js`): sunucunun `expected-cash` yanıtındaki açık hesap ve belirsiz ödeme sayıları UYARI olarak gösterilir; kasiyer "Bunları bilerek kapatıyorum" kutusunu işaretlemeden "Kasayı Kapat" kapalıdır; onay istekle (`acknowledgeUnsettled`) gider. Fark sıfırdan farklıysa açıklama alanı zorunludur ve istekle (`differenceExplanation`) gider; tolerans üstündeyse yönetici onayı ayrı yazılır (mevcut davranış korunur, tek açıklama metni iki alana gönderilir). Tolerans sunucudan okunur ve ekranda yazılır (sabit sayı istemcide kopyalanmaz).
2. Sunucu hata kodları Türkçe sözlükten gösterilir: sahip olmayan kullanıcı (`CASH_SESSION_OWNER_REQUIRED`), sayım yok/uyuşmuyor, açıklama zorunlu, onaylanmamış açık hesap, mutabakatta kendi onayı reddi. Ham mesaj basılmaz.
3. 789'dan kalan ve HEAD'de doğrulanacaklar (kodda varsa yeniden yazılmaz, testle kanıtlanır): ağ hatasında "kasa kapanmış olabilir" bildirimi ve durumun yeniden okunması; beklenen tutar okunamazsa "Kasayı Kapat" kapalı; "Kasa Bilgisi Okunamadı" ekranının tanımlı stillerle çizilmesi. Eksik olan varsa bu görevde kapatılır.
4. 789'dan kalanlar bu görevde kapanır: `index.html` `aria-live` kapsamı (tüm sayfa yerine uyarı/bildirim bölgeleri) ve kapanış ekranındaki çift uyarı (hata + bildirim aynı metni iki kez gösteriyor); "Süpervizör açıklaması" ifadesi "Yönetici açıklaması" olur (E2E metinleri birlikte güncellenir).
5. Yönetici gün sonu bölümü (`ClosingSection.tsx`): iki elle girilen sayı alanı (iptal, yazıcı hatası) kalkar; sunucunun `close-preflight` yanıtındaki iptal/yazıcı sayıları, açık kasa oturumu, açık hesap ve belirsiz ödeme sayıları gösterilir; ön koşul varsa uyarı + onay kutusu olmadan "Günü Kapat" kapalıdır; "Ciro (bahşiş hariç)" ve ayrı "Bahşiş" satırı; kapanmış gün raporunda masa kırılımı, kasa mutabakat toplamları ve garson/yazdırma özetleri. Hata kodu Türkçe çeviriden geçer.
6. Yönetici ayarları ekranı: "Kasa kapanış fark toleransı" (0-500 ₺) etiketi ve kuralı; "Yönetici kendi talebini onaylayabilir" açıklaması mutabakatı da kapsar.

Kapsam dışı: backend (V1-RMD-790, 791); React kasa kabuğunda oturum bağlantısı ve rozet (ürün kararı bekliyor, kasa dışı grup); canlı güncelleme.

## Owned surface

- `evidence/V1-RMD-792/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-close.test.js (V1-RMD-109) — yeni dosya: kasa kapanış arayüz testleri.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js (V1-RMD-241) — kapanış ekranı, hata sözlüğü, durum yeniden okuma.
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css (V1-RMD-377) — uyarı ve onay kutusu stilleri.
  - src/Clients/Cashier/wwwroot/payments/cash-session/index.html (V13-PUI-002) — `aria-live` kapsamı.
  - tests/Clients/StaticApps/cash-session-movement.test.js (V1-RMD-109) — kapanış metni değişirse uyum.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx (V1-RMD-504) — gün sonu bölümü.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx (V1-RMD-504) — gün sonu testleri.
  - src/Clients/PosTerminal/src/features/management-closing/closingApi.ts (V1-RMD-504) — önizleme ve onaylı kapanış çağrısı.
  - src/Clients/PosTerminal/src/features/management-closing/models.ts (V1-RMD-504) — yeni alanlar.
  - src/Clients/PosTerminal/src/features/management-closing/management-closing.css (V1-RMD-504) — uyarı stilleri.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-504) — yalnız gün sonu metinleri ve hata çevirileri.
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts (V1-RMD-684) — tolerans ayarı etiketi ve kuralı, onay ayarı açıklaması.
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx (V1-RMD-684) — etiket testi.
  - tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js (V1-RMD-756) — açıklamalı, onaylı kapanış.
  - tests/E2E/Cashier/specs/06-cash-session-conflict.spec.js (V13-PUI-002) — uyum gerekirse.
  - tests/E2E/Cashier/specs/24-cash-session-accessibility.spec.js (V1-RMD-361) — `aria-live` değişikliği uyumu.
  - tests/E2E/Cashier/specs/28-cash-session-denomination-count.spec.js (V1-RMD-377) — kapanış adımı uyumu.
  - src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs (V1-RMD-421) — masa dökümü satırı masa numarasını da taşır (GUID okunmaz).
  - tests/Host/Experience/Reporting/EndOfDayServerFiguresTests.cs (V1-RMD-791) — masa numarası testi.
  - tests/Host/Experience/Reporting/EndOfDayTestDatabase.cs (V1-RMD-791) — masa tohumu.
  - tests/Modules/Reporting/V1Operations/PostgresOperationalReportRepositoryTests.cs (V1-RMD-791) — masa kaydı şekli.
  - tests/E2E/Cashier/specs/11-cross-client-payment-aware-transfer.spec.js (V1-RMD-261) — E2E paketinde kırık: hedef masa seçeneği artık yalnız masa numarasını yazıyor, spec "numara + boşluk" arıyordu.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her davranış için önce kırmızı test (vitest/DOM), sonra düzeltme, sonra yeşil; gerçek Host + Postgres + Chromium E2E kapanış akışı.
2. Onay kutusu olmadan kapanışın istek göndermediği, işaretlenince `acknowledgeUnsettled`/`differenceExplanation` ile gittiği, açıklamasız farkın sunucuya gitmeden uyarıldığı testlenir.
3. Ağ hatasında "kapanmış olabilir" ve beklenen tutar okunamazsa düğme kapanması HEAD'de testle doğrulanır (yoksa eklenir).
4. Ekranlarda İngilizce sızıntı taraması (`docs/UI_STYLE_GUIDE.md` §4), `tsc` ve `oxlint` temiz.

## Out of scope

- Backend kuralları; React kasa kabuğu bağlantısı; müşteri ekranı.

## Dependencies

- V1-RMD-790
- V1-RMD-791

## Acceptance evidence

1. Kırmızı sonra yeşil: StaticApps (kasa kapanış dosyası + önceki kasa dosyaları), PosTerminal ayarlar ve gün sonu testleri, `tsc`, `oxlint`.
2. E2E 05, 06, 24, 28 ve gün sonu akışı gerçek tarayıcıda koşar (`evidence/V1-RMD-792/e2e.log`).
3. Mutasyon: her kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-frontend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir, düzeltmelerden sonra kısa doğrulama turu yapılır.
6. Semih'in elle deneyeceği senaryo: kasada açık hesap varken sayım yapıp kapat; uyarıyı gör, onay kutusu olmadan kapanmadığını gör; farklı sayımda açıklamasız kapanamadığını gör; gün sonunda sayıların kendiliğinden geldiğini gör.

## Handoff

- None
