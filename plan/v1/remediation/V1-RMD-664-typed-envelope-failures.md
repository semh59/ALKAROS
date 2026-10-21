# V1-RMD-664 - Bozuk şifreli zarf için tipli hata ve zarf güvenlik testleri

- Task ID: V1-RMD-664
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B019 ve B011'in SensitiveData kısmı kapanır (V1-RMD-663'ten taşındı). B019: yanlış boyutlu nonce/etiket veya eksik alanlı kayıtlı zarf ham `ArgumentException` yerine tipli `SensitiveDataEncryptionException` / `SensitiveDataException` fırlatır. B011: eksik testler eklenir (nonce benzersizliği, 16/24/31/33 bayt anahtar reddi, kurcalanmış nonce, kurcalanmış etiket).

## Owned surface

- `plan/v1/remediation/V1-RMD-664-typed-envelope-failures.md`
- `evidence/V1-RMD-664/**`
- `tests/BuildingBlocks/Security/SensitiveData/Encryption/EnvelopeHardeningTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Security/SensitiveData/AesGcmEnvelopeCipher.cs - yalnız boyut hatası dönüşümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs - yalnız bozuk girdi dönüşümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-663-envelope-hardening-and-boundary-coverage.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İki dönüşüm, yeni test dosyası ve bunların mutasyon kanıtı.

## Out of scope

- Zarf AAD'sinin kayda bağlanması (B020), sır adı çakışması (B025), küçük sertleştirmeler kümesi (B026): sonraki görevde.

## Dependencies

- V1-RMD-663

## Acceptance evidence

1. Önce kırmızı test, sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-664/` altında çıktılar; SensitiveData ve zarfı kullanan test projeleri yeşil.

## Handoff

- V1-RMD-665
