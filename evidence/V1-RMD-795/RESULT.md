# V1-RMD-795 sonuç

Kapatılan bulgular: F0424 (kısa nakitte düğme pasif), F0435 (Eft iade fişsiz), F0076 (ham mesaj basılmaz, sabit sözlük), iade yöntemi etiketleri, kendi onayı rozeti, bütçe terminali, eski tarayıcıda zaman aşımı.

## Doğrulama
- StaticApps: 248/248 (static-final.log); yeni dosyalar error-dictionary-lock (3 test) ve client-timeout-fallback (3 test).
- PosTerminal vitest: 859 testten 858 geçti, kalan 1 (workspace.test.tsx) eski ham mesaj beklentisiydi; güncellendi, 31/31 (vitest-final.log, vitest-workspace.log). tsc ve lint 0 (tsc-final.log, lint-final.log).
- Playwright E2E spec 32: 2/2, taze veritabanıyla iki ayrı koşuda (e2e-5-1.log, e2e-5-2.log), gerçek Host + gerçek Postgres (e2e-3.log): eşik üstü nakit çıkışı kasiyer onaylayamaz, ikinci yönetici aynı terminalde onaylar ve kasadan düşer; açık hesap varken yönetici iş gününü açar, kapanış özetini görür, onay kutusu işaretlenmeden kapatamaz, işaretleyince kapatır.
- Mutasyon: 13 mutant (istemci 12, E2E 1), 13 öldürüldü, her dosya cmp ile geri alındı (mutation-summary.txt, mut/*/restore.txt).

## İnceleme
- qa-frontend ve qa-test-kalitesi: eski yorum düzeltildi; E2E gün sonu testi koşulsuz hale getirildi; zaman aşımı ve sözlük mutantları eklendi; E2E mutantı (kendi talebini onaylama düğmesi) öldürüldü. customer-accounts.js sunucu metnini basar: o uç noktaların tüm metinleri sunucu tarafında Türkçe kilitli (ErrorMessageLanguageTests).

## Notlar
- Rozet yalnız ekranda; selfApproved süzgeci API düzeyindedir.
- PosTerminal dist yeniden derlenemedi (pnpm PATH'te yok); yönetim ekranı bu görevde değişmedi, E2E mevcut dist ile koştu.
