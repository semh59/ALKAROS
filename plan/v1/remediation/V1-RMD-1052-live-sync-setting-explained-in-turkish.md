# V1-RMD-1052 - Mutfak canlı eşitleme ayarı yönetici ekranında anlaşılır Türkçe açıklamayla açılıp kapatılır

- Task ID: V1-RMD-1052
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Mutfak canlı eşitleme (kitchen.live_sync_enabled) KDS gibi donanım isteyen, her lokantada olmayan bir özelliktir; varsayılanı kapalı kalır ve ayar olarak açılıp kapatılabilir. Yönetici ayarlar ekranında ayar zaten Evet/Hayır olarak değiştirilebiliyor ama yalnız "Mutfak canlı eşitleme" etiketi var; ne işe yaradığı ve neyin açılacağı (kalem durumunun siparişe yansıması, garson hazır bildirimi, gönderilmiş ürünü iptal akışı, mutfak panosunda Hazır biletlerin servis edilene kadar kalması) yazmıyor. Ayar düzenleme formunda bu ayara özel Türkçe açıklama gösterilir; varsayılan ve çalışan yığındaki değer değişmez. Semih kararı 2026-11-14.

## Owned surface

- `evidence/V1-RMD-1052/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.tsx
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1051

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
