# V1-RMD-1075 - Kasa oturumu sayfasında 14 pikselden küçük yazı kalmaz

- Task ID: V1-RMD-1075
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

docs/CASHIER_UI_RULES.md 2 en küçük yazıyı 14px ister; kasiyer denemesi 9 kasa oturumu sayfasında başlık etiketi, durum rozeti, uyarı, Açılış Tutarı/Saati etiketleri, boş liste metni, pasif düğme açıklaması ve "Listeyi yenile" için 12-13px ölçtü. cash-session.css içindeki 14px altı bütün yazı boyutları 14px'e çıkarılır; yeni bir test bu dosyada 14px altı yazı kalmadığını korur.

## Owned surface

- `evidence/V1-RMD-1075/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cash-session-text-size.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1074

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. tests/Clients/StaticApps tam koşusu exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
