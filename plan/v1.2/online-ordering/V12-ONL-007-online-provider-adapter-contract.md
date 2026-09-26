# V12-ONL-007 - Online platform adaptör sözleşmesi ve Yemeksepeti'nin bu sözleşmeye taşınması

- Task ID: V12-ONL-007
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Yemeksepeti akışı sağlayıcıya özel yazıldı. Her platformun aynı iç akışa (alım, stok tutma, mutfak, durum
senkronu, mutabakat) bağlanabilmesi için tek bir adaptör sözleşmesi tanımlanır ve Yemeksepeti bu sözleşmeye
taşınır; davranışı değişmez.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/Contracts/**`
- `tests/Modules/OnlineOrdering/Providers/Contracts/**`
- `evidence/V12-ONL-007/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/ (V12-ONL-001, V12-ONL-002, V12-ONL-003, V12-MAP-001, V12-MAP-002) — sözleşmeyi uygulama.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — adaptör kaydı.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-ONL-003) — orkestrasyonun sözleşme üzerinden çalışması.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ ve tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. `IOnlineOrderProvider`: gelen isteği doğrula, siparişi iç modele çevir, durum olayını iç komuta eşle, dışarı giden durum isteğini üret; platform kimliği ve deneme profili.
2. Yemeksepeti adaptörü sözleşmeyi uygular; mevcut Yemeksepeti testlerinin hepsi değişmeden geçer.
3. Host orkestrasyonu (alım, iptal, teslim) platformu adaptörden alır.

## Out of scope

- Gelen kutusu ve ürün eşleme tablolarının ortaklaştırılması (V12-ONL-008).

## Dependencies

- V12-ONL-006

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-007/` altında.
- `task_scope_tool.py --task-id V12-ONL-007 --diff-base <InProgress commit>` exit 0.
- Sözleşme testleri iki farklı sahte olmayan adaptör davranışını (Yemeksepeti ve test platformu) aynı akıştan geçirir.

## Handoff

- None
