# V1-RMD-704 - Katalog yönetim ekranının güvenilirliği

- Task ID: V1-RMD-704
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulguları B012 (yönetim listesi ilk 100 kayıtla sınırlı), B010 (sunucu hatası modalın arkasında kalıyor, çakışma tespiti İngilizce metne bağlı) ve B013 (oturum bitişi işlenmiyor) ile F-004, F-005 ve B-009 kapanır. Liste tüm sayfaları imleçle çeker; 401 ortak oturum-bitti işleyicisini çağırır; kayıt hatası modal içinde gösterilir ve çakışma durum koduyla ayırt edilir; çift gönderim engellenir; boş katalog doğru mesajı gösterir; fiyat başlangıç varsayılanı yerel saatle yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-704-catalog-screen-reliability.md`
- `evidence/V1-RMD-704/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/** - yalnız sayfalama, hata gösterimi, çift gönderim ve tarih varsayılanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız boş katalog durumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx - yalnız katalog rotası boş durum testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts - yalnız oturum-bitti bildirimini dışa açma
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İmleçle tüm sayfaları çekme, 401 işleme, modal içi hata ve durum koduyla çakışma tespiti, çift gönderim koruması, boş katalog durumu, yerel saat varsayılanı, testler.

## Out of scope

- Düzenleme ve pasifleştirme uçları (sunucu) ve ekranı ayrı iştir.
- Her işlemden sonra tam ekran yükleme titremesi (F-006) ve erişilebilirlik ayrıntıları.

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-704/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
