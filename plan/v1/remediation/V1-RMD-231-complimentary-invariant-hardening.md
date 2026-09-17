# V1-RMD-231 - İkram satırı invariant'ının totolojisi ve eksik karma toplam testi

- Task ID: V1-RMD-231
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

`V1-RMD-228`'in eklediği "Complimentary satır tam indirimli olmak zorunda"
kontrolü (`BillItem.cs` constructor'ı, `DiscountAmount != lineSubtotal`
fırlatma), gerçek bir üretim yolunda (`FromOrderItem`) TOTOLOJİYE
dönüşüyor: `netAmount` açıkça `0m` geçirildiği için `lineSubtotal =
RoundCurrency(0 + DiscountAmount) = DiscountAmount` — kontrol kendi
girdisiyle kendini karşılaştırıyor, hiçbir zaman gerçek bir bağımsız brüt
değerle (`Quantity * UnitPrice`) doğrulamıyor. Yarın `FromOrderItem`'in
`discountAmount` hesabı (satır: `orderItem.NetAmount +
orderItem.DiscountAmount`) yanlış/eksik olsa bile (ör. bir modifier
toplamı unutulsa), bu "güvenlik ağı" YAKALAYAMAZ — GİB'e eksik raporlanmış
bir ikram sessizce gidebilir. İKİ bağımsız denetim ajanı (Billing modülü +
Uyum/compliance, 2026-09-17 Kasa modülü kapsamlı denetimi) birbirinden
habersiz aynı sonuca vardı; uyum ajanı bunu fiskal doğruluk açısından
YÜKSEK öncelikli işaretledi.

Ayrıca: `Bill.Subtotal`/`Bill.DiscountTotal`'ın artık Sale+Complimentary
KARMA bir hesapta değişen davranışını (ikram satırının gerçek brüt değeri
artık bu toplamlara dahil oluyor, `PayableAmount` değişmiyor) doğrulayan
HİÇBİR test yok — yalnız izole tek-satırlık testler var.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/BillItem.cs
  (V1-RMD-035 ailesinde kalır) — yalnız constructor'daki invariant kontrolü
  ve/veya `FromOrderItem`'in çağrı şekli, gerçek bağımsız bir brüt değerle
  doğrulayacak şekilde güçlendirilir; para hesaplama mantığının geri kalanı
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/BillFoundation/BillDomainTests.cs
  (V1-RMD-035 ailesinde kalır) — yeni bir karma (Sale+Complimentary) Bill
  testi eklenir; mevcut testler değiştirilmez.
- `evidence/V1-RMD-231/**`

## In scope

1. `BillItem` invariant kontrolünü, `netAmount` açıkça geçirildiğinde bile
   gerçek bir bağımsız brüt değerle (modifier'lar dahil, `FromOrderItem`
   çağrısından önce hesaplanmış bir "trueGross" değeriyle, ya da
   Complimentary dalında `netAmount`'ı hiç kabul etmeyip yalnız
   `discountAmount`'tan `Quantity * UnitPrice` ile çapraz doğrulayarak)
   anlamlı kılmak.
2. Yeni regresyon testi: 1 Sale (₺100) + 1 Complimentary (₺50) satırından
   oluşan bir `Bill` için `Subtotal=150`, `DiscountTotal=50`,
   `PayableAmount=100` olduğunu kanıtlamak.

## Out of scope

- Fiskal doküman üretiminin kendisi (`V13-FSC-*`'nin kapsamı).
- `AdjustmentCalculator`/indirim mekanizması — ayrı, bu görev onu
  değiştirmez.

## Dependencies

- V1-RMD-228

## Acceptance evidence

- Yeni bir test, `discountAmount` yanlış hesaplanmış (ör. modifier
  eksik) bir Complimentary `BillItem.FromOrderItem` çağrısının artık
  gerçekten `ArgumentException` fırlattığını kanıtlar (önceki totolojik
  davranışta bu senaryo sessizce geçerdi).
- Karma Bill testi (Sale+Complimentary) → `Subtotal`/`DiscountTotal`/
  `PayableAmount` beklenen değerlerle eşleşir.
- `ALKAROS.Billing.BillFoundation.Tests` (tamamı) → regresyonsuz geçer.
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
