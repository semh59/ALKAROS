# V1-RMD-539 - Yönetim ekranı: sistem sağlığı ve hatalar

- Task ID: V1-RMD-539
- Status: InProgress
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
- `src/Host/Observability/ErrorEventEndpoints/**`
- `tests/Host/Observability/ErrorEventEndpoints/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız uç nokta kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yönetici uç noktaları: liste (katman/şiddet/durum süzgeci, sayfalı), ayrıntı ve son oluşlar, onayla, çözüldü; bildirim kanalı durumu (açık/kapalı). Ekran V1-RMD-565 kapsamına taşındı.
- Sabit hata türü kataloğu ve Türkçe sözlük; ham hata metni basılmaz; yetki: yönetici.
- Uyarı gerekçesindeki kaçışsız JSON hatası bu yolda düzeltilir.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533
- V1-RMD-536
- V1-RMD-538

## Acceptance evidence

1. 10.000 olayda liste ilk sayfa 300 ms altında; yetkisiz çağrı reddedilir.
2. Onayla ve çözüldü durumu değiştirir ve olayı denetim izine bırakır.
3. Çıktılar `evidence/V1-RMD-539/` altındadır.

## Handoff

- None
