# V1-RMD-639 - Denetim kaydının iş işlemiyle aynı işlemde yazılması

- Task ID: V1-RMD-639
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Para ve stok işlemlerinde denetim satırı, iş işleminin commit sonrasında ayrı bağlantıda yazılıyor; yazma başarısız olursa iş kalıcı olur, iz kalmaz. IAuditEventStore bağlantı ve işlem alan bir aşırı yükleme kazanır; 27 AppendAsync çağrı noktasından commit sonrası yazanlar (iptal, bölme, iade, ödeme, sipariş yönetimi) aynı işleme alınır; 6 ham INSERT store üzerinden geçer; void, ikram ve gönderilmiş void için çift kayıt teke iner; Host ve modülde çift DI kaydı teke iner; kullanılmayan toplu yazma yolu ya kaldırılır ya da kullanılır. Kaynak bulgular: F01, F09, F18, F20, Y1 (V1-RMD-595); önem: Yüksek.

## Owned surface

- `plan/v1/remediation/V1-RMD-639-audit-write-atomic-with-business-transaction.md`
- `evidence/V1-RMD-639/**`
- Kod yolları başlangıç commit'inde Sınırlı ek olarak eklenir; bu görev başka görevin yüzeyini değiştiremez.

## In scope

- Audit modülü store arayüzü, Billing, Orders, Payments ve OnlineOrdering uç dosyalarındaki AppendAsync çağrıları ve ham audit INSERT yerleri; her değişen çağrı için, denetim yazımı hata verdiğinde iş işleminin de geri alındığını gösteren gerçek PostgreSQL testi.

## Out of scope

- Bu görevin kaynak bulgularında olmayan refactor ve yeni özellik.

## Dependencies

- V1-RMD-595

## Acceptance evidence

1. Her kaynak bulgu için önce kırmızıya dönen test, sonra düzeltme ve yeşil koşu; gerçek PostgreSQL testleri.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-639/` altındadır.

## Handoff

- None
