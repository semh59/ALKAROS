# V1-RMD-663 - Zarf sertleştirme, olay zarfı doğrulaması ve mimari test kapsamı

- Task ID: V1-RMD-663
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B011, B013, B015, B019 ve B022 kapanır. B019: bozuk veya eksik alanlı şifreli zarf ham `ArgumentException` yerine tipli `SensitiveDataException` / `SensitiveDataEncryptionException` fırlatır. B022: `OutboxEnvelope` boş (`Guid.Empty`) toplulaştırıcı kimliğini reddeder. B015: olay serileştirici yorumu gerçeği söyler (QR siparişi serbest metin notu taşıyabilir). B011: eksik testler eklenir (nonce benzersizliği, anahtar uzunluğu 16/24 bayt reddi, kurcalanmış nonce/etiket, olay sırası, vadesi gelmemiş mesaj talep edilmez, geri çekilme çizelgesi). B013: mimari modül listesi Payments, QrOrdering, Invoicing ve Privacy modüllerini de izler.

## Owned surface

- `plan/v1/remediation/V1-RMD-663-envelope-hardening-and-boundary-coverage.md`
- `evidence/V1-RMD-663/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Security/SensitiveData/AesGcmEnvelopeCipher.cs - yalnız boyut hatası dönüşümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs - yalnız bozuk girdi dönüşümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Messaging/OutboxEnvelope.cs - yalnız kimlik doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/IntegrationContracts/IntegrationEventSerializer.cs - yalnız yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs - yalnız modül listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Security/SensitiveData/Encryption/EnvelopeHardeningTests.cs - yalnız yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Messaging/OutboxOrderingTests.cs - yalnız yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-662-connector-key-and-deadletter-watch.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Zarf bozuk girdi dönüşümü, olay zarfı kimlik doğrulaması, yorum düzeltmesi, yeni testler ve mimari liste genişletmesi.

## Out of scope

- Zarf AAD'sinin kayda bağlanması (B020; kayıtlı veriyi okunamaz yapar, sürümlü bir geçiş gerektirir), sır adı çakışması (B025), küçük sertleştirmeler kümesi (B026), eksik alanlı JSON olay yükü doğrulaması: belge/karar görevinde gerekçesiyle ele alınır.
- Mutfak olayı atomikliği (B010) ayrı görevdir.

## Dependencies

- V1-RMD-662

## Acceptance evidence

1. Önce kırmızı test, sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-663/` altında çıktılar; SensitiveData, Messaging ve mimari testleri yeşil.

## Handoff

- None
