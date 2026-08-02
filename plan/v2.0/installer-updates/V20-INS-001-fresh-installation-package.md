# V20-INS-001 - Build and verify fresh installation package

- Task ID: V20-INS-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.45.1
- PDF:I.50
- CORR:C15

## Goal

Signed release candidate'ı deterministic ve belgelenmiş package ile clean supported target'a kurmak.

## Owned surface

- `installer/**`, `tools/release/install/**`, `tests/Installer/FreshInstall/**`
- Bu görev uygulama modüllerinin iş mantığını değiştiremez.

## In scope

- Önkoşul kontrolleri, paket imzası/karma, hizmet/veritabanı önyüklemesi, en az ayrıcalıklı kimlik, yapılandırma
  doğrulama, seçilen QR relay gateway/local connector deployment assets ve kaldırma sınırı.

## Out of scope

- Yerinde güncelleme, lisanslama politikası ve production dağıtımı.

## Dependencies

- V15-SEC-001
- V1-SET-001
- V0-ARC-007
- V14-QRT-001
- V0-ARC-009

## Deliverables

- İmzalı kurulum yapısı ve temiz makine test otomasyonu.
- Desteklenen hedef/önkoşul matrisi ve arıza teşhisi.

## Acceptance evidence

- Temiz bir şekilde desteklenen hedef, etkileşimli olmayan bir şekilde yüklenir, yapı kimliğini doğrular, gömülü gizli
  bilgi olmadan sağlıklı bir şekilde başlar ve karşılanmayan önkoşullarda güvenli bir şekilde başarısız olur.
- `V0-ARC-009` topology'sinin local connector ve seçilmiş relay deployment bileşenleri eksiksiz paketlenir; seçilmemiş
  provider/deployment artifact'i pakete girmez.

## Handoff

- V20-INS-002
- V20-MIG-001
- V20-DRL-001
