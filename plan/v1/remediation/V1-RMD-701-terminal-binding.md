# V1-RMD-701 - Terminal kimliğinin sunucuda doğrulanması

- Task ID: V1-RMD-701
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-599 bulguları B035 ve B088 kapanır. PO kararı: tarayıcıya bağlı gizli anahtar ve yönetici sıfırlama. Bir terminal kimliği ilk girişte tarayıcıya HttpOnly, SameSite=Strict bir gizli anahtar çerezi verir; sunucu yalnız özetini saklar. Aynı terminal kimliğiyle başka tarayıcıdan giriş reddedilir. Mevcut terminaller ilk girişte anahtarı devralır. Tarayıcı verisi silinen terminali yönetici ekrandan sıfırlar; sıfırlama denetim kaydı bırakır. Kasa oturum çerezi terminale bağlı olduğu için ayrı bir doğrulama gerekmez (B088 aynı kök neden).

## Owned surface

- `plan/v1/remediation/V1-RMD-701-terminal-binding.md`
- `evidence/V1-RMD-701/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız giriş, terminal anahtarı ve sıfırlama ucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/** - yalnız terminal yönetimi ucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/migrations/** - yalnız terminal anahtarı sütunu
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/** - yalnız terminal sıfırlama ekranı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Anahtar üretimi ve özet saklama, giriş doğrulaması, mevcut terminallerin devralınması, yönetici sıfırlama ucu ve ekranı, denetim kaydı, testler.

## Out of scope

- Cihaz kayıt ve onay akışı (yönetici onaylı yeni terminal ekleme) ayrı ürün işidir.

## Dependencies

- V1-RMD-696

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-701/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-600
