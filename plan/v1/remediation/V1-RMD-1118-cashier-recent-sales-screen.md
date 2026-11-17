# V1-RMD-1118 - Kasiyer ekranında "Son satışlar": kapanmış hesaptan iade sayfasına tek dokunuş

- Task ID: V1-RMD-1118
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Kasiyer deneyimi turu 14: ödenmiş hesaba iade için kasiyer ekranlarından ulaşılamıyor. PosTerminal'de "Son satışlar" çalışma alanı (`/recent-sales`) eklenir; V1-RMD-1117 ucunu okur ve kasiyerin açık oturumunda kapanan en yeni 20 hesabı listeler: masa veya Hızlı satış, kapanış saati, tutar ve ödeme yöntemleri (Nakit, Kart, Havale, Müşteri hesabı; bilinmeyen için Bilinmiyor). Satıra dokununca mevcut ödeme/iade sayfası iade bölümü açık olarak (`billId` ve `open=refund`) açılır. Ekran `payments.refund.request` yetkisi olanlara ana gezintide "Son satışlar" olarak ve kasa satış başlığında bağlantı olarak görünür. Durumlar sabit Türkçe cümledir: yükleniyor, "Bu oturumda kapanmış satış yok", kasa oturumu kapalıyken "Son satışları görmek için önce kasayı açın", hata (ham mesaj gösterilmez) ve eski veri uyarısı. 14 px yazı ve 48 px dokunma hedefi (docs/CASHIER_UI_RULES.md). Sunucu sözleşmesi, iade kuralları ve ödeme sayfası değişmez.

## Owned surface

- `evidence/V1-RMD-1118/**`
- `src/Clients/PosTerminal/src/features/recent-sales/**` (yeni)
- `src/Host/Experience/Billing/RecentSalesStore.cs` (V1-RMD-1117'de eklendi; yalnız sahiplik kaydı, bu görev dokunmaz)
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx
  - src/Clients/PosTerminal/src/touchTargets.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci typecheck, testler ve üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu ucu ve sözleşmesi; iade, onay ve yetki kuralları; Hesap Ödeme sayfasının geri bağlantı etiketi; fiş yeniden yazdırma

## Dependencies

- V1-RMD-1117

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
