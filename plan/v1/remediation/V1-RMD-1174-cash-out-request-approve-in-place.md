# V1-RMD-1174 - Yerinde yönetici onayı: kasiyer ekranında nakit çıkış talebini yöneticinin şifresiyle onaylama

- Task ID: V1-RMD-1174
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Nakit çıkış talebi (onay eşiğini aşan çıkış) bugün yalnız `cash.session.override` sahibi ayrı girişle onaylanabiliyor; kasiyer ekranı "yönetici onaylamalı" diyor. `POST .../cash-movement-requests/{requestId}/approve` gövdeye isteğe bağlı `ManagerUsername` + `ManagerPassword` alır (kapanış farkı akışındaki kalıp). Alanlar yoksa davranış aynıdır (çağıranın `cash.session.override` yetkisi). Alanlar varsa çağıran yalnız kasa çekmecesi yetkisi (`RequireCashDrawerAsync`) ister, yönetici `InPlaceManagerApproval` ile doğrulanır ve `cash.session.override` aranır; karar ve denetim kaydı yöneticiyi (`DecidedBy`) yazar, talep eden kasiyer kaydı değişmez. Kodla doğrulanan kurallar aynen korunur: nakit yalnız talep edenin kendi oturumunda hareket eder, talep eden ile onaylayan aynı kişi olamaz ("yönetici kendi talebini onaylayabilir" ayarı kapalıyken), çekmece bakiyesini aşan çıkış reddedilir; limit + onay kararı (eşik üstü onay bekler) değişmez. İstemcide (cash-session.js) onay bekleyen çıkış satırında, onaylama yetkisi olmayan kullanıcıya "Yönetici burada onaylasın" alanları (kullanıcı adı, parola türünde şifre, "Yönetici onayı ver") gösterilir; şifre yalnız alanda yaşar, her denemeden sonra boşaltılır; 14 px / 48 px, Türkçe sabit hata cümleleri; mevcut Onayla/Reddet yolu yetkililer için aynen kalır.

## Owned surface

- `evidence/V1-RMD-1174/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashMovements.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs
  - tests/Clients/StaticApps/cash-session-movement-approve-here.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu HTTP + istemci), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. `tests/Clients/StaticApps` ilgili testleri, CashMovement ve CashClose HTTP testleri ile ApiConventions testi exit code 0

## Out of scope

- Nakit çıkış limiti ve eşik kuralının değişmesi; reddetme ve geri çekme; PosTerminal ekranı; şema ve migration

## Dependencies

- V1-RMD-1173

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1175
