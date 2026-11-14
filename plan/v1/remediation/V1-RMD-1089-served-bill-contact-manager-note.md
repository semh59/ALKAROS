# V1-RMD-1089 - Servis edilmiş ürünlü hesapta kasiyer paneli yetkili yöneticiye yönlendirir

- Task ID: V1-RMD-1089
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-14: servis edilmiş ama ödenmemiş hesabı yalnız yetkili yönetici iptal eder; kasiyer ekranı "yetkili yöneticiye başvurun" demelidir. Bugün masa panelinin notu hesap açılmış masada her zaman "Hesabı iptal edin" der; hesapta servis edilmiş kalem varsa sunucu kasiyerin iptalini 409 BILL_HAS_SERVED_ITEMS ile reddeder, yani not kasiyeri yanlış yola sokar. Düzeltme: panel zaten okuduğu kalemlerde kitchenState Served olan etkin kalem varsa ve kullanıcıda payments.refund.approve yetkisi yoksa (sunucunun servis edilmiş hesabı iptal etme kuralı) notu "Hesapta servis edilmiş ürün var; hesabı yalnız yetkili yönetici iptal edebilir. Yetkili yöneticiye başvurun." yapar. Servis edilmiş kalemde Geri al zaten yoktur: Geri al yalnız kasiyerin az önce kaldırdığı kalemi geri koyar ve sunucu yalnız taslak kalemin kaldırılmasına izin verir; bu RESULT.md'de kanıtlanır, kodda değişiklik gerekmez.

## Owned surface

- `evidence/V1-RMD-1089/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu iptal kuralı ve yetki kodları; ödeme ekranındaki iptal bölümü; mutfak panosu Geri Al düğmesi

## Dependencies

- V1-RMD-1088

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
