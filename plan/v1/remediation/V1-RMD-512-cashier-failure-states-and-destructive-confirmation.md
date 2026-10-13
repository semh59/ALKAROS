# V1-RMD-512 - Kasiyer ekranlarında hata durumları ve yıkıcı eylem onayı

- Task ID: V1-RMD-512
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

UI departmanının kasiyer alanı denetiminde (2026-10-13) kodda doğrulanan hataların kapatılması: reddedilen ödemenin sessiz kalması, sunucu hatasının "oturum düştü" gibi görünmesi, ilk yüklemede ağ kopunca takılma, iptal/iade onaysız tek tıkla çalışması, ödeme sonrası ekranlarda çıkış yolu olmaması ve iade panelinde kilitli ödemeye rağmen iptalin sunulması.

## Owned surface

- `plan/v1/remediation/V1-RMD-512-cashier-failure-states-and-destructive-confirmation.md`
- `evidence/V1-RMD-512/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-failures.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-refunds.test.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/refundApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/refundApi.test.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kasa sayfası (`split-payment`): ödeme sonucu "Declined" ise Türkçe ret mesajı; hesap yüklenirken 401 dışı sunucu hatası ve ağ kopması için "Tekrar dene" düğmeli hata ekranı (giriş ekranı yalnız 401); iade/iptal 409 dönerse hesabı yeniden yükleme; iade listesi okunamadıysa "İade iste" satırlarını gizleyip uyarı; hesabı iptal etme ve iadeyi onaylama için ikinci adım onayı, gerekçe boşken düğmenin pasif olması; "Ödendi" ve "İptal edildi" ekranlarında bekleyen hesaplara dönüş bağlantısı; ağ hatasında tahsilatın gerçekleşmiş olabileceği uyarısı.
- PosTerminal iade paneli: çözümlenmemiş (kilitli) ödeme varken iptalin sunulmaması; iptal ve onay için ikinci adım onayı, boş gerekçede düğmenin pasif olması; iade listesi okunamazsa iade satırlarının gizlenmesi; eylem sonrası yenileme düşse bile bildirimin korunması; 409'da veriyi yeniden yükleme.

## Out of scope

- Fiş yazdırma, para üstü, kısmi iade, hazır neden listesi, bölme-tahsilat birleştirme, kasa oturumu bağlantıları (ürün kararı gerektirir; ayrı görev).
- Kasa sayfasında yetkiye göre düğme gizleme (oturum ucu yetki listesi döndürmüyor; sunucu 403 ile reddetmeye devam eder).

## Dependencies

- V1-RMD-509
- V1-RMD-510

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-512/` altındadır.

## Handoff

- None
