# V1-RMD-005 - Define production dual-screen POS topology

- Task ID: V1-RMD-005
- Status: Done
- Assignee: Codex-/root
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-08-24

## Goal

Kasada aynı Windows iş istasyonuna bağlı operatör ekranı ile salt okunur müşteri ekranının gerçek kullanımdaki
çalışma, veri otoritesi, eşleştirme, yeniden bağlanma, güvenlik ve arıza davranışını tek bir üretim kararıyla
sabitlemek.

## Owned surface

- `docs/architecture/dual-screen-pos-topology.md`
- `evidence/V1-RMD-005/**`

## In scope

- Kasa operatörü ve müşteri ekranının süreç, pencere ve ekran yerleşimi sınırları.
- PostgreSQL otoritesi, HTTP snapshot ve SignalR güncelleme akışının rolleri.
- Terminal-ekran eşleştirmesi, kısa ömürlü bağlantı yetkisi, oturum yenileme ve yetki iptali.
- Snapshot sürümü, sıralama, yeniden bağlanma, stale ekran, ödeme ve sipariş kapanış davranışı.
- Müşteri ekranında gösterilebilen veri ve gizli kalması gereken personel, güvenlik, ödeme ve mali veriler.
- Uygulama, kurulum, güvenlik ve gerçek donanım kabul görevlerine aktarılacak kesin sorumluluk sınırları.

## Out of scope

- WebPrototype veya production kodu, test, migration, solution/project/build dosyası değiştirmek.
- Payment, mali cihaz, yazıcı veya dış sağlayıcı davranışını gerçek sözleşme ve cihaz kanıtı olmadan tanımlamak.
- İkinci ekranı tarayıcı sekmeleri arası bellek, BroadcastChannel veya IndexedDB ile authoritative hale getirmek.

## Dependencies

- V0-ARC-004
- V0-ARC-007
- V1-RMD-004

## Acceptance evidence

- Tek karar kaydı; kaynakları, erişim tarihlerini, onaylayanı, seçilen sonucu, reddedilen alternatifleri ve etkilenen
  exact Task ID'lerini içerir.
- Operatör ekranı ile müşteri ekranı aynı siparişin versioned server snapshot'ını izler; SignalR yalnız invalidation
  ve güncelleme taşıyıcısıdır, veri otoritesi değildir.
- Bağlantı kesintisi, process restart, müşteri ekranı kapanması, yanlış ekran eşleşmesi, sipariş kapanışı ve stale
  projection için fail-closed davranış tanımlıdır.
- Müşteri ekranı hiçbir personel PIN'i, auth token, ödeme credential'ı, mali cihaz detayı veya gereksiz kişisel veri
  göstermez.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- Semih; iki fiziksel ekranda sipariş açma, satır ekleme/çıkarma, ödeme ilerlemesi, bağlantı kesme/geri getirme ve
  sipariş kapatma akışını karar kaydındaki gözlenebilir sonuçlarla elle doğrulayabilir.

## Handoff

- None
