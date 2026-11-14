# V1-RMD-1081 - Mutfak test yardımcısı SeedSessionAsync(expired: true) yaşam süresi kısıtına uyar

- Task ID: V1-RMD-1081
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

KitchenOperationsTestDatabase.SeedSessionAsync(expired: true) created_at = now() ile expires_at = now() - 1 dakika yazdığı için chk_device_sessions_lifetime (expires_at > created_at) kısıtına takılır. Yardımcı süresi dolmuş oturumu kısıta uyan zaman damgalarıyla ekler ve V1-RMD-1080 testindeki UPDATE dolanması yardımcı çağrısına geri çevrilir. Yardımcıyı expired: true ile çağıran diğer testler yanlış-geçme açısından denetlenir. Üretim kodu değişmez.

## Owned surface

- `evidence/V1-RMD-1081/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/KitchenOperations/KitchenOperationsTestDatabase.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yardımcının düzeltilmesi ve 1080 testindeki UPDATE'in kaldırılması
2. expired: true çağıranlarının yanlış-geçme denetimi ve mutasyonla kanıt
3. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Üretim kodu

## Dependencies

- V1-RMD-1080

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
