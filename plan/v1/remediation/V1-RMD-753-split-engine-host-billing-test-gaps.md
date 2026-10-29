# V1-RMD-753 - SplitEngine ve Host Billing eksik test kapsamını kapat (F0908 test boşlukları)

- Task ID: V1-RMD-753
- Status: Blocked
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: test
- Surface state: Existing

## Goal

V1-RMD-752 belgesi (F0908) dokümante ettiği üç motor davranışını gerçek testlerle kanıta bağla:

1. **SplitEngine.CreateAmountSplit** ve **CreateCustomSplit**, `AdjustedBillSummary` verildiğinde indirimli payable/tax hedeflerini kullanır; null iken özgün `Bill` tutarlarına döner. Bugün sadece `CreateEqualSplit` test altında (SplitDesignDomainTests.cs:122-133).
2. **Host Billing HTTP**, indirimli hesapta `SaveEqualAsync` ve `SaveCustomAsync` kaydedildiğinde düşen toplamları onaylar; bugün sadece `SaveAmountsAsync` test altında (BillingSplitHttpTests.cs:541-598).
3. **Item split**, ayarlama (discount/fee/tip) net sıfırsa (yani `AdjustedPayableAmount == Bill.PayableAmount`) hâlâ serbest kalmalı; bugün bu dal test edilmiyor (BillingSplitStore.cs:546).

Yeni bir politika veya dağıtım mantığı eklenmez; sadece mevcut davranışlar test altına alınır.

## Owned surface

- `evidence/V1-RMD-753/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs

## In scope

- Domain test: `CreateAmountSplit` ve `CreateCustomSplit` için `AdjustedBillSummary` parametreli pozitif case'ler (discount, fee, tip kombinasyonları) ve `null` dal kontrolü.
- Host test: indirimli hesapta eşit ve serbest bölme kaydı + toplam doğrulaması; sıfır-net ayarlamalı hesapta item split başarı case'i.
- Testler mevcut fixture (`BillingSplitTestDatabase`) ve desenlerle (SeedBillAsync, SeedSessionWithPermissionsAsync) yazılır.
- `dotnet test` her iki proje için 0 exit code; TRX evidence klasörüne.
- Belge (V1-RMD-752) ve not (V1-RMD-103) **değişmez**.

## Out of scope

- Motor, store, endpoint, repository veya AdjustmentCalculator mantığındaki herhangi bir değişiklik.
- V1-RMD-103, V1-RMD-752, V1-RMD-298, V1-RMD-413 plan metinlerinin değişimi.
- Migration, dependency veya composition root dosyası.
- Kalem-bazlı indirim dağıtımı veya yeni dağıtım politikası tasarımı.

## Dependencies

- V1-RMD-752
- V1-RMD-754
- V1-RMD-298
- V1-RMD-413

## Blocker

- Yeni Host testi ItemSplitAllowedOnAdjustedBillWhenNetAdjustmentIsZero gerçek üretim kusuru buldu (F0908-PD1, evidence/V1-RMD-753/production-defect-found.md). Düzeltme V1-RMD-754'te; 753 test değişiklikleri git stash'te bekler, Görev ancak V1-RMD-754 Done olduktan sonra açılır.

## Acceptance evidence

1. `tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj` — 0 exit, TRX evidence, yeni testler isimleri `*Adjusted*` içermeli.
2. `tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj` — 0 exit, TRX evidence, yeni testler isimleri `*Discounted*Equal*`, `*Discounted*Custom*`, `*ZeroNetAdjustment*ItemSplit*` içermeli.
3. Plan audit, consistency, task scope — 0 hata.
4. `evidence/V1-RMD-753/guard-mutation-results.json` — her yeni assertion için en az 1 mutant öldürülmüş (assertion'ın gerçekten davranışı sorduğunu kanıtlar).
5. V1-RMD-103 ve V1-RMD-752 dosyaları byte-preserved (git diff sadece test dosyaları ve evidence).

## Handoff

- V1-RMD-739