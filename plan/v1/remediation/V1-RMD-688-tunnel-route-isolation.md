# V1-RMD-688 - Tünel yolu rota izolasyonu ve güvenlik başlıkları (Yüksek)

- Task ID: V1-RMD-688
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B001, B030, B075, B136 kapanır. Tünel (bağlayıcı) üzerinden gelen istek NFC/QR rota izolasyonuna girer: tünelden yalnız QR/NFC uçları sunulur, giriş, yönetim ve kasa uçları sunulmaz; /hubs/* yolları da kapsanır. /qr ve /nfc statik sayfaları güvenlik başlıklarıyla çıkar. Yanlış güvenlik gerekçesi veren yorumlar koda göre düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-688-tunnel-route-isolation.md`
- `evidence/V1-RMD-688/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız kaynak kapısı, rota izolasyonu ve başlık sırası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/** - yalnız bayrak ve seçenek bağlama
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız api ve connector bayrakları ile yorumları
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/dual-screen-pos-topology.md - yalnız tünel ve rota bölümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-599-host-quality-audit.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kaynak kapısının bağlayıcı ağını tanıması, tünel isteklerinde X-Alkaros-Origin başlığının temizlenmesi, /hubs/* izolasyonu, başlık sırası, yorum düzeltmeleri; kapıyı bağlayıcı adresiyle sınayan testler.

## Out of scope

- cloudflared'in gerçek başlık davranışının canlıda doğrulanması (V0-QRG-001); CSP/HSTS kararı (V1-RMD-335).

## Dependencies

- V1-RMD-599

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-688/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-689
