# V1-RMD-327 - QR Relay: belgelenen "kuyruk + tampon" mimarisi kodda hiç yoktu

- Task ID: V1-RMD-327
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K16 bulgusu: `docs/architecture/qr-relay-topology.md` (V0-ARC-009, Semih'in onayladığı bir mimari karar belgesi) "Durable Queue, 7-day retention, at-least-once delivery" vaat ediyor; gerçek kod (`CloudflareApiClient.cs`, `RelayProvisioningService.cs`) doğrudan bir Cloudflare Tunnel reverse-proxy ingress'i kuruyor — kuyruk/tampon mekanizması hiç yok. `RelayProvisioningService.cs`'in tek yapılandırdığı origin (`http://api:5080`) tünelin İÇ hedefi; müşterinin telefonu için bir LAN/yerel erişim yolu hiç yayınlanmıyor. Cloudflare tüneli düşerse tüm QR sipariş kanalı sıfıra iner, hiçbir yedek yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-327-qr-relay-docs-vs-reality.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/qr-relay-topology.md

## In scope

1. `docs/architecture/qr-relay-topology.md`'ye (2026-09-17 tarihli mevcut "2a. Amendment" kaydının hemen bilinen deseniyle) yeni bir "2b. Amendment" bölümü eklenir: gerçek uygulamanın kuyruksuz bir reverse-proxy olduğunu, LAN yedeğinin hiç olmadığını dürüstçe kaydeder ve bunun bir iş/mimari kararı gerektirdiğini (Semih'in kararına açık bırakılarak) not düşer.

## Out of scope

- Gerçek bir dayanıklı kuyruk (mesaj broker'ı, 7 günlük saklama, en-az-bir-kez teslim) inşa edilmesi — bu, ayrı, büyük bir mimari çalışma gerektirir; bu görevin kapsamında SAHTE bir uygulama yapılmadı.
- Gerçek bir LAN yerel erişim yolu (ikili URL'li QR kod veya yerel ağ servis keşfi) inşa edilmesi — aynı şekilde ayrı, büyük bir çalışma.
- `DeleteTunnelAsync`'in yeniden provizyonda çağrılmaması (öksüz tünel bulgusu) — ayrı, orta seviye bir bulgu, ayrı görev.

## Dependencies

- None

## Acceptance evidence

Bu görev bir belge düzeltmesidir, kod değişikliği içermiyor. Kanıt: `docs/architecture/qr-relay-topology.md`'nin yeni "2b. Amendment" bölümü, gerçek kod dosyalarına (`CloudflareApiClient.cs`, `RelayProvisioningService.cs`) referans vererek gerçek mimariyi doğru tarif ediyor ve orijinal onaylı kararın (§1/§3) kuyruk/tampon iddialarının hiç uygulanmadığını açıkça kaydediyor — sonraki bir okuyucu artık orijinal kararı gerçekten çalışanla karıştırmayacak. `markdownlint-cli2` ile biçim doğrulandı (0 sorun).

## Handoff

- None
