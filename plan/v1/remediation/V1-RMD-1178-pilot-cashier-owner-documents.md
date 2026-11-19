# V1-RMD-1178 - Pilot belgeleri: kasiyer hızlı başlangıç, elle satış defteri, pilot günü kontrol listesi, geri bildirim formu

- Task ID: V1-RMD-1178
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Existing

## Goal

Gerçek kasiyerle pilota başlanabilmesi için `docs/operations/pilot/` altında, kod bilmeyen kasiyer ve işletme sahibi için çok sade Türkçe, yazdırılabilir (her biri en fazla 1-2 sayfa) dört belge yazılır: kasiyer ilk gün rehberi (gerçek ekran ve düğme metinleriyle), elle satış defteri baskı şablonu (mali müşavir onayına tabi uyarısıyla), sahibin sabah açılış ve akşam kapanış kontrol listesi, pilot geri bildirim formu. Belgelerdeki her ekran adı, düğme metni ve sayı koddan ya da mevcut belgelerden doğrulanır; hukuki görüş verilmez.

## Owned surface

- `evidence/V1-RMD-1178/**`
- `docs/operations/pilot/kasiyer-hizli-baslangic.md`
- `docs/operations/pilot/elle-satis-defteri.md`
- `docs/operations/pilot/pilot-gunu-kontrol-listesi.md`
- `docs/operations/pilot/pilot-geri-bildirim-formu.md`
- `docs/README.md`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/operations/production-go-live-checklist.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dört pilot belgesi ve `docs/README.md` belge dizini (pilot belgelerine ve mevcut işletme belgelerine bağlantı)
2. `production-go-live-checklist.md` bölüm 6 ve "Companion docs" için pilot belgelerine bağlantı satırı
3. İki bağımsız qa incelemesi (qa-dokuman-bagimlilik, qa-ux-tasarim)
4. plan-audit validate ve consistency-audit temiz

## Out of scope

- Kod, ekran metni, şema; elle belge girişi ekranı; mali müşavir görüşü; kart ile tahsilat akışı (kart düğmesi yalnız kart terminali tanımlıysa görünür)

## Dependencies

- V1-RMD-1131
- V1-RMD-1169
- V1-RMD-1177

## Acceptance evidence

1. Her ekran adı, düğme metni, yol ve sayının koddaki ya da belgedeki kaynağını gösteren doğrulama listesi
2. plan-audit ve consistency-audit çıktıları
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
