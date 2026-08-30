# V1-RMD-014 - Catalog management production API

- Task ID: V1-RMD-014
- Status: Done
- Assignee: /root/rmd014_catalog_api_retry
- Work type: integration
- Surface state: Planned

## Goal

Mevcut catalog ve pricing repository sözleşmelerini manager yetkisi, bounded query ve kararlı hata contract'larıyla
production HTTP yüzeyine açmak. V11 menu publication davranışı bu göreve dahil değildir.

## Owned surface

- PO:2026-08-31 kararıyla Catalog experience yüzeyi V1-RMD-059'a devredildi.
- `tests/Host/Experience/Catalog/**`
- `evidence/V1-RMD-014/**`

## Dependencies

- V1-GOV-010
- V1-RMD-009

## Acceptance evidence

- Category, tax profile, product, modifier group/modifier, product-modifier assignment ve effective price read/write
  endpoint'leri versioned DTO ve gerçek HTTP contract testleriyle geçer; listeler deterministic keyset/cursor ve
  bounded page size kullanır.
- Validation, duplicate SKU, negative price, overlapping effective price, unauthorized ve concurrency sonuçları
  kararlı problem/conflict contract'ları döndürür; başarısız mutation authoritative state'i kısmen değiştirmez.
- `dotnet build ALKAROS.slnx -c Release` ve ilgili catalog/API testleri exit code `0` verir. Mevcut migration değiştirilmez;
  yeni şema ihtiyacı kesin path'li ayrı migration task'ına blocker olur.
- Semih, gerçek Host ve PostgreSQL üzerinde category, tax, product, effective price ve modifier oluşturur; duplicate SKU,
  negative price ve overlapping price girişlerinin reddedildiğini, düzeltilmiş verinin güvenle yeniden gönderilebildiğini
  gözlemler.

## Handoff

- V1-RMD-018
