# V1-RMD-027 - Operational bill-splitting API

- Task ID: V1-RMD-027
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Mevcut eşit, ürün/miktar ve tutar hesap bölme tasarımını; kalıcı sandalye/kişi sahip referansları, atomik değiştirme ve
kurtarılabilir concurrency hatalarıyla yetkili, versioned Host API üzerinden açmak. Ödeme yürütme bu görevin dışında
kalır.

## Owned surface

- `src/Modules/Billing/SplitDesign/**`
- `src/Modules/Billing/BillFoundation/BillingModule.cs`
- `src/Host/Experience/Billing/**`
- `tests/Modules/Billing/SplitDesign/**`
- `tests/Host/Experience/Billing/**`
- `evidence/V1-RMD-027/**`

## Dependencies

- V1-RMD-026
- V1-BIL-004

## Acceptance evidence

- Okuma, kişi başına eşit, ürün/miktar, tutar, özel kayıt ve sıfırlama endpoint'leri authoritative hesap ve bölme
  repository'lerini kullanır. Verilen kalıcı sandalye referansları hesabın masa/salon bağlamına göre doğrulanır.
- Cumulative quantity cannot exceed the bill item quantity; amount totals exactly equal payable amount; tax and kuruş
  remainder are deterministic. Save replaces the design atomically and stale bill/allocation versions return `409`
  while preserving the caller draft.
- Ödenmiş veya desteklenmeyen hesap durumları fail-closed olur. Eksik session `401`, eksik izin `403` döndürür; hiçbir
  endpoint ödeme gerçekleştiğini iddia etmez.
- Release build, domain, repository ve gerçek HTTP/PostgreSQL testleri exit code `0` verir. Gerçek senaryo eşit, ürün ve
  tutar bölmelerini kaydeder, fazla dağıtımı reddeder, restart sonrasında yeniden yükler ve tasarımı sıfırlar.

## Handoff

- V1-RMD-028
