# V1-RMD-395 - Tek ortak API hata işleyicisi

- Task ID: V1-RMD-395
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

Host'ta aynı işi yapan 13 ayrı hata çevirme fonksiyonu (`MapError`, `WriteError` ve benzerleri) ve 5 ayrı hata zarfı tipi, ASP.NET Core'un yerleşik `IExceptionHandler` mekanizmasıyla çalışan tek bir ortak işleyici ve tek bir hata zarfıyla değiştirilir. Amaç, API standardındaki hata biçimini tek yerde uygulamak ve tekrar eden kodu kaldırmaktır.

## Owned surface

- `src/Host/Composition/Errors/**`
- `tests/Host/Errors/**`
- `plan/v1/remediation/V1-RMD-395-shared-api-error-handler.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/ src/Host/DualScreen/ src/Host/Program.cs ALKAROS.slnx

## In scope

1. Ortak işleyici, bugünkü her hata kodunu ve HTTP durumunu birebir korur; istemcilerin gördüğü sözleşme değişmez.
2. Alanlara özel eşleşmeler (ör. `products_sku_key` için `DUPLICATE_SKU`) tek bir kayıt tablosunda tutulur.
3. Eski `MapError` fonksiyonları, hata zarfı tipleri ve yalnız bu fonksiyonlara yönlendiren `catch` blokları silinir.

## Out of scope

- Yeni hata kodu eklemek veya mevcut bir kodu yeniden adlandırmak.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx` ve bütün Host testleri exit code 0 verir; her eski hata kodu için en az bir test aynı durum ve kodu doğrular.
- Silinen ve eklenen satır sayısı `evidence/V1-RMD-395/` altına kaydedilir; net satır sayısı düşmelidir.
- Semih için senaryo: aynı SKU ile iki ürün eklenince ekranda eskisiyle aynı Türkçe uyarı görünür.
