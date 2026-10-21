# V1-RMD-675 - Oturum ömrünün üst sınırı ve kilitlenme denetim kaydı

- Task ID: V1-RMD-675
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B019 ve B026'nın giriş kısmı kapanır. Doğru PIN ile ekran kilidi açılınca oturum belirteci yenilenir ama oturumun ömrü uzamaz: yenilenen oturum, eskisinin bitiş anını aşamaz; sekiz saatlik giriş ömrü bir üst sınırdır ve parola ile yeniden giriş gerekir. Giriş denetimi, hesabın kilitlendiği anı (5. hatalı deneme) bir kez kaydeder; kilitliyken yapılan her deneme artık kayıt üretmez (kayıt şişmesi biter). Denetim kaydı yazılamazsa giriş 500 vermez, giriş sonucu aynen döner.

## Owned surface

- `plan/v1/remediation/V1-RMD-675-session-lifetime-and-lockout-audit.md`
- `evidence/V1-RMD-675/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/IdentityHardening/SessionRotationService.cs - yalnız üst sınır
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/IdentityHardening/SuspiciousLoginAuditingAuthenticationService.cs - yalnız kilitlenme anı ve yazım hatası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/IdentityHardening/SuspiciousLoginEvent.cs - yalnız bir yorum satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/IdentityHardening/SessionRotationServiceTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/IdentityHardening/SuspiciousLoginAuditingAuthenticationServiceTests.cs - yalnız yeni ve uyarlanan testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-674-waiter-discount-limit-and-delegator-check.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B019: yenilenen oturumun bitişi ≤ eski oturumun bitişi.
- B026: kilitlenme anı olayı, kilitliyken olay yok, yazım hatası girişi bozmaz.

## Out of scope

- B021 (terminal kimliği, hız sınırı kovası, 19 yönetim ucu), B025 (girişin tek işlemde olması), PIN deneme ve kurma denetimi, B039 (HSTS, kayıt ToString): ayrı iş. Kilitlenme olayında ham kullanıcı adı: kullanıcı adı gizli değildir, bu görevde değişmez.

## Dependencies

- V1-RMD-674

## Acceptance evidence

1. Her kural için gerçek PostgreSQL testi; B019 için mutasyon kanıtı.
2. `evidence/V1-RMD-675/` altında çıktılar; IdentityHardening testleri yeşil.

## Handoff

- V1-RMD-598
