# V1-RMD-534 - Sunucu dedektörleri

- Task ID: V1-RMD-534
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Yakalanmamış istisna, yavaş istek, yavaş sorgu, bağlantı havuzu, kilit/kilitlenme ve kaçan arka plan işi hata olayı olarak yakalanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-534-server-detectors.md`
- `evidence/V1-RMD-534/**`
- `src/Host/Observability/ServerDetectors/**`
- `tests/Host/Observability/ServerDetectors/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız izleme kaydı ve istek süresi ara katmanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İstisna yakalayıcı yalnız 5xx sunucu hatalarını KRİTİK sayar; 4xx istemci hataları olay üretmez.
- Veritabanı hata kodları (55P03, 40P01 Error; 53300, 57P03, 08006 Critical) ve yavaş istek eşiği (ayarlanabilir, varsayılan 2000 ms; yol şablonu kullanılır, ham yol değil).
- Olay yazıcısı Host'ta izleme havuzuyla bağlanır; saklama silme işi zamanlanır ve denetim kaydı yazar.
- Havuz doluluğu, kaçan arka plan işi, N+1, yavaş sorgu ve sorgu planı V1-RMD-558'e taşındı.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. 500 durumlu bir istek tam 1 KRİTİK olay; 404 ve 409 sıfır olay üretir.
2. Yapay 10 sn kilit bekleme 55P03 olayı üretir.
3. Yavaş istek eşiğini aşan istek 1 Warning olay üretir, eşik altı üretmez.
4. Olay yazıcısı Host'ta izleme havuzuyla bağlanır; hata olayı tabloları saklama kataloğuna (V1-RMD-549 `TelemetryRetention`) eklenir, silme işi zamanlanır ve her çalışma denetim kaydı yazar.
5. Çıktılar `evidence/V1-RMD-534/` altındadır.

## Handoff

- None
