# V0-QRG-001 - Validate QR relay threat model and feasibility

- Task ID: V0-QRG-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: validation
- Surface state: Existing

## Source basis

- PDF:I.6
- PDF:I.6.5

## Goal

Public QR trafiğinin local POS'a inbound LAN erişimi açmadan taşınabileceğini kanıtlamak.

## Owned surface

- `evidence/v0/integrations/V0-QRG-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outbound connector, authentication, token rotation, replay, rate limit, outage queue ve revocation.

## Out of scope

- QR ordering UI ve Order aggregate.

## Dependencies

- V0-ARC-009
- V0-ARC-003

## Deliverables

- V0-QRG-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Threat modelde açık critical risk yok ve local network'e public inbound port açmadan çalışan proof mevcut.
- 2026-09-07: Semih tarafından sağlanan gerçek, adlandırılmış bir domain
  (Cloudflare DNS üzerinde, repo'da adı gizlenmiş — bkz. kanıt dosyası)
  ile Cloudflare Tunnel üzerinden gerçek uçtan uca istek, outage (bağlayıcı
  süreç öldürülerek), recovery (yeniden başlatılarak) ve revocation (tünel
  silinerek) testleri yapıldı; hiçbiri simüle edilmedi. Ayrıntılı transkript:
  `evidence/v0/integrations/V0-QRG-001/2026-09-07-cloudflare-tunnel-feasibility.md`.
- Doğrulanmayan maddeler (çoklu-günlük outage queue ölçeği, hop-bazlı mTLS
  eşlemesi) o kanıt dosyasında açıkça "doğrulanmadı" olarak kaydedildi,
  varsayımla kapatılmadı.

## Handoff

- V14-QRT-001
- V14-QRS-001
- V14-QRS-002
