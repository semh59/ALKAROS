# V1-RMD-460 - Trendyol Go ürün eşleme ekranı

- Task ID: V1-RMD-460
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Trendyol Go siparişlerindeki ürün kimliklerini katalog ürünlerine eşleyen bir Yönetim ekranı sunulur. Bugün bu eşleme için
ekran yoktur; eşlenmemiş ürün içeren siparişler hangi ürüne denk geldiğini kimseye göstermeden bekler. Uç noktalar ve ekran
başlangıçta bu görevin kapsamı olarak netleştirilir; sağlayıcı kimlik bilgisi olmadan çalışan taraf yalnız yerel eşleme
kayıtlarıdır (Trendyol'a çağrı yapılmaz).

## Owned surface

- `plan/v1/remediation/V1-RMD-460-trendyol-go-product-mapping-screen.md`

## In scope

- Eşlenmemiş Trendyol Go ürünlerinin listesi, katalog ürününe eşleme ve eşlemeyi kaldırma; Türkçe metinler.

## Out of scope

- Trendyol'a gerçek çağrı (V12-TGO-001 gerçek satıcı hesabı bekliyor).
- Yemeksepeti ürün eşlemesi (mevcut ekran).

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-460/` altındadır. Başlarken owned surface genişletilir.

## Handoff

- None
