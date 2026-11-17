# V1-RMD-1150 - Kasiyer denemesi 15: kalan maddeler için karar kaydı (Hesap ön fişi, tek dokunuşla satışsız çekmece, iade/iptal varsayılanları)

- Task ID: V1-RMD-1150
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: analysis
- Surface state: Existing

## Goal

Kasiyer denemesi 15'in üç maddesi kodla doğrulanıp karara bağlanır; kod değiştirilmez, gerekçe `evidence/V1-RMD-1150/RESULT.md` dosyasına yazılır.

1. Ödeme öncesi "Hesap" ön fişi: kodda yalnız ödenmiş hesabın fişi var (`BILL_NOT_PAID`, ilk basım orijinal, sonrakiler kopya); ön fiş yeni bir belge türüdür, orijinal sayacını tüketmemelidir ve yasal ibare ile e-Adisyon (VUK 509 IV.12) uygulanabilirliği repoda karara bağlanmamıştır. Sonuç ve Semih'e sorulacak sorular yazılır.
2. Satış ekranında tek dokunuşla satışsız çekmece: sunucu gerekçeyi zorunlu tutar (`CASH_DRAWER_REASON_INVALID`, denetim kaydı) ve çekmece vuruşundan önce kaydı yazar; gerekçesiz tek dokunuş bu güvenceyi bozar. Kasa sayfasındaki mevcut forma kısayol bağlantısı olarak yapılabilir; yapılmadıysa gerekçesi.
3. İade ve iptal formu varsayılanları: "Siparişi de iptal et" kutusu işaretlenmediğinde başlar (işaretlenince geri alınamaz); formda stoğa dönüş seçeneği yoktur (kodda aranıp bulunmadı). Varsayılanlar güvenli tarafta olduğundan değiştirilmedi; gerekçesi yazılır.

## Owned surface

- `evidence/V1-RMD-1150/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kod ve belge doğrulaması, karar kaydı
2. İki bağımsız qa incelemesi

## Out of scope

- Her türlü kod değişikliği; fiş yazdırma uygulaması; yasal görüş

## Dependencies

- V1-RMD-1149

## Acceptance evidence

1. evidence/V1-RMD-1150/RESULT.md karar kaydı
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
