# V1-RMD-858 sonuç

## Yapılan
Bekleyen hesaplar ekranı `canCancel` (bills.cancel) ve `canMerge` (bills.split) alır; yetkisiz kullanıcıya "Siparişi iptal et" düğmesi ve birleştirme kutuları/çubuğu gösterilmez (iade paneliyle aynı kural). `workspace.tsx` iki yetkiyi oturum yetkilerinden aktarır. Sunucuya yeni alan gerekmedi: oturum yetkileri istemcide zaten vardı, bu yüzden kayıttaki "kuyruk ucu yetki bilgisi taşımıyor" kısmı sunucu değişikliği olmadan kapandı.

## Çürütülen iddia: kardeş sipariş kuyruğa bildirilmiyor
Sunucu yalnız iptal edilen siparişi duyurur, ama (a) istemci her `PendingChecksChanged` olayında listenin tamamını yeniden okur ve (b) sunucu kuyruğu iptal edilmiş faturayı saymadan üretir. Kanıt iki parça: sunucu testi `CancellingOneOrderOfAMergedBillLeavesTheOtherInTheQueueWithoutABill` (birleştirilmiş fatura iptalinden sonra kardeş `awaiting-payment` listesinde `billId = null`; mutant Q1: `status <> 'Cancelled'` süzgeci kaldırılınca düşer) ve istemci testi "shows the other check of a cancelled merged bill as unbilled after the hub announces only the cancelled one" (mutant H1: hub işleyicisi boşaltılınca düşer). Düzeltme gerekmedi.

## Ek: faturası olmayan satır
Satıra tıklayınca fatura açmak sunucuda `bills.split` ister (BillingSplitApplication `/from-order`). Bölme yetkisi olmayan kullanıcıda faturasız satır pasif olur ve "Hesap açmak için bölme yetkisi gerekir." notu görünür; faturası olan satır açılmaya devam eder (mutantlar S1, S2 öldü).

## Tanımsız kayıt: müşteri ekranı/kuyruk çift görünüm
Hangi iki görünümün çakıştığı kayıtta ve kodda belirlenemedi; test yazılmadı. Ana oturuma ürün sorusu olarak bildirilir.

## Testler (kırmızı -> yeşil)
- PendingChecksWorkspace.test.tsx: yetkisiz birleştirme, yetkisiz iptal (red.log: 2 test düştü), kardeş yenileme kanıtı.
- workspace.test.tsx: üç yetki kombinasyonunda kuyruk ekranı (yetkilerin aktarımı).

## Mutasyon (cp+cmp ile geri alındı)
P1 birleştirme denetimi kaldırıldı, P2 iptal denetimi kaldırıldı, P3 canMerge yanlış yetkiye bağlandı, P4 canCancel her zaman true, S1 satır pasifliği kaldırıldı, S2 not kaldırıldı, H1 hub işleyicisi boşaltıldı, Q1 sunucu kuyruğunda iptal süzgeci kaldırıldı: hepsi ÖLDÜ (mut-*.log).

## Koşular
pending-checks + routes vitest tümü yeşil (green.log), tsc --noEmit exit 0, oxlint exit 0 (önceden var olan autoFocus uyarıları).
