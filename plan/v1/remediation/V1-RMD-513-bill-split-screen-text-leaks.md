# V1-RMD-513 - Hesap bölme ekranında İngilizce ve teknik metin sızıntısı

- Task ID: V1-RMD-513
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

UI departmanının Türkçe metin denetiminde (2026-10-13) bulunan, hesap bölme ekranındaki İngilizce ve teknik sızıntıların kapatılması (`docs/UI_STYLE_GUIDE.md` §3 ve §4).

## Owned surface

- `plan/v1/remediation/V1-RMD-513-bill-split-screen-text-leaks.md`
- `evidence/V1-RMD-513/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yükleniyor metnindeki "Authoritative" kelimesinin Türkçeleştirilmesi; ikincil etiket yokken ham "Seat/Person" sahip türünün Türkçe karşılıkla değiştirilmesi; ekranda gösterilen satır sürüm numarasının (`v{billRowVersion}`) kaldırılması; ham sunucu mesajının ve HTTP/regex tahminli çakışma tespitinin durum koduna dayalı sabit Türkçe metinle değiştirilmesi.

## Out of scope

- Terim sözlüğü ve diğer ekranların metinleri.

## Dependencies

- V1-RMD-510

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-513/` altındadır.

## Handoff

- None
