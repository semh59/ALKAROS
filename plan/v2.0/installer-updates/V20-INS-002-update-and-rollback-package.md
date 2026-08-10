# V20-INS-002 - Build and verify update rollback package

- Task ID: V20-INS-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.45.1
- PDF:I.50
- CORR:C15

## Goal

Onaylı önceki kurulumu release candidate'a yükseltmek ve update migration öncesi/sonrası failure durumundan güvenle
kurtarmak.

## Owned surface

- `updater/**`, `tools/release/update/**`, `tests/Installer/UpdateRollback/**`
- Bu görev uygulama modüllerinin iş mantığını veya migration içeriğini değiştiremez.

## In scope

- Yapı doğrulama, uyumluluk ön kontrolü, bakım sınırı, güncelleme sıralaması, arıza kontrol noktaları ve uygulama ikili
  programı geri alma.

## Out of scope

- Veri geri alma uygulaması, otomatik production kullanıma sunma ve sessiz zorunlu güncellemeler.

## Dependencies

- V20-INS-001
- V0-DAT-001
- V15-BKP-002
- V0-ARC-007

## Deliverables

- İmzalı güncelleyici ve geri alma yapısı.
- Başarı/başarısızlık checkpoint matrisini ve otomatik testleri temizleyin.

## Acceptance evidence

- Enjekte edilen her hata, açık bir veri kurtarma talimatı içeren ve karışık ikili durum içermeyen önceki sağlıklı
  sürümü veya yeni sağlıklı sürümü bırakır.

## Handoff

- V20-MIG-001
- V20-REL-001
