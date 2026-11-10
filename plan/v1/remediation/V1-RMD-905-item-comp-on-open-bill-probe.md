# V1-RMD-905 - Hesap açıkken yapılan kalem ikramı hesap kalemine yansıyor mu: önce davranışı çalıştıran test

- Task ID: V1-RMD-905
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-903 incelemesi (qa-uctan-uca-akis): hesap bir siparişten oluşturulurken kalemleri kopyalıyor (BillItem.FromOrderItem; ikramlı kalem Complimentary ve gerçek değerle taşınır). Hesap AÇIKKEN garson ya da yönetici kalemi ikram ederse sipariş kalemi Complimentary olur ama hesap kalemi (billing.bill_items) Sale kalır ve discount_amount 0 kalır; `Order.ItemComplimentary` olayını hesap tarafında dinleyen kod bulunamadı. Doğruysa misafir ikram edilen kalemi ödemeye devam eder ve hesap toplam ikram sınırı (V1-RMD-902/903) o ikramı görmez. Önce davranışı çalıştıran test yazılır: açık bir hesabı olan siparişte kalem gerçek ikram uç noktasıyla ikram edilir; ardından hesap kaleminin satır tipi, ikram edilen tutarı ve hesabın ödenecek tutarı okunur. Beklenen: kalem Complimentary ve discount_amount kalemin gerçek değeri, ödenecek tutar buna göre düşmüş. Kırmızı çıkarsa düzeltme ayrı bir plan değişikliğiyle eklenir (para davranışı: ürün kararı gerekmeden mevcut tasarım hedefi olan "ikram edilen kalem ödenmez" doğrultusunda); değilse test kanıt olarak kalır ve RESULT.md'ye çürütme gerekçesi yazılır.

## Owned surface

- `evidence/V1-RMD-905/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Orders/Comp/OrderCompOnOpenBillHttpTests.cs — yeni test
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil test: açık hesabı olan siparişte kalem ikramı hesap kalemine ve ödenecek tutara yansıyor
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim kodu değişikliği (şüphe gerçek çıkarsa ayrı plan değişikliği)

## Dependencies

- V1-RMD-903

## Acceptance evidence

1. Test çıktıları ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
