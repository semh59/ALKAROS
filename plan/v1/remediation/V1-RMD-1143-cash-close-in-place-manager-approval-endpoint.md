# V1-RMD-1143 - Kasa kapanışında fark toleransı aşılınca yerinde yönetici onayı: uç

- Task ID: V1-RMD-1143
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 5): kasa kapanışında fark toleransı aşılınca ekran "yönetici onayı gerekir" deyip bir metin kutusu gösteriyor, ama sunucu `cash.session.override` yetkisi istiyor ve kasiyerde yok; kasiyer çıkmazda. V1-RMD-1128 yerinde yönetici onayı kalıbı kapanışa uygulanır.

1. `POST .../cash-sessions/{id}/close` gövdesine isteğe bağlı `ManagerUsername` ve `ManagerPassword` eklenir. `IsSupervisorOverride` doğruysa ve ikisi de verilmişse sunucu yöneticiyi mevcut giriş doğrulamasıyla (`AuthenticationService.LoginAsync`: PBKDF2, sahte hash, hesap kilidi; PIN kabul edilmez) ve V1-RMD-1128 `InPlaceApprovalThrottle` tekilini kullanarak sınar (kasiyer ve yönetici anahtarı başına 3 hata, 15 dakika). Yönetici `cash.session.override` yetkisine sahip olmalı; kapanışı yapan kasiyerin kendisi olamaz (kendini onaylama engeli: `SELF_APPROVAL_NOT_ALLOWED`). Doğrulanınca kapanış yönetici onayıyla ilerler.
2. Kimlik bilgisi verilmezse davranış eskisi gibidir: `IsSupervisorOverride` için çağıranın kendi `cash.session.override` yetkisi aranır (yönetici kendi oturumunda kapatır).
3. Onaylayan yönetici `cash.session.closed` denetim kaydına `overrideApprovedBy` olarak yazılır; her başarısız deneme `cash.close.approval_failed` olayıdır (denenen kullanıcı adı, sonuç; şifre yazılmaz). Şifre günlüğe, denetime ve yanıta girmez.
4. Hatalar sabit Türkçe cümleyle: `MANAGER_CREDENTIAL_INVALID` 403, `MANAGER_LOCKED` 403, `TOO_MANY_ATTEMPTS` 429, `SELF_APPROVAL_NOT_ALLOWED` 403, `APPROVER_LACKS_PERMISSION` 403; 401 dönmez (istemci oturum bitti sanmasın). Cümleler V1-RMD-1128 ve V1-RMD-1142 ile aynıdır ve `ApiErrorCatalog` varsayılanına eklenir.
5. `docs/domain/cash-session-design.md` kapanış bölümüne bu kural yazılır.

## Owned surface

- `evidence/V1-RMD-1143/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Host/DualScreen/CashCloseStore.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri ve ApiConventions mimari testi

## Out of scope

- İstemci ekranı (V1-RMD-1144); PIN ile onay; tolerans ayarı ve kapanış fark kuralı; yönetim ekranı

## Dependencies

- V1-RMD-1142

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. ApiConventions mimari testi exit code 0

## Handoff

- V1-RMD-1144
