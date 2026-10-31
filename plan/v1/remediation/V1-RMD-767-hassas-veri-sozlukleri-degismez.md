# V1-RMD-767 - Hassas veri yükü ve zarfı sözlükleri aşağı çevrilerek değiştirilemez

- Task ID: V1-RMD-767
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu V1-SEC-005: `SensitivePayload.Fields/Categories` ve `SensitiveEnvelope.FieldCategories` salt-okunur arayüz arkasında düz `Dictionary` tutar; `(Dictionary<string, SensitiveCategory>)payload.Categories` aşağı çevirmesiyle kategori sonradan değiştirilebilir (örneğin `Payment` alanı `Public` yapılır ve maskeleme atlanır). Önceki görev (V1-SEC-005) değişmezlik iddia etmişti, kodda ve testte karşılığı yoktur.

Sözlükler kopyalandıktan sonra `ReadOnlyDictionary` ile sarılır; aşağı çevirme `Dictionary`'ye ulaşamaz ve yazma `InvalidCastException` verir.

## Owned surface

- `plan/v1/remediation/V1-RMD-767-hassas-veri-sozlukleri-degismez.md`
- `evidence/V1-RMD-767/**`
- Sınırlı ek: src/BuildingBlocks/Security/SensitiveData/SensitivePayload.cs
- Sınırlı ek: src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs
- Sınırlı ek: tests/BuildingBlocks/Security/SensitiveData/Protection/PayloadRedactionTests.cs

## In scope

- Üç sözlüğün `ReadOnlyDictionary` ile sarılması (kopya + sarmalayıcı).
- Testler: `Categories`, `Fields` ve `FieldCategories` aşağı çevrilerek değiştirilemez; kaynak sözlükte sonradan yapılan değişiklik yük ve zarfa yansımaz.

## Out of scope

- `SensitiveEnvelope` üzerindeki `init` erişimcileri ve `with` ifadesiyle yeniden atama.
- Şifreleme, serileştirme ve maskeleme mantığı.

## Dependencies

- V1-SEC-005

## Acceptance evidence

1. Düzeltmeden önce yeni testler kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `ALKAROS.SensitiveData.Tests` exit 0 geçer.
3. Mutasyon: sarmalayıcı kaldırılınca testler kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
