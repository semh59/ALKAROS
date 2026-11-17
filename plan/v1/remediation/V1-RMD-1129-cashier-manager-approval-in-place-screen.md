# V1-RMD-1129 - Kasiyer ekranında "Yönetici burada onaylasın": indirim ve ikram bekleme durumunda yerinde onay

- Task ID: V1-RMD-1129
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 17: Hesap Ödeme sayfasında indirim ya da ikram yönetici onayı beklerken ("yönetici onayına gönderildi… O zamana kadar hesap değişmez") kasiyerin yapabileceği bir şey yok. V1-RMD-1128 ucunu kullanan "Yönetici burada onaylasın" bölümü eklenir: talep beklerken (indirim ve ikram formlarının her biri için) yönetici kullanıcı adı ve şifre alanları ile "Yönetici onayı ver" düğmesi görünür. Başarıda istemci aynı talebi aynı anahtarla yeniden gönderir; indirim/ikram uygulanır ve bildirim "Yönetici onayıyla uygulandı." der. Hatalar sabit Türkçe cümledir: kimlik hatalı ("Yönetici adı veya şifre hatalı."), kilitli, yetkisiz, kendi talebi ("Kendi talebinizi onaylayamazsınız; yöneticinin kendi hesabıyla girmesi gerekir."), zaten karara bağlanmış (talep yeniden denenir), bağlantı yok. Şifre yalnız alanda yaşar: state'e, sessionStorage'a, adrese, log'a yazılmaz; her denemeden sonra alan boşaltılır. Alanlar parola türünde ve tarayıcı otomatik doldurmaya kapalıdır. Mevcut uzaktan onay ve "Onay durumunu kontrol et" yolu aynen kalır. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1129/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
  - tests/Clients/StaticApps/split-payment-comp.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu ucu ve sözleşmesi; iade onayı; PIN ile onay; yönetim ekranı

## Dependencies

- V1-RMD-1128

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
