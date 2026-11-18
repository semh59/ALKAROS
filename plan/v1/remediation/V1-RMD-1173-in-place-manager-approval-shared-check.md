# V1-RMD-1173 - Yerinde yönetici onayı: kullanıcı adı + şifre denetimi tek ortak bileşende (indirim/ikram ve kapanış farkı buna geçer)

- Task ID: V1-RMD-1173
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Yönetici kullanıcı adı ve şifresiyle yerinde onay denetimi (deneme sınırı, giriş doğrulaması, başarısız deneme denetim kaydı, yönetici yetkisi) iki yerde kopya: `BillingSplitApplication.cs` (indirim/ikram talebi) ve `DualScreenApplication.CashSession.cs` (kapanış farkı). Üç yeni akış (nakit çıkış talebi, iade onayı, fiş orijinal hakkı sıfırlama) aynı denetimi isteyecek; beş kopya yerine tek `InPlaceManagerApproval` bileşeni yazılır ve iki mevcut akış ona geçer. Davranış değişmez: aynı hatalar (403 MANAGER_CREDENTIAL_INVALID / MANAGER_LOCKED, 403 APPROVER_LACKS_PERMISSION, 429 TOO_MANY_ATTEMPTS), aynı sayaç anahtarları (kasiyer ve yönetici başına ortak), şifre hiçbir kayda yazılmaz. Tek fark: başarısız deneme denetim kaydında yazılan kullanıcı adı artık iki akışta da maskeli (ilk iki karakter + uzunluk; kapanış akışı zaten böyleydi; şifrenin kullanıcı adı kutusuna yazılması durumuna karşı).

## Owned surface

- `evidence/V1-RMD-1173/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/InPlaceManagerApproval.cs
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - tests/Host/Experience/Billing/InPlaceManagerApprovalTests.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (ortak bileşenin birim testi: yanlış şifre, kilitli hesap, sayaç, yetkisiz yönetici, başarıda sayaç sıfırlanır, maskeli denetim), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. Billing ve CashClose HTTP testleri ile ApiConventions mimari testi yeşil (mevcut davranış kilidi)

## Out of scope

- Yeni uç, yeni istemci davranışı, kendini onaylama kurallarının (her akışın kendi deposunda) değişmesi; şema ve migration

## Dependencies

- V1-RMD-1129

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1174
