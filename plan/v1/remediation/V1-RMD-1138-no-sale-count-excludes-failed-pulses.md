# V1-RMD-1138 - Teslim edilmeyen "Satış yok" darbesi sayaca ve "5 kez açıldı" uyarısına yazılmaz

- Task ID: V1-RMD-1138
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 3): "Satış yok" kaydı darbeden ÖNCE yazılır (kayıt yazılamazsa çekmece açılmaz; bu güvenlik değişmezi korunur), ama darbe teslim edilmese de açma sayılıyordu: yazıcı arızasında 5 başarısız deneme sayacı 5'e çıkardı ve "5 kez satışsız açıldı" uyarısını tetikledi. Başarısız darbe ayrı sayılır.

1. Satışsız açma sayısı (durum ucu, uç yanıtı, kapanış kaydı `drawerNoSaleCount`) yalnız darbesi teslim edilen açmaları sayar: aynı oturumda aynı korelasyon kimliğiyle `cash.drawer.kick-failed` kaydı olan `NoSale` açma kaydı sayılmaz. Başarısız darbe sayısı (`drawerKickFailedCount`, satış ve satışsız) olduğu gibi ayrı sayılmaya devam eder.
2. `OpenWithoutSaleAsync` darbeden sonra yanıtı yeniden hesaplar: teslim edildiyse sayı ve eşik uyarısı (5 ve üstü) bu açmayı içerir; teslim edilmediyse sayı bu denemeyi içermez ve `Warning` boştur. Kayıt-önce-darbe-sonra sırası değişmez; açma kaydındaki `warned` bayrağı darbe öncesi tahmindir, kapanış raporu ve ekran ondan okumaz (bilinen sınır, RESULT'ta yazılır).
3. Tekrar (aynı anahtar) yanıtı eskisi gibi teslim sonucunu döndürür; başarısız bir denemenin anahtarı sayacı ikinci kez değiştirmez.
4. `docs/domain/cash-session-design.md` 8. bölüm bu sayma kuralıyla güncellenir.

## Owned surface

- `evidence/V1-RMD-1138/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashDrawerService.cs
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri

## Out of scope

- Kapanış raporunda sayacın gösterilmesi (V1-RMD-1140); ödeme ekranı uyarısı; eşik değerinin ayara taşınması; denetim şeması ve migration

## Dependencies

- V1-RMD-1137

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1140
