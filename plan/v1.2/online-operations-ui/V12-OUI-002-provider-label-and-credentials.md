# V12-OUI-002 - Online kuyrukta platform etiketi ve süzgeci

- Task ID: V12-OUI-002
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Operasyon ekranı siparişin ve sorunlu olayın hangi platformdan geldiğini göstermiyor. Kuyruktaki her online sipariş
ve sorun Türkçe platform adını taşır; kuyrukta birden fazla platform varsa personel platforma göre süzebilir.
Platform kimlik bilgisi ekranı, bölme testi gereği ayrı görevdir (V12-OUI-003).

## Owned surface

- `evidence/V12-OUI-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs (V12-OUI-001) — sipariş ve sorunda platform.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — etiket ve süzgeç.
  - tests/Host/Experience/OnlineOrdering/ — HTTP testleri.

## In scope

1. Kuyruk yanıtında her sipariş (platform bağından, V12-ONL-006) ve her sorun (gelen kutusundan) platform kimliğini
   taşır; QR siparişinde boştur.
2. Ekran platformu Türkçe adla gösterir; tanınmayan platform "Diğer platform" olur, ham kimlik ekrana çıkmaz.
3. Kuyrukta birden fazla platform varsa platform süzgeci görünür ve yalnız seçilen platformun sipariş ve sorunlarını
   gösterir; tek platform varken süzgeç gösterilmez.

## Out of scope

- Platform kimlik bilgisi ekranı (V12-OUI-003).

## Dependencies

- V12-ONL-008

## Acceptance evidence

- Host OnlineOrdering ve PosTerminal testleri yeşil, PosTerminal üretim derlemesi başarılı; mutasyon kontrolü
  `evidence/V12-OUI-002/` altında.
- `task_scope_tool.py --task-id V12-OUI-002 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
