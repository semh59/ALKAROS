# V1-RMD-343 - Kasa kapatıldığında bekletilen (park) sepetler artık temizleniyor

- Task ID: V1-RMD-343
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Kasa 'beklet' (park) sepetleri vardiya/
oturum sınırı olmadan kalıcı `localStorage`'da kalıyor." Doğrulandı: `cashier-app.js`'in `alkaros_cashier_parked`
anahtarı hiçbir zaman temizlenmiyordu — bir kasiyer bir fişi bekletip vardiyasını kapatsa, o fiş SONRAKİ vardiyayı
aynı terminalde açan kasiyer tarafından hâlâ geri çağrılabilir (recall) durumda kalıyordu. Kasa istemcisinde ayrı
bir "çıkış yap" akışı yok; gerçek vardiya sınırı `cash-session.js`'in kendi `/close` uç noktasıdır (kasa kapatma).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/05-cash-session-lifecycle.spec.js
- `plan/v1/remediation/V1-RMD-343-parked-cart-cleared-on-shift-close.md`

## In scope

1. `submitClose`'un başarı yolunda: kasa kapatma yanıtı işlendikten hemen sonra `alkaros_cashier_parked`
   `localStorage` anahtarı temizleniyor (gizli sekme gibi `localStorage`'ın kullanılamadığı durumlar için
   dosyanın kendi yerleşik `try/catch` deseni izlendi).

## Out of scope

1. Bekletilen sepetler için bir zaman aşımı (TTL) eklemek — bulgu özellikle "vardiya/oturum sınırı" diyor,
   aynı vardiya İÇİNDE ne kadar süre bekletilebileceği ayrı bir ürün kararı (kasiyerin normal iş akışında bir
   fişi bekletip saatler sonra geri çağırması meşru bir kullanım olabilir).
2. Kasa istemcisinde ayrı bir "çıkış yap" (logout) akışı inşa etmek — böyle bir akış zaten yok; gerçek vardiya
   sınırı kasa kapatmadır, bu görevin kapsamı onu doğru şekilde kullanmakla sınırlı.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` (specs/05-cash-session-lifecycle.spec.js): 3/3 test geçti (1 yeni test dahil: "kasa
  kapatıldığında bekletilen sepetler temizlenir") — gerçek Chromium + gerçek Host + gerçek Postgres'e karşı.
- Mutasyon kontrolü: temizleme satırı geçici olarak kaldırıldı (`git stash`), yeni test GERÇEK bir çalışma zamanı
  farkıyla kırmızıya döndü (kapatma sonrası `localStorage`'da hâlâ bekletilen sepet verisi bulundu). `git stash
  pop` ile geri getirildi (`diff` ile bayt-bayt doğrulandı), paket yeniden 3/3 yeşile döndü.

## Handoff

- None
