# V12-ONL-010 - Kayıtlı her platformun gelen kutusu olaylarını siparişe çevir

- Task ID: V12-ONL-010
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Goal

Ortak gelen kutusundaki bir olayı siparişe çeviren alım bugün yalnız Yemeksepeti için çalışıyor ve kutudaki
şifreli yükü yalnız Yemeksepeti webhook bileşeni açabiliyor. Kutuya yazma ve yükü açma ortak bir bileşene
taşınır; alım kayıtlı her platform için aynı akışla (stok, mutfak, iptal, mutabakat) çalışır. Yemeksepeti
davranışı değişmez. `V12-GOV-008` ile açıldı.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/Inbox/**`
- `evidence/V12-ONL-010/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/Providers/Contracts/ (V12-ONL-007) — platform sözleşmesi.
  - src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/ (V12-ONL-001) — kutuya yazmanın ortak bileşene devri.
  - src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/ (V12-ONL-002) — işlenecek olay seçiminin platform parametresi.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002) — alım servisinin platform parametresi ve zamanlanmış servis.
  - src/Host/DualScreen/DualScreenApplication.cs — kayıt.
  - tests/Host/Experience/OnlineOrdering/ — uçtan uca testler.
  - tests/Modules/OnlineOrdering/Yemeksepeti/ — testler.

## In scope

1. Ortak gelen kutusu: platform, olay anahtarı, platform sipariş kimliği, durum ve şifreli ham yük; aynı platformda
   aynı olay anahtarı tek kayıt olur. Olay anahtarını platform adaptörü üretir, böylece webhook ve çekme aynı
   olayı aynı anahtarla yazar.
2. Yükü yalnız ortak kutu açar; mevcut Yemeksepeti kayıtları aynı ana anahtarla açılmaya devam eder.
3. Alım servisi ve zamanlanmış işleyici kayıtlı her platform için çalışır; platforma özel kısım yalnız
   `IOnlineOrderProvider` üzerindendir.

## Out of scope

- Platforma özel yük okuma (adaptör görevlerinde), çekme (V12-ONL-009).

## Dependencies

- V12-ONL-008
- V12-GOV-008

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-ONL-010/` altında.
- `task_scope_tool.py --task-id V12-ONL-010 --diff-base <InProgress commit>` exit 0.
- İkinci bir test platformunun kutudaki olayı sipariş olur; Yemeksepeti testleri değişmeden geçer.

## Handoff

- None
