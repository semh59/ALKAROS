# V12-REC-002 - Mutabakat vakalarını ve kanal raporunu platform bazında çalıştır

- Task ID: V12-REC-002
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Goal

Online mutabakat kaynak çiftleri ve kanal raporu siparişi dış numarasıyla, platformdan bağımsız eşliyor. İki
platform aynı numarayı kullandığında vakalar ve rapor satırları karışır. Her vaka ve rapor satırı hangi platforma
ait olduğunu taşır; eşleme V12-ONL-006 bağ tablosu üzerinden yapılır.

## Owned surface

- `evidence/V12-REC-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Yol notu: V12-ONL-009 (sipariş çekme) henüz yazılmadığı için çekme hatalarının fark türü bu görevden çıkarıldı;
  çekme altyapısı ilk kullanan platform adaptörüyle birlikte gelecek.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — platform alanı, anahtar ve bağ tablosuyla eşleme.
  - src/Modules/Reporting/Channels/ (V12-RPT-001) — platform kırılımı.
  - src/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncService.cs (V12-ONL-003) — kanıt kimliğinde platform.
  - tests/Modules/Reconciliation/OnlineOrders/ — testler.
  - tests/Modules/Reporting/Channels/ — testler.
  - tests/Host/Experience/Reconciliation/ — testler.
  - tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. Dış sipariş numarasıyla anahtarlanan vakalar (yerelde reddedilen, fiyat farkı, toplam farkı) anahtarda ve
   ayrıntıda platformu taşır; siparişle eşleme `(platform, dış numara)` bağ tablosundan yapılır.
2. Sağlayıcıya bildirilemeyen durum vakası siparişini bağ tablosundan bulur; her vakanın ayrıntısında platform
   vardır.
3. "Teslimden sonra iptal" kanıt kimliği platformu içerir; iki platformun aynı numarası tek kanıtta birleşmez.
4. Kanal raporu online satırlarını ve sağlayıcı retlerini platforma göre ayırır (`Provider`); platform bağı
   olmayan eski kayıt `Provider` boş satırda kalır.

## Out of scope

- Çekme servisi hataları (V12-ONL-009 ile birlikte); ekrandaki platform süzgeci (V12-OUI-002).

## Dependencies

- V12-ONL-008

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-REC-002/` altında.
- `task_scope_tool.py --task-id V12-REC-002 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
