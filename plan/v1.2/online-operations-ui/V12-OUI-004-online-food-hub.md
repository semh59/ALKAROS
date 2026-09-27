# V12-OUI-004 - Online Yemek merkez ekranı

- Task ID: V12-OUI-004
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Goal

Online yemek tek ekrandan kullanılır: yan menüde tek "Online Yemek" girişi; Siparişler, Menü, Sorunlar ve Ayarlar
sekmeleri. Menü ve Ayarlar yalnız yönetici oturumunda görünür. Siparişler sekmesinin üstünde her platformun bağlantı
durumu (ayarlar girili mi, son sipariş ne zaman geldi, son hata) durur. `V12-GOV-009` ile açıldı.

## Owned surface

- `src/Clients/PosTerminal/src/features/online-hub/**`
- `src/Host/Experience/OnlineOrdering/OnlineChannelHealthEndpoints.cs`
- `evidence/V12-OUI-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-27 kararı):
  - src/Clients/PosTerminal/src/routes/ — tek giriş, eski iki yolun yeni ekrana yönlenmesi.
  - src/Clients/PosTerminal/src/shell/ — menü girişi.
  - src/Clients/PosTerminal/src/strings.ts — giriş etiketi.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — sekme olarak yerleştirme.
  - src/Clients/PosTerminal/src/features/online-platform-credentials/ (V12-OUI-003) — sekme olarak yerleştirme.
  - src/Host/DualScreen/DualScreenApplication.cs — uç nokta kaydı.
  - tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. Sekmeli merkez ekran ve tek menü girişi; eski `/online-operations` ve `/online-platforms` yolları yeni ekranın
   ilgili sekmesine açılır.
2. Menü ve Ayarlar sekmeleri yalnız `integrations.manage` yetkili oturumda görünür; yetkisiz oturum bu sekmelerin
   verisini hiç istemez.
3. Platform bağlantı durumu uç noktası (`orders.create`): platform başına ayarlar girili mi, son alınan olay,
   çekme durumu ve son hata; ham hata metni ekrana basılmaz.

## Out of scope

- Menü (V12-OUI-005), Sorunlar (V12-OUI-006) ve restoran açma/kapama (V12-ONL-011) sekmelerinin içeriği.

## Dependencies

- V12-GOV-009

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; PosTerminal testleri yeşil; mutasyon kontrolü
  `evidence/V12-OUI-004/` altında.
- `task_scope_tool.py --task-id V12-OUI-004 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
