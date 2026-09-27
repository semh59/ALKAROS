# V12-OUI-006 - Online Yemek Sorunlar sekmesi

- Task ID: V12-OUI-006
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Online siparişlerle ilgili açık mutabakat vakaları (reddedilen sipariş, platforma bildirilemeyen durum, fiyat veya
toplam farkı, stok bildirilemedi, çekme hataları) platform adıyla tek listede görünür; yönetici güvenli sonraki
eylemi (yeniden dene, çözüldü) buradan uygular. `V12-GOV-009` ile açıldı.

## Owned surface

- `src/Clients/PosTerminal/src/features/online-problems/**`
- `evidence/V12-OUI-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-27 kararı):
  - src/Clients/PosTerminal/src/features/online-hub/ (V12-OUI-004) — sekmenin yerleştirilmesi.
  - src/Host/Experience/Reconciliation/ (V12-REC-001) — online vakaların listelenmesi.
  - tests/Host/Experience/Reconciliation/ — testler.

## In scope

1. Online sipariş vakalarının listesi: tür, platform, sipariş kodu, tutar, sonraki eylem; hepsi Türkçe etiketle.
2. Yeniden dene ve çözüldü eylemleri mevcut mutabakat uç noktalarıyla; yetki ve sürüm kontrolü sunucudadır.
3. Kaynak henüz düzelmemişken çözme reddi açık Türkçe mesajla gösterilir.

## Out of scope

- Ödeme mutabakatı vakaları.

## Dependencies

- V12-OUI-004

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; PosTerminal testleri yeşil; mutasyon kontrolü
  `evidence/V12-OUI-006/` altında.
- `task_scope_tool.py --task-id V12-OUI-006 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
