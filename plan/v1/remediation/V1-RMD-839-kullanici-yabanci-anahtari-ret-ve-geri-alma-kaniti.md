# V1-RMD-839 - Ödeme ve kasa kullanıcı yabancı anahtarları: ret ve geri alma kanıtı

- Task ID: V1-RMD-839
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubu, kayıt F0066 (V1-RMD-242): 131 numaralı migration `payments` ve `cash` tablolarına kullanıcı yabancı anahtarları ekledi; "var olmayan kullanıcı kimliği ekleme reddedilir" kabulü ve geri alma için test/kanıt yok. Bu görev yalnız eksik testi ekler: var olmayan kullanıcı kimliğiyle ekleme her kısıtta reddedilir, ve 131 için "uygula, geri al, tekrar uygula" sınaması listeye girer.

## Owned surface

- `evidence/V1-RMD-839/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — yeni testler.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Gerçek Postgres testleri: 131'in her yabancı anahtarı (oturum açan/kapatan/mutabık kılan, sayım yapan, defter kaydeden, iade isteyen, durum geçmişi değiştiren) var olmayan kullanıcı kimliğiyle reddedilir; boşaltılabilir olanlar `NULL` ile kabul edilir.
2. 131 geri alma testi: kısıtlar kalkar, yeniden uygulanır.
3. Mutasyon turu: kısıtı gevşeten (örneğin bir yabancı anahtarı kaldıran) bir test verisi mutantı testi kırar (`cp` + `cmp` ile geri alınır).

## Out of scope

- Migration dosyalarını değiştirmek.

## Dependencies

- V1-RMD-838

## Acceptance evidence

1. Migration test projesi yeşil; çıktı `evidence/V1-RMD-839/` altında.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
