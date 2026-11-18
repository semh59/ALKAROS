# V1-RMD-1155 - Kapanışta sayfa yenilenince kayıtlı sayım geri gelir, yeniden giriş düzeltme sayılmaz

- Task ID: V1-RMD-1155
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 16 (bulgu 1): sayım kaydedildikten sonra sayfa yenilenirse oturum `Counting` durumunda kalır ama ekran boş sayım formu açar (sayılan tutar ve beklenen yalnız sayfa belleğindeydi); kasiyer tutarı yeniden girince sunucu bunu yeni bir sayım satırı yazar (30 sn tekrar penceresi geçmişse) ve kapanış özeti "Sayım düzeltildi" der. Yenileme gerçek bir düzeltme gibi kaydolur ve düzeltme sayısını şişirir.

Seçim (kodla gerekçeli): kayıtlı sayım varsa sayfa doğrudan "Fark Teyidi" ekranına döner. Bu ekran sayımı ve beklenen tutarı farkla birlikte gösterir; kör sayım ilkesi (beklenen tutar sayım gönderilene kadar gizli) korunur çünkü sayım satırı zaten vardır ve sayım rakamı farksız gösterilmez. Sayım hiç yoksa boş sayım formu açılır (beklenen tutar gizli). Yeniden saymak yalnız "Sayımı düzelt" ile olur ve bilerek düzeltme sayılır.

1. `GET .../expected-cash` yanıtı `lastCountedAmount` alanını taşır: oturumun son sayım tutarı, sayım yoksa `null`.
2. `cash-session.js`: etkin oturum `Counting` ve kullanıcı oturum sahibi adına işlem yapabiliyorsa (`mayActForOwner`) önce `expected-cash` okunur; `lastCountedAmount` doluysa sayılan tutar ve beklenen tutar ile `closing` aşamasına geçilir ve "Kayıtlı sayımınızla devam ediyorsunuz. Yeniden saymak için Sayımı düzelt düğmesini kullanın; her yeni sayım düzeltme olarak kaydedilir." bildirimi gösterilir (başka bir bildirim varsa ezilmez). Sayım yoksa boş sayım formu açılır ve beklenen tutar durumda saklanmaz (yalnız `lastCountedAmount` doluyken saklanır). Oturum sahibi adına işlem yapamayan kullanıcı eskisi gibi engelli sayım görünümünü görür.
3. Okuma başarısız olursa boş sayım formu açılmaz (yeniden giriş düzeltme sayılırdı): ilk yüklemede mevcut "yüklenemedi, yeniden dene" ekranı (`failActiveSessionRead`) çıkar.
4. Sayım API sözleşmesi, tekrar penceresi ve düzeltme sayımı değişmez; son sayım mevcut `ICashSessionRepository.GetCountsAsync` ile Host uç noktasında okunur, Cash modülü değişmez.
## Owned surface

- `evidence/V1-RMD-1155/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-count-restore.test.js
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - tests/Clients/StaticApps/cash-session-blind-count.test.js
  - tests/Clients/StaticApps/cash-session-owner.test.js
  - tests/Clients/StaticApps/cash-session-no-sale-shortcut.test.js
  - tests/Clients/StaticApps/cash-session-close-start-and-exit.test.js
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri, ApiConventions mimari testi ve istemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sayım tekrar penceresi, düzeltme hakkı, `cash.cash_counts` şeması; yönetim ekranı; başka sayfalar

## Dependencies

- V1-RMD-1154

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps ve CashSession HTTP testleri exit code 0

## Handoff

- None
