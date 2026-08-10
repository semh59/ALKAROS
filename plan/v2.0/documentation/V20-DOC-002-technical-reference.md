# V20-DOC-002 - Publish technical reference

- Task ID: V20-DOC-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: documentation
- Surface state: Planned

## Source basis

- PDF:I.45.1
- PDF:I.51
- PDF:I.54

## Goal

release ile eşleşen bir mimari, contract modülü, API/event, veri sahipliği, entegrasyon ve tanılama referansı
yayınlayın.

## Owned surface

- `docs/technical/**`
- Bu görev product contracts, migrations veya runbook içeriğini yeniden tanımlayamaz.

## In scope

- Modül/bağımlılık haritası, API/event şemaları, veri sözlüğü/sahiplik, migration dizini, entegrasyon yapılandırması,
  gözlemlenebilirlik ve tanılama paketi kullanımı.

## Out of scope

- Kullanım kılavuzu, hukuki tavsiye, gizli değerler ve uygulanmamış gelecek tasarımı.

## Dependencies

- V0-ARC-001
- V0-ARC-004
- V15-RUN-001
- V15-SUP-001

## Deliverables

- Oluşturulan contract/şema bağlantılarıyla birlikte sürümlendirilmiş teknik referans.

## Acceptance evidence

- Otomatik bağlantı/şema kontrolleri başarıyla tamamlanır ve temiz bir incelemeci, her genel contract ve sahip olunan
  veri kümesini tam olarak tek bir modül ve release sürümüyle eşleyebilir.
- `V15-RUN-001` kanıtlı `NotApplicable` ise runbook bağlantıları referansa dahil edilmez; kalan contract/şema eşleme
  kuralı yine geçerlidir.

## Handoff

- V20-REL-001
- V20-GAT-002
