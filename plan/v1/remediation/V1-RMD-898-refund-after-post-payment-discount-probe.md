# V1-RMD-898 - Ödeme sonrası indirimde eski iadelerin eski tutardan kalması şüphesi: önce davranışı çalıştıran test

- Task ID: V1-RMD-898
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: validation
- Surface state: Existing

## Goal

Kalem iadesi, kalemin indirimli payını iade anındaki ayarlamalardan hesaplar; daha önce yapılmış iadeler ise kaydedildikleri tutarda kalır. Şüphe: bir kalem kısmen iade edildikten sonra hesaba (tahsil edilenin altına inmeyen) indirim uygulanırsa, sonraki iade yeni paydan hesaplandığı için bir kalemin toplam iadesi ödenen tutarla uyuşmaz (fazla ya da eksik iade). Önce davranışı çalıştıran kırmızı test yazılır: kısmen ödenmiş hesapta bir kalem kısmen iade edilir, sonra indirim uygulanır, kalan adetler iade edilir; toplam iade, o kalemin gerçekte ödenen payıyla karşılaştırılır. Gerçekse düzeltme ayrı plan değişikliğiyle (para/mevzuat kararı çıkarsa Semih'e sorulmadan raporlanır); değilse testler kanıt olarak kalır ve RESULT.md'ye çürütme gerekçesi yazılır.

## Owned surface

- `evidence/V1-RMD-898/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/RefundAfterDiscountHttpTests.cs — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil test: iade, sonra indirim, sonra kalan adetlerin iadesi; ödemeyle iade toplamlarının ilişkisi
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu değişikliği (şüphe gerçek çıkarsa ayrı plan değişikliği)

## Dependencies

- V1-RMD-897

## Acceptance evidence

1. Test çıktıları ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
