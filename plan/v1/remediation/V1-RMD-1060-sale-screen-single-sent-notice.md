# V1-RMD-1060 - Satış ekranında aynı olay tek yerde bildirilir ve boş durumda "aktif sipariş yok" iki kez yazılmaz

- Task ID: V1-RMD-1060
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Satış ekranında sipariş mutfağa gönderilince aynı olay iki kez bildirilir: sipariş panelinde kalıcı "Sipariş gönderildi" bandı ve ekranın altında kendiliğinden kapanan "Sipariş mutfağa gönderildi" bildirimi; ayrıca dar ekranda (768 piksel) sipariş yokken hem "Aktif sipariş / Yeni satış" panel başlığı hem de alttaki sabit çubuk "Aktif sipariş yok / Yeni satış" yazıyor. docs/CASHIER_UI_RULES.md 9'a göre aynı olay tek yerde görünür: gönderme başarısında kalıcı bant kalır, bildirim açılmaz; hızlı satışta "Yeni satış açabilirsiniz" ipucu banda taşınır; sipariş yokken alt çubuk çizilmez. Hata bildirimleri ve diğer başarı bildirimleri değişmez.

## Owned surface

- `evidence/V1-RMD-1060/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.single-notice.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; hata ve geri al bildirimleri

## Dependencies

- V1-RMD-1059

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
