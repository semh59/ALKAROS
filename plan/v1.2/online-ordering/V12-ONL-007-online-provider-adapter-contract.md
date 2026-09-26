# V12-ONL-007 - Online platform adaptör sözleşmesi ve Yemeksepeti'nin bu sözleşmeye taşınması

- Task ID: V12-ONL-007
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Goal

Yemeksepeti akışı sağlayıcıya özel yazıldı. Her platformun aynı iç akışa (alım, stok tutma, mutfak, durum
senkronu, mutabakat) bağlanabilmesi için tek bir adaptör sözleşmesi tanımlanır ve Yemeksepeti bu sözleşmeye
taşınır; davranışı değişmez.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/Contracts/**`
- `src/Modules/OnlineOrdering/Yemeksepeti/Provider/**`
- `evidence/V12-ONL-007/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Yol notu: sözleşme testleri yeni bir test projesi açmamak için (çözüm dosyası V1-FND-001'e ayrılmış) mevcut
  Host OnlineOrdering test projesinde yaşar.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/, OrderNormalization/ ve StatusSync/ (V12-MAP-002,
    V12-ONL-002, V12-ONL-003) — ortak modellerin sözleşmeye taşınması.
  - src/Modules/OnlineOrdering/OrderLinks/ (V12-ONL-006) — siparişin platformunu okuma.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — adaptör kaydı.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-ONL-003, V12-OUI-001) — alım, teslim ve iptalin sözleşme
    üzerinden çalışması.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ ve tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. `IOnlineOrderProvider` (`Providers/Contracts`): platform kimliği ve Türkçe adı, sipariş numarası öneki, durum
   olayının iç komuta eşlenmesi, siparişin iç modele çevrilmesi, iptal için kalem referansları, bir siparişin
   teslimde bildirilecek durumu ve dışarı giden durum isteğinin kuyruğa alınması.
2. Ortak modeller (`StatusMappingResult`, `CancellationDetail`, `NormalizedOnlineOrder`, `NormalizationResult`,
   giden durum ve iptal nedeni) sözleşmeye taşınır; Yemeksepeti kendi yük ve olay biçimlerini tutar.
3. `OnlineOrderProviderRegistry`: platform kimliğine göre adaptör; aynı kimlikle iki adaptör ve bilinmeyen
   kimlik açık hatadır.
4. Host alım, teslim ve restoran iptali adaptör üzerinden çalışır; teslim ve iptal, siparişin platformunu bağ
   tablosundan (V12-ONL-006) okur. Sipariş kilidi platformu içerir.
5. Yemeksepeti davranışı değişmez: mevcut Yemeksepeti testlerinin hepsi geçer. İkinci bir adaptör (yalnız
   testte) aynı teslim ve iptal akışından geçer.

## Out of scope

- Gelen kutusu ve ürün eşleme tablolarının ortaklaştırılması (V12-ONL-008); alım bu görevde hâlâ Yemeksepeti
  gelen kutusundan okur.

## Dependencies

- V12-ONL-006

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-007/` altında.
- `task_scope_tool.py --task-id V12-ONL-007 --diff-base <InProgress commit>` exit 0.
- Sözleşme testleri Yemeksepeti ve bir test adaptörünü aynı teslim ve iptal akışından geçirir.

## Handoff

- None
