# V1-RMD-438 - Katalog listesinde ve ayrıntısında kalan İngilizce değerler ve ham kimlikler

- Task ID: V1-RMD-438
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-437'nin kırmızı koşusunda katalog ürün ayrıntısının tam metni görüldü: ürün satırının alt yazısında ürün
tipi ham İngilizce enum olarak ("ESP-01 · MenuItem"), ayrıntıdaki "KDV profili" satırında vergi profilinin adı
yerine ham kimliği ("tax-1") gösteriliyordu. Fiyat satırlarının alt yazısı da ham fiyat tipini ("SalePrice") ve
ürün kimliğinin ilk 8 karakterini gösteriyordu. docs/UI_STYLE_GUIDE.md: kullanıcıya görünen her metin Türkçe; ham
İngilizce enum ekrana basılamaz.

Bu görev: ürün alt yazısı ürün tipinin Türkçe etiketini, fiyat alt yazısı fiyat tipinin Türkçe etiketini ve ürünün
adını, ayrıntıdaki KDV profili satırı profilin adını gösterir. Bilinmeyen bir değer ham olarak değil "Tanımsız"
olarak görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-438-catalog-list-and-detail-english-leaks.md`
- `evidence/V1-RMD-438/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx,
  src/Clients/PosTerminal/src/features/catalog/models.ts ve
  src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (katalog ekranı görevlerinin sahipliğinde)
  — yalnız satır alt yazıları, KDV profili satırı ve testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts (V1-RMD-097 sahipliğinde) — yalnız
  fiyat tipi etiketi

## In scope

- Ürün ve fiyat satırlarının alt yazısı; ayrıntı panelindeki KDV profili satırı.

## Out of scope

- Katalog ekranının diğer bölümleri ve API.

## Dependencies

- V1-RMD-437

## Acceptance evidence

- PosTerminal tip denetimi ve tam `vitest` koşusu geçer (`evidence/V1-RMD-438/tests.log`). Yeni test: ürün
  listesinde "Menü ürünü", ayrıntıda "KDV %10", fiyat listesinde "Satış fiyatı · Espresso" görünür; "MenuItem",
  "SalePrice" ve "tax-1" görünmez. Değişiklik geri alınınca kırmızı (`evidence/V1-RMD-438/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: Katalog → Ürünler; satır alt yazısında ürün tipi Türkçe, ayrıntıda KDV
  profilinin adı görünür. Fiyatlar sekmesinde satırlar "Satış fiyatı · ürün adı" biçimindedir.

## Handoff

- None
