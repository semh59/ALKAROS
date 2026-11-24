# V1-RMD-1185 - Mutfağa giden kalemin iptali kasada yerinde yönetici onayıyla tamamlanır

- Task ID: V1-RMD-1185
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 17 (bulgu 2): `bills.void` yetkisi olmayan kasiyer mutfağa giden kalemi iptal edince istek yetki talebi (grant) olarak yönetici onayına gider ve ekran yalnız "yönetici onayına gönderildi, onaylandıktan sonra yeniden isteyin" der; onayın nerede verileceği belli değil. Nakit çıkış, iade ve fiş sıfırlama V1-RMD-1173..1176 ile yerinde onay veriyor; bu akış tutarsız. Aynı ortak `InPlaceManagerApproval` bileşenine bağlanır:

1. Yeni uç `POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/{itemId}/approvals/{grantId}/approve` (gövde `ApproveGrantInPlaceRequestV1`: IdempotencyKey, Username, Password): talebin bu kasiyerin kendi talebi, bu kalemin ve aynı anahtarın olduğunu doğrular, yöneticiyi `InPlaceManagerApproval` ile doğrular (kendini onaylama reddi, deneme sınırı, hatalı deneme denetim kaydı, `bills.void` yetkisi) ve kararı mevcut `AuthorizationDecisionStore.ApproveAsync` ile verir; denetim izi onaylayan yöneticiyi yazar.
2. Kasiyerde `bills.void` yoktur ve verilmez; mevcut `void-sent` ucu ve grant akışı değişmez.
3. PosTerminal masa panelinde iptal "Beklemede" dönünce yönetici adı ve parolası alanları aynı panelde açılır; onay başarılıysa aynı anahtarla iptal yeniden istenip uygulanır (kasiyer bir şey yeniden yazmaz); parola her denemeden sonra boşaltılır; hata metinleri Türkçe sabit cümleler, 14 px yazı, 48 px düğme.
4. Pilot belgesi (`kasiyer-hizli-baslangic.md`) bu adımı anlatır.

## Owned surface

- `evidence/V1-RMD-1185/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs
  - tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentTestDatabase.cs
  - tests/Host/Experience/Orders/VoidSent/ALKAROS.Host.Experience.Orders.VoidSent.Tests.csproj
  - tests/Architecture/ApiConventions/api_convention_allowlist.json
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/api.test.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - docs/operations/pilot/kasiyer-hizli-baslangic.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu HTTP: doğru yönetici onaylar ve iptal uygulanır, onaylayan denetimde yazılı; kasiyer kendini onaylayamaz; yetkisiz yönetici, yanlış şifre, başkasının talebi, çözülmüş talep reddedilir; istemci: Beklemede sonrası alanlar, onay ve otomatik yeniden istek), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. VoidSent HTTP testleri, ApiConventions mimari testi, PosTerminal ilgili vitest dosyaları ve istemci üretim derlemesi

## Out of scope

- Garson PWA iptali (kendi akışı); `bills.void` verilmesi; yeni migration; indirim/ikram akışı; yönetici ekranındaki bekleyen talep listesi

## Dependencies

- V1-RMD-1184

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
