# V1-RMD-1128 - Yerinde yönetici onayı: kasiyer ekranından indirim/ikram talebini yöneticinin şifresiyle onaylama ucu

- Task ID: V1-RMD-1128
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 17: kasiyerin yetkisini aşan indirim ya da ikram talebi bugün yalnız yönetici ekranından onaylanabiliyor; kasiyer "yönetici onayına gönderildi" diyerek bekliyor. Yönetici kasiyerin yanındayken aynı ekranda onaylayabilsin diye `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/approvals/{grantId}/approve` ucu eklenir. Gövde: `IdempotencyKey` (talebin kendi anahtarı; grant'ın anahtarıyla eşleşmezse reddedilir), `Username`, `Password`. Sunucu sırasıyla: kasiyer oturumunu doğrular; grant'ın bu hesaba ait (`SubjectType=Bill`, `SubjectId=billId`), çağıran kasiyer tarafından istenmiş, bekleyen ve anahtarı eşleşen olduğunu doğrular; yöneticinin kullanıcı adı ve ŞİFRESİNİ mevcut giriş doğrulamasıyla (AuthenticationService.LoginAsync: PBKDF2, bilinmeyen kullanıcıda sahte hash, 5 hatada kilit) sınar (PIN kabul edilmez: PIN yalnız oturum açmış kişinin cihaz kilidini açar, başka kullanıcıyı doğrulamaz); yöneticide `reports.view` ve talebin yetkisi aranır (yönetici ekranıyla aynı kural); mevcut AuthorizationDecisionStore.ApproveAsync ile onaylar (talep eden ile onaylayan aynı kişi olamaz; kayıt ve denetim izi aynı işlemde: onaylayan yönetici, talep eden kasiyer). Ardından istemci aynı indirim/ikram isteğini aynı anahtarla yeniden gönderir ve mevcut tekrar yolu uygular. Hatalı kimlik, kilitli hesap, yetkisiz yönetici, kendi talebini onaylama ve zaten karara bağlanmış talep Türkçe sabit cümlelerle 403/409 döner; 401 dönmez (istemci oturum bitti sanmasın). Şifre log'a, denetim izine ve yanıta yazılmaz. Mevcut uzaktan onay yolu değişmez. İade ve diğer onay akışları kapsam dışıdır.

## Owned surface

- `evidence/V1-RMD-1128/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/Experience/Billing/BillingSplitContracts.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Billing HTTP testleri ve ApiConventions mimari testi

## Out of scope

- İstemci ekranı (V1-RMD-1129); iade, kasa hareketi ve diğer onay akışları; PIN ile doğrulama; şema ve migration; yönetim ekranındaki onay ucu

## Dependencies

- V1-RMD-1127

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1129
