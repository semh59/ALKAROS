# V20-INT-006 - Certify QR public path

- Task ID: V20-INT-006
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.34-I.35
- CORR:C19

## Goal

Onaylı network/security topology altında scan işleminden PendingConfirmation Order'a kadar public QR path'i
sertifikalandırmak.

## Owned surface

- `release/evidence/integrations/qr-public-path/**`
- Bu görev QR uygulama veya relay kodunu değiştiremez.

## In scope

- TLS/etki alanı yolu, belirteç/oturum süresinin dolması, geçiş kimlik doğrulaması, kötüye kullanım sınırları, mobil
  tarayıcılar, erişilebilirlik ve beklemede-order oluşturma.

## Out of scope

- Personel onayı, çevrimiçi teslimat kanalları ve müşteri payment.

## Dependencies

- V14-QRS-001
- V14-QRS-002
- V14-QRS-003
- V14-QRT-001
- V14-CWB-001
- V14-CWB-002
- V14-QRO-001
- V0-CMP-005

## Deliverables

- Cihaz/tarayıcı/ağ matrisi ve düzenlenmiş güvenlik/işlevsel transkriptler.

## Acceptance evidence

- Onaylanan mobil/ağ servis talepleri tam olarak bir bekleyen order'ye ulaşıyor; süresi dolmuş, yeniden oynatılmış,
  çapraz table ve hızı sınırlı vakalar reddedilir ve denetlenir.
- Mobile browser akışları `V0-CMP-005` kararındaki customer QR success criteria ve exception kayıtlarını karşılar.

## Handoff

- V20-UAT-001
- V20-GAT-002
