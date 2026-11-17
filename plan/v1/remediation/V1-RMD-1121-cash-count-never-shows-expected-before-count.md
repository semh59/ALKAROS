# V1-RMD-1121 - Kasa sayımı bitmeden beklenen nakit hiçbir ekranda görünmez

- Task ID: V1-RMD-1121
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 15: Kasa sayımı kördür; kasiyer beklenen nakdi görüp sayımı ona uydurmamalıdır. Kodla doğrulandı: sayım ekranı ve fark teyidi ekranı beklenen tutarı yalnız sayım sunucuya kaydedildikten sonra gösterir (fark teyidi ekranına yalnız sayım gönderimi götürür), yani bu iki ekran zaten doğrudur. Gerçek sızıntı nakit hareketi özet ekranındadır: sayımdan önce "Kasadaki beklenen nakit" satırı tutarı açıkça yazar. Bu satır kaldırılır; çıkış tutarı çekmece nakdini aşarsa kaydı engelleyen koruma ve sunucu kuralı aynen kalır. Testler sayımdan önce hiçbir ekranda (açık oturum, hareket özeti, sayım) beklenen tutarın bulunmadığını, sayım gönderildikten sonra fark teyidi ekranında bulunduğunu ve "Sayımı düzelt" akışının (V1-RMD-1110) bozulmadığını kilitler.

## Owned surface

- `evidence/V1-RMD-1121/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-blind-count.test.js
  - tests/Clients/StaticApps/cash-session-movement.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu sözleşmesi ve expected-cash uç noktası; fark teyidi ekranı; tolerans kuralı

## Dependencies

- V1-RMD-1120

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
