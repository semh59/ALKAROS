# V1-RMD-228 - İkram (Complimentary) satırının fiskal görünürlüğü

- Task ID: V1-RMD-228
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`BillItem.cs`, bir `Complimentary` (ikram) satırının `NetAmount`/
`TaxAmount`/`GrossAmount` alanlarını her zaman `0`'a zorluyordu — hem
`FromOrderItem`'de (satır 155-157, eski hâl) hem de doğrudan constructor'da
(`if (lineType is BillLineType.Complimentary) { NetAmount = 0m; ... }`).
Sonuç: ikram edilen bir ürünün gerçek değeri (ör. ₺180'lik bir Döner) hiçbir
kalıcı alanda görünmüyordu — yalnız `Quantity`/`UnitPrice` üzerinden dolaylı
olarak çıkarılabiliyordu, `DiscountAmount` ise ilgisiz kalıyordu (genelde
`0`). Bu, GİB'in YN ÖKC/e-Adisyon kuralına aykırı: ikram, "hiç olmamış" bir
satır değil, "tam fiyat + %100 indirim" olarak fiskal çıktıya yansımalıdır
(Semih'in kasa/ödeme araştırması sonrası talebi — "İkram mekanizmasını
düzelt: Complimentary satırını 0 TL yerine gerçek fiyat + %100 indirim
olarak fiskal katmana çevir").

Bulgu şu şekilde doğrulandı: `ItemExceptionHandler.ApplyComplimentaryAsync`
zaten `OrderItem`'ın kendi `NetAmount`/`TaxAmount`/`GrossAmount`'ını
DEĞİŞTİRMİYOR ("Complimentary item retains original quantity, snapshot unit
price and tax rate for tax records" — kod içi yorum), yani gerçek değer
Order seviyesinde zaten korunuyor. Hata yalnız `BillItem.FromOrderItem`'in
bunu Bill'e aktarırken atmasıydı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/BillItem.cs
  (V1-RMD-035 sahipliğinde kalır) — yalnız constructor'daki Complimentary
  özel-durumu ve `FromOrderItem`'in discount hesaplaması değişti; başka
  hiçbir alan/metot dokunulmadı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/BillFoundation/BillDomainTests.cs
  (V1-RMD-035 sahipliğinde kalır) — mevcut `BillItemComplimentaryLineHasZeroTaxableBase`
  testi genişletildi, yeni bir `ComplimentaryLineConstructedDirectlyRejectsAPartialDiscount`
  testi eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs
  (V1-RMD-035 sahipliğinde kalır) — `ItemSplitRejectsComplimentaryZeroAmountItem`
  testindeki doğrudan `BillItem` constructor çağrısına artık zorunlu olan
  `discountAmount: 20m` parametresi eklendi (yeni invariant'ı karşılamak
  için); testin kendi iddiası (Gross=0, SplitEngine reddi) değişmedi.
- `evidence/V1-RMD-228/**`

## In scope

1. `BillItem` constructor'ı: `Complimentary` satırı artık NORMAL para
   hesaplama yolundan geçiyor (özel `if/else` dalı kaldırıldı); yeni bir
   fail-closed invariant eklendi — `lineType == Complimentary` iken
   `DiscountAmount`, satırın gerçek ön-indirim ara toplamına (`lineSubtotal`)
   tam eşit olmak ZORUNDA, aksi hâlde `ArgumentException` fırlatılır (sessizce
   yanlış bir değerle devam etmez).
2. `BillItem.FromOrderItem`: `Complimentary` dalında artık `discountAmount`
   olarak `orderItem.NetAmount + orderItem.DiscountAmount` (gerçek,
   modifier'lar dahil tam ön-indirim tutarı) geçiyor; `NetAmount`/
   `TaxAmount`/`GrossAmount` hâlâ açıkça `0` (müşteri ödemesi değişmedi,
   yalnız hangi tutarın indirildiği artık görünür).
3. `LineSubtotal` (`NetAmount + DiscountAmount`) hesaplanmış özelliği artık
   ikram satırları için gerçek brüt değeri (ör. ₺80) döndürüyor, `0`
   değil — bu, `Bill.Subtotal`/`Bill.DiscountTotal` toplamlarına da
   yansıyor (`Bill.PayableAmount` DEĞİŞMEDİ, hâlâ ikram satırından `0`
   içeriyor — müşterinin ödediği tutar aynı).

## Out of scope

- Fiskal doküman/e-Adisyon üretiminin kendisi (V13-FSC-*'nin kapsamı) —
  bu görev yalnız Bill seviyesindeki ARA veriyi doğru tutuyor, henüz hiçbir
  fiskal cihaza gönderim yok.
- `AdjustmentCalculator`/`DiscountReasonCatalog` (V1-BIL-003 ailesi) —
  ayrı bir "sonradan indirim" mekanizması, bu görev onu değiştirmedi.
- Kasa/Garson UI'larının "Ara toplam"/"İndirim" satırlarını yeni
  toplamlara göre yeniden tasarlamak — sayılar artık daha doğru
  (ikram dahil brüt + ikram indirimi ayrı görünüyor) ama ekran metni/
  düzeni bu görevin kapsamında değişmedi.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `ALKAROS.Billing.BillFoundation.Tests` → 42/42 geçti (yeni testler dahil,
  daha önce modifier'lı bir satırda gerçek bir regresyon bulundu ve aynı
  oturumda düzeltildi — bkz. aşağıdaki not).
- `ALKAROS.Billing.SplitDesign.Tests` → 28/28 geçti, regresyon yok.
- `ALKAROS.Billing.Adjustments.Tests` → 16/16 geçti, regresyon yok.
- `ALKAROS.Host.Experience.Billing.Tests` (gerçek Postgres) → 18/18 geçti,
  regresyon yok.
- `ALKAROS.Host.Experience.Orders.Comp.Tests` (gerçek Postgres, uçtan uca
  ikram akışı) → 14/14 geçti, regresyon yok.
- **Yol boyunca bulunan ve aynı oturumda düzeltilen gerçek bir regresyon:**
  `FromOrderItem`'in ilk taslağı `netAmount`/`taxAmount`/`grossAmount`'ı
  artık açıkça geçirmiyordu (constructor'ın kendi `quantity * unitPrice`
  hesaplamasına bırakıyordu) — bu, modifier fiyat farkı olan (ör. "Ekstra
  Peynir" +₺50) SATIŞ satırlarında modifier tutarını sessizce kaybediyordu
  (`BillItemFromOrderItemWithModifiersAndDiscountCalculatesAccurately`
  testi 120 yerine 70 üretti). Düzeltme: `netAmount`/`taxAmount`/
  `grossAmount` yine açıkça geçiriliyor (Sale için `orderItem`'ın kendi
  değerleri, Complimentary için açık `0m`); yalnız `discountAmount`
  hesaplaması değişti. Test tekrar 42/42 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `git status --short` → yalnız `BillItem.cs` ve iki test dosyasında
  değişiklik var (yeni task dosyası hariç).
- Semih'in elle deneyebileceği senaryo: Garson'da bir ürünü ikram et,
  Kasa'da hesabı görüntüle; ikram edilen ürünün "Ara toplam"a gerçek
  fiyatıyla girip ayrı bir indirim satırıyla düştüğünü, toplam ödenecek
  tutarın değişmediğini doğrula.

## Handoff

- V13-FSC-001
