# V1-RMD-229 - TenderMethodCatalog boşluklu sayısal string bypass'ı

- Task ID: V1-RMD-229
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

`TenderMethodCatalog.TryParse` (`src/Modules/Payments/TenderRouting/
TenderMethod.cs`), yalnız kanonik dört ad'ı (`Cash`/`BankCard`/`MealCard`/
`CustomerAccount`) kabul etmesi gerekirken, baştan/sondan BOŞLUKLU sayısal
bir string'i (`" 1"`, `" 2"`, `"3 "`) kabul edip sessizce bir
`TenderMethod`'a çeviriyor. Kök neden: `!raw.All(char.IsDigit)` guard'ı
yalnız "tüm karakterler rakam mı" kontrolü yapıyor; `" 1"` bir boşluk
içerdiği için bu guard'ı geçiyor, ardından `Enum.TryParse` .NET'in kendi
davranışı gereği string'i TRIM EDİP `"1"`'i sayısal ordinal olarak kabul
ediyor (`TenderMethod.BankCard`). Kod yorumu açıkça "bare numeric ordinal
kabul edilmemeli" diyor ama bu boşluklu varyant onu atlatıyor. Bağımsız bir
denetim ajanı tarafından bulundu (2026-09-17, Kasa modülü kapsamlı
denetimi) ve bizzat doğrulandı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/TenderRouting/TenderMethod.cs
  (V13-PAY-002 sahipliğinde kalır) — yalnız `TryParse`'ın sayısal ordinal
  kabul davranışı düzeltilir; sözleşme/diğer davranışlar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Payments/TenderRouting/TenderRoutingTests.cs
  (V13-PAY-002 sahipliğinde kalır) — yalnız yeni regresyon testleri eklenir.
- `evidence/V1-RMD-229/**`

## In scope

- `TryParse`'ın sayısal ordinal kabul etme davranışını, boşluklu varyantlar
  dahil, tam olarak kapatmak (ör. `raw.Trim()` üzerinde `All(char.IsDigit)`
  kontrolü, ya da doğrudan ad eşleştirmesi yaparak `Enum.TryParse`'ın
  sayısal/trim davranışına hiç güvenmemek).
- Regresyon testi: `" 1"`, `"1 "`, `" 1 "`, `"+1"`, `"1.0"` gibi tüm
  boşluklu/varyant sayısal string'lerin reddedildiğini kanıtlamak.

## Out of scope

- `TenderMethod` enum'ının kendisi veya `TenderRouter`/`TenderRequest`'in
  başka bir davranışı — yalnız parse fonksiyonu değişiyor.

## Dependencies

- None

## Acceptance evidence

- Yeni testler: `" 1"`, `"1 "`, `" 1 "`, `"+1"` gibi girdilerin hepsi
  `TryParse`'tan `false` dönmesini kanıtlar.
- Mevcut `ALKAROS.Payments.TenderRouting.Tests` → tam kapsamda regresyonsuz
  geçer.
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
