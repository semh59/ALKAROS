# V1-RMD-539 - Yönetim ekranı: sistem sağlığı ve hatalar

- Task ID: V1-RMD-539
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Müdür, mevcut Sistem bölümünde canlı hata listesini görür; süzer, ayrıntıya ve tekrar sayısına bakar, hatayı onaylar veya çözüldü işaretler.

## Owned surface

- `plan/v1/remediation/V1-RMD-539-error-screen.md`
- `evidence/V1-RMD-539/**`
- `src/Clients/PosTerminal/src/features/management-error-events/**`
- `src/Host/Observability/ErrorEventEndpoints/**`
- `tests/Host/Observability/ErrorEventEndpoints/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Liste, katman/şiddet/durum süzgeci, ayrıntı, onayla, çözüldü, dedektör sağlığı şeridi, bildirim kanalı durumu.
- Sabit hata türü kataloğu ve Türkçe sözlük; ham hata metni basılmaz; yetki: yönetici.
- Uyarı gerekçesindeki kaçışsız JSON hatası bu yolda düzeltilir.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533
- V1-RMD-536
- V1-RMD-538

## Acceptance evidence

1. 10.000 olayda liste ilk sayfa 300 ms altında.
2. Ekran metinlerinde İngilizce sızıntı taraması 0.
3. Çıktılar `evidence/V1-RMD-539/` altındadır.

## Handoff

- None
