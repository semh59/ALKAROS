# V1-RMD-1148 - Kasa kapanışı: sayıma başlamadan önce tek onay, kapanınca birincil eylem çıkış

- Task ID: V1-RMD-1148
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 10, küçük akış düzeltmeleri): "Kasayı Kapat: Sayıma Başla" düğmesi geri alınamaz bir adım atıyor ve onay istemiyor; kapanış bitince birincil düğme "Yeni Vardiya Aç" kasiyeri yanlış yönlendiriyor.

1. Sayıma başlamak durumu `Counting` yapar ve kasayı yeni işlemlere kilitler (docs/domain/cash-session-design.md, Counting tanımı; sunucuda `Counting` oturumdan satış yazılmaz). Düğmeye ilk dokunuşta ekran tek bir onay kutusu gösterir ("Sayıma başlayınca kasada yeni satış ve nakit hareketi yapılamaz. Devam edilsin mi?" ve "Evet, sayıma başla" / "Vazgeç"); yalnız "Evet" isteği gönderir. Onay metni ve düğmeler görünür, dokunma hedefi 48 px.
2. Kasa kapandıktan sonra birincil eylem "Çıkış yap" olur: oturumu kapatır (`POST /api/v1/auth/logout?terminalId=...`, sayfanın mevcut oturum bilgisindeki terminal) ve giriş ekranına döner; "Yeni Vardiya Aç" ikincil düğme olarak kalır. Çıkış isteği başarısız olursa ekran Türkçe bir cümleyle söyler, kasiyer yine de ikincil yoldan devam eder.
3. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1148/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-close-start-and-exit.test.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cash-session-close.test.js
  - tests/Clients/StaticApps/cash-session-closed-summary.test.js
  - tests/Clients/StaticApps/cash-session-blind-count.test.js
  - tests/Clients/StaticApps/cash-session-close-manager-approval.test.js
  - tests/Clients/StaticApps/cash-session-close-recount.test.js
  - tests/Clients/StaticApps/cash-session-connection-errors.test.js
  - tests/Clients/StaticApps/cash-session-movement.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sayımı geri alma (sunucu davranışı); yönetici kapanış onayı; vardiya yeniden açma kuralı

## Dependencies

- V1-RMD-1147

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
