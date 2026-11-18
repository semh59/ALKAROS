# V1-RMD-1158 - Hesap ön fişi: numara basımdan önce verilir, iptal edilen yazdırma numarayı tüketir (karar kaydı)

- Task ID: V1-RMD-1158
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: analysis
- Surface state: Existing

## Goal

Kasiyer denemesi 16 (bulgu 4): Hesap ön fişinde "Hesap fişi no" sunucu tarafında `order.pre-receipt.printed` kaydı yazılırken verilir, sonra tarayıcı yazdırma penceresi açılır; pencere iptal edilirse numara kullanılmış kalır. Karar için kodda şunlar doğrulanır ve belgelenir:

1. Tarayıcı yazdırma penceresinin iptalini bildirmez: `afterprint` hem yazdırmada hem iptalde tetiklenir, `window.print()` sonucu döndürmez; bu yüzden "yalnız gerçekten yazdırılınca artsın" istemci tarafında uygulanamaz.
2. Karar: sayaç "yazdırılan" değil "verilen" numarayı sayar; iptal edilen yazdırma numarayı tüketir, numara boşluğu denetim kayıtlarında görünür. Ön fiş mali değildir, ödenmiş hesabın orijinal/kopya hakkını tüketmez (korunur, mevcut testlerle).
3. `window.print()` kilitlenmesi: çağrı, sunucu yanıtından sonra eşzamanlı yapılır ve yazdırma penceresi kapanana kadar sayfa iş parçacığını bekletir; kullanıcı pencereyi kapatınca devam eder. Bu otomasyon aracının yerel pencereyi göremeyip beklemesidir, kullanıcı sorunu değildir; kodda zamanlayıcı ya da yeniden deneme yoktur.
4. `docs/domain/bill-pre-receipt.md` kararı ve gerekçeyi anlatır; kod değişmez, gerekçe `evidence/V1-RMD-1158/RESULT.md` dosyasına da yazılır.

## Owned surface

- `evidence/V1-RMD-1158/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/domain/bill-pre-receipt.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kod doğrulaması, karar kaydı ve belge
2. İki bağımsız qa incelemesi

## Out of scope

- Her türlü kod değişikliği; yazıcıya doğrudan basım; mali müşavir teyidi

## Dependencies

- V1-RMD-1157

## Acceptance evidence

1. evidence/V1-RMD-1158/RESULT.md karar kaydı
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
