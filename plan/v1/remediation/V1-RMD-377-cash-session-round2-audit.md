# V1-RMD-377 - Kasa Oturumu Tur 2 denetimi: kupür bazlı sayım

- Task ID: V1-RMD-377
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 2: Kasa Oturumu
(`src/Clients/Cashier/wwwroot/payments/cash-session/**`). Tur 1'de P1 boyutunda bu ekranın sayım
adımı için "sayım ekranı tek bir 'sayılan tutar' alanı istiyor, bazı rakip ürünler kupür bazlı
bir sayım arayüzü sunuyor... büyük bir özellik eklemesi, Semih'in kararına bırakıldı" notu
düşülmüştü. Tur 2, aynı bulguyu somut bir uygulamaya çevirdi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/24-cash-session-accessibility.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/28-cash-session-denomination-count.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-377-cash-session-round2-audit.md`

## In scope

1. **[P1, Yüksek — rakip karşılaştırması] Sayım ekranı yalnızca tek bir serbest-yazılan tutar
   alanı sunuyordu.** Her gerçek rakip register (Toast/Square/Clover) çekmeceyi kupür (banknot/
   madeni para) bazında saydırır, toplamı sistem hesaplar — kasiyer zihinden toplama yapmaz.
   Serbest-yazılan tek alanda YAPILAN TEK bir yazım hatası, kapanışta izi sürülemeyen bir farkı
   sessizce yaratıyordu. Sayım ekranına varsayılan olarak kupür bazlı bir döküm eklendi (200, 100,
   50, 20, 10, 5, 1, 0,50 ₺ — bu kod tabanının gerçekçi bir kasa çekmecesinde bulunan kupürler
   için zaten kullandığı "kapsamlı değil, pratik" yargısı, ör. `OCCUPANCY_WARNING_MINUTES`), her
   satırın kendi ara toplamı ve genel toplam canlı hesaplanıyor. Gerçek bir kupür-dışı durum için
   (yabancı para, yırtık/bantlanmış banknot) "Kupürüm yok, tek tutar gireceğim" bağlantısıyla eski
   serbest alana geri dönülebiliyor.

## Out of scope

- P2/P4/T7/T8: bu ekranın geri kalanı (giriş, açılış, nakit hareketi, kapatma/fark teyidi) Tur
  1'de zaten derinlemesine incelenmişti; Tur 2'de yeniden taranan tek gerçek bulgu yukarıdaki.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (67 test, 28 numaralı yeni dosya dahil, 05 ve 24 numaralı
  dosyalarda güncellenen assertion'lar dahil): 67/67 geçti, regresyon yok.
- Mutation-check: `cash-session.js`/`cash-session.css` `git stash` ile geri alındı; etkilenen 7
  test (28 numaralı dosyanın 3'ü, 05 numaralı dosyanın 3'ü, 24 numaralı dosyanın 1'i) GERÇEKTEN
  kırmızı oldu (çoğu, artık var olmayan `#counted-amount`/geçiş düğmesini bekleyip zaman aşımına
  uğradı). `git stash pop` ile geri yüklendi, tam paket tekrar yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı.

## Handoff

- None
