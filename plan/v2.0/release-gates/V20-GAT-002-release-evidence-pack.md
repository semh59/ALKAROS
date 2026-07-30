# V20-GAT-002 - Assemble release evidence pack

- Task ID: V20-GAT-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.13-II.15
- PDF:III.29-III.40

## Goal

Tamamlanan gate çıktılarından, sonuçları yeniden yazmadan tamper-evident release evidence pack oluşturmak.

## Owned surface

- `release/evidence/package/**`
- Bu görev kaynak kanıtı veya ürün kodunu değiştiremez.

## In scope

- Kanıt bildirimi, yapı karmaları, yapı kimliği, görev status anlık görüntüsü, kusur envanteri ve onay referansları.

## Out of scope

- Hataları düzeltme, başarısız geçitlerden feragat etme ve production kullanıma sunma.

## Dependencies

- V20-GAT-001
- V20-MIG-002
- V20-DRL-001
- V20-SEC-001
- V20-CMP-001
- V20-UAT-003
- V20-REL-002
- V20-LIC-002

## Deliverables

- Sürümlendirilmiş kanıt bildirimi ve değişmez arşiv.
- Karma doğrulama komutu ve tekrarlanabilirlik talimatları.

## Acceptance evidence

- Temiz bir doğrulayıcı, tüm bildirim karmalarını yeniden üretir ve gerekli eksik geçit yapıtı veya çözülmemiş
  kritik/yüksek kusur olmadığını bildirir.
- Licensing `NotApplicable` ise pack, `V0-LIC-001`, `V20-LIC-001` ve `V20-LIC-002` için aynı decision ID, tarih ve
  approver zincirini içerir; aksi durumda license implementation/recovery evidence zorunludur.

## Handoff

- V20-REL-003
