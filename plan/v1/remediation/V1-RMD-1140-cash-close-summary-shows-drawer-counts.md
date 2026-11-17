# V1-RMD-1140 - Kasa kapanış özetinde satışsız açma sayısı ve başarısız darbe sayısı görünür

- Task ID: V1-RMD-1140
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 4): satışsız açma sayacı yalnız denetim kaydının JSON'unda (`cash.session.closed`) duruyor; kasiyerin kapanış özeti hiçbirini göstermiyor. Kapanış yanıtı ve "Kasa Kapatıldı" özeti sayıları taşır.

1. `POST .../cash-sessions/{id}/close` yanıtına `drawer` alanı eklenir: `saleOpenCount`, `noSaleCount`, `kickFailedCount` (V1-RMD-1138 sayma kuralıyla, `CashDrawerService.CountAsync`). Sayım oturum kapandıktan sonra okunur; kapalı oturuma açma kaydı düşmediği için kapanış kaydındaki sayılarla aynıdır. Mevcut alanlar (`session`, `difference`) aynen kalır.
2. "Kasa Kapatıldı" özeti (Cashier, `cash-session.js`) çekmece satırlarını gösterir: "Satışsız çekmece açma: N" ve yalnız N > 0 iken "Açılamayan çekmece denemesi: M". İkisi de 0 ise ve çekmece hiç kullanılmadıysa satır çıkmaz. Metinler sabit Türkçedir.
3. Yönetim ekranındaki "Gün sonu ve mutabakat > Kasa oturumları" tablosu kapsam dışıdır: tablo Reporting modülünün `CashSessionSummaryEntry` kaydından beslenir ve denetim tablosunu okumak yeni bir modüller arası okuma olur; bu görevde yapılmaz, RESULT'ta açık risk olarak yazılır.

## Owned surface

- `evidence/V1-RMD-1140/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/cash-session-closed-summary.test.js
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs
  - tests/Clients/StaticApps/cash-session-close.test.js
  - tests/Clients/StaticApps/cash-session-text-size.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri, ApiConventions mimari testi ve istemci testleri (tests/Clients/StaticApps)

## Out of scope

- Yönetim ekranı kasa oturumları tablosu ve Reporting modülü; kapanış denetim kaydının şeması; kör sayım düzeltme bilgisi (V1-RMD-1141)

## Dependencies

- V1-RMD-1138

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- V1-RMD-1141
