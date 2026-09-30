# V1-RMD-460 - Trendyol Go: eşlenmemiş ürün kodlarını Yönetim menü yanıtına eklemek

- Task ID: V1-RMD-460
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Trendyol Go (ve Yemeksepeti) siparişlerinde katalogdaki hiçbir ürüne eşli olmayan platform ürün kodu bugün yalnız
`online_ordering.provider_inbox` içinde reddedilmiş satır olarak kalır; hangi kodun eşlenmesi gerektiğini kimse görmez.
Eşleme uç noktaları ve seçim listesi zaten vardır (`/online-menu/{provider}`). Bu görev aynı yanıta, son 30 günde
`UnmappedSku` gerekçesiyle reddedilmiş ve şu an hâlâ eşli olmayan platform kodlarını (kod, sipariş sayısı, son görülme)
ekler. Yeni tablo, uç nokta ya da izin yoktur; yerel veri okunur, Trendyol'a çağrı yapılmaz. Ekran tarafı `V1-RMD-462`dedir.

## Owned surface

- `plan/v1/remediation/V1-RMD-460-trendyol-go-product-mapping-screen.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/OnlineMenuEndpoints.cs - yalnız yanıta eşlenmemiş kod listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/OnlineMenuHttpTests.cs - yalnız yeni alanın testleri

## In scope

- `OnlineMenuV1` yanıtına `UnmappedCodes` (kod, reddedilen sipariş sayısı, son görülme zamanı) eklenmesi; sağlayıcıya göre süzme,
  eşleşmiş kodların ve 30 günden eski kayıtların dışlanması, en fazla 100 kod.
- Başarı, sağlayıcı ayrımı, eşlenince listeden düşme ve yönetici dışı erişimin reddi testleri.

## Out of scope

- Ekran (`V1-RMD-462`); Trendyol'a gerçek çağrı (V12-TGO-001 gerçek satıcı hesabı bekliyor); reddedilmiş siparişin yeniden işlenmesi.

## Dependencies

- None

## Acceptance evidence

- Testler ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-460/` altındadır.

## Handoff

- V1-RMD-462
