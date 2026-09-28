# V1-RMD-437 - Katalog ekranının etkisiz "Takipsiz" stok modunu sunmaması

- Task ID: V1-RMD-437
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-11: katalogda ürün oluştururken "Stok modu" alanı varsayılan olarak
"Takipsiz" geliyordu; ama satış yolu stok modunu hiç okumuyor. V1-RMD-143'te Semih'in kararı: bütün kanallarda,
bütün satılabilir ürünler için gerçek stok düşümü; stok eşlemesi olmayan ürünün siparişi onaylanmaz, sessiz atlama
yok. Sonuç olarak "Takipsiz" seçilen ürün yine stok eşlemesi ister; yönetici bunu seçerek stok takibinin
kapandığını sanır. Ayrıca ürün ayrıntısında stok modu ham İngilizce değer olarak gösteriliyordu ("Untracked";
docs/UI_STYLE_GUIDE.md dil sızıntısı).

Bu görev, V1-RMD-143 kararını esas alır: yeni ürün formunda "Takipsiz" seçeneği sunulmaz (varsayılan "Miktar
takipli"); alanın altında satış onayının stok eşlemesi gerektirdiği yazar; ayrıntı panelinde stok modu Türkçe
etiketle gösterilir, eski "Takipsiz" kayıtlar "Takipsiz (eski kayıt)" olarak görünür. Sunucu sözleşmesi değişmez:
`Untracked` değeri API'de geçerli kalır (mevcut kayıtlar ve istemciler kırılmaz).

## Owned surface

- `plan/v1/remediation/V1-RMD-437-catalog-stops-offering-untracked-stock-mode.md`
- `evidence/V1-RMD-437/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx,
  src/Clients/PosTerminal/src/features/catalog/models.ts ve
  src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (katalog ekranı görevlerinin sahipliğinde)
  — yalnız stok modu alanı, ayrıntı satırı ve testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts (V1-RMD-097 sahipliğinde) — yalnız
  stok modu etiketleri

## In scope

- Yeni ürün formundaki stok modu seçenekleri, varsayılanı ve açıklaması.
- Ürün ayrıntısında stok modunun Türkçe gösterimi.

## Out of scope

- `StockMode` enum'unun ya da API'nin değiştirilmesi; stok modunun satış yolunda okunması (V1-RMD-143 kararı
  değişmedikçe gerekmez).

## Dependencies

- V1-RMD-436

## Acceptance evidence

- PosTerminal testleri (`pnpm test`) ve tip denetimi (`pnpm typecheck`) geçer (`evidence/V1-RMD-437/tests.log`).
  Yeni testler: yeni ürün formunda "Takipsiz" seçeneği yok ve varsayılan "Miktar takipli"; ayrıntı panelinde eski
  kayıt "Takipsiz (eski kayıt)" olarak görünür, ham "Untracked" görünmez. Değişiklik geri alınınca kırmızı
  (`evidence/V1-RMD-437/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: Katalog → Ürünler → yeni ürün; "Stok modu" listesinde "Takipsiz" yok, altında
  "Siparişin onaylanması için ürünün stok eşlemesi olmalı" yazar. Eski bir ürünün ayrıntısında stok modu Türkçe
  görünür.

## Handoff

- None
