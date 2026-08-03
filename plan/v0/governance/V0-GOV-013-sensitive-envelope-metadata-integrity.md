# V0-GOV-013 - Authenticate sensitive envelope metadata

- Task ID: V0-GOV-013
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

SensitiveEnvelope metadata'sini ciphertext ile birlikte dogrulamak ve yetki
kararinin degistirilmis kategori veya retention metadata'sina dayanmamasini
saglamak.

## Owned surface

- `src/BuildingBlocks/Security/SensitiveData/AesGcmEnvelopeCipher.cs`
- `src/BuildingBlocks/Security/SensitiveData/EnvelopeCiphertext.cs`
- `src/BuildingBlocks/Security/SensitiveData/IEnvelopeCipher.cs`
- `src/BuildingBlocks/Security/SensitiveData/SensitiveEnvelope.cs`
- `src/BuildingBlocks/Security/SensitiveData/SensitivePayloadProtector.cs`
- `tests/BuildingBlocks/Security/SensitiveData/Fixtures/SensitiveDataFixtures.cs`
- `tests/BuildingBlocks/Security/SensitiveData/MetadataIntegrity/EnvelopeMetadataIntegrityTests.cs`
- `evidence/V0-GOV-013/**`

## In scope

- AES-GCM authenticated associated data, canonical metadata encoding ve
  metadata kurcalamasini reddeden automated testler.

## Out of scope

- Access policy kural seti, key provider, retention politikasi, persistence
  schema veya plaintext payload semasini degistirmek.

## Dependencies

- V0-GOV-012
- V1-SEC-002

## Deliverables

- Metadata-authenticated envelope contract'i ve kategori, zaman damgasi,
  key kimligi kurcalamasini reddeden test kaniti.

## Acceptance evidence

- Metadata'daki tek byte degisiklik decrypt veya policy bypass uretemez.
- Yetkili, değiştirilmemiş zarfı aynı içerikle açar; yetkisiz çağırıcı çözme
  işlemi başlatmaz.
- SensitiveData test projesi basariyla tamamlanir.

## Handoff

- V1-SEC-003
