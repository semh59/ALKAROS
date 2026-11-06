# V1-RMD-837 sonuç

Kapatılan: `AdjustmentType.CustomFee` hiçbir fabrika, uç nokta veya ekranca üretilmediği halde enum'da, kasa ekranı sözlüklerinde ve `billing.bill_adjustments.adjustment_type` CHECK'inde kabul ediliyordu. Tür enum'dan ve iki kasa sözlüğünden kaldırıldı; yeni migration 217 CHECK'i (`ck_bill_adjustments_adjustment_type`) yalnız beş gerçek türle yeniden kurar. Kayıtlı `CustomFee` satırı varsa migration açık hatayla durur (veri dönüştürülmez); geri alma türü CHECK'e geri koyar.

## Doğrulama
- Kırmızı (`red.log`): satır varken durma ve geri alma testleri düştü (CHECK-enum eşitliği testi, enum ile veritabanı hâlâ uyumlu olduğundan bu aşamada geçti; mutasyonla doğrulandı).
- Yeşil: Host Migrations+Manifest 45/45 (`green-host.log`), Billing.Adjustments 21/21, StaticApps 365 testin 364'ü ilk koşuda, bir zaman aşımı yük altında (`waiter-app.test.js`, ilgisiz); aynı dosya tek başına 8/8 (`green-static-waiter.log`).
- Mutasyon (`mutations.log`): CHECK'e tür geri konunca (2 test düştü), ön kontrol kapatılınca (1), enum'a tür geri konunca (1); üçü de `cmp` ile geri doğrulandı.
- Atlanan test yok.

## İncelemeler (qa-veritabani, qa-test-kalitesi)
İkisi de bulgu bulmadı. Düşük notlar kabul edildi: `session_replication_role` testin süper kullanıcı (test PG `postgres`) gerektirmesi; ön kontrol testinin hata iletisinde `CustomFee` geçmesine dayanması (mutasyon m2 bunu sınıyor).
