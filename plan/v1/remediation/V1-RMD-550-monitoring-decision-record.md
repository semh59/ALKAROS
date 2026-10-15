# V1-RMD-550 - İzleme sistemi karar kaydı

- Task ID: V1-RMD-550
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Yeni hata/performans izleme sisteminin mevcut uyarı sistemi ve planlı V15-OBS-002, V15-OBS-003, V15-NOT-001 ile ilişkisi, Telegram içerik politikası ve izleme verisi için KVKK değerlendirmesi karar olarak kayda geçer.

## Owned surface

- `plan/v1/remediation/V1-RMD-550-monitoring-decision-record.md`
- `evidence/V1-RMD-550/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Karar 1: hata olayları mevcut uyarı çatısını (AlertFoundation) besler; parmak izi uyarının tekilleştirme anahtarıdır; ayrı bir uyarı yaşam döngüsü kurulmaz.
- Karar 2: yönetim ekranı mevcut Sistem bölümünü genişletir; Telegram, V15-NOT-001 bildirim kanalı sözleşmesinin bir kanalıdır.
- Karar 3 (PO 2026-10-15): Telegram'a yalnız sabit künye gider (kategori, sayı, saat); hata metni, müşteri, tutar, ürün adı gitmez.
- Karar 4 (PO 2026-10-15): ağır parçalar (Z-skor şiddeti, zaman çizelgesi, derin veritabanı istatistiği, gerileme tespiti) ilk dalgada kalır.
- V15-OBS-002, V15-OBS-003 ve V15-NOT-001 için örtüşme tablosu: hangi madde hangi göreve devredildi; devredilenler bu kayıtlarda not edilir.
- KVKK: çalışan davranışı izlemesi için aydınlatma metni ve hukuki görüş ihtiyacı açık madde olarak kayda geçer (hukuki görüş doğrulanmadı).

## Out of scope

- Yok.

## Dependencies

- None

## Acceptance evidence

1. Kayıt, her karar için gerekçe ve dayanak içerir; örtüşme tablosu üç V15 görevini ve V1-ALT-001'i kapsar.
2. Çıktılar `evidence/V1-RMD-550/` altındadır.

## Handoff

- None
