# V1-RMD-531 - Giriş ekranı ve kasa girişi düzeltmeleri

- Task ID: V1-RMD-531
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-15

## Goal

Taze sayfa açılışında henüz oturum açılmamışken "Oturumunuz sonlandırıldı" uyarısı görünmez; /cashier/ sayfasında oturum yoksa giriş sayfasına giden bir bağlantı gösterilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-531-login-entry-fixes.md`
- `evidence/V1-RMD-531/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/cashier-session-link.test.js
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İlk oturum yoklaması 401 dönse bile mesaj yalnız daha önce oturum açılmışsa gösterilir.
- /cashier/ oturum yok mesajının yanında "Giriş sayfasına git" bağlantısı.

## Out of scope

- Genel arayüz yerleşim yeniden tasarımı (ayrı görev).

## Dependencies

- None

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-531/` altındadır.

## Handoff

- None
