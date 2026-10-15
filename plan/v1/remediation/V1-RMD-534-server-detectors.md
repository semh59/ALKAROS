# V1-RMD-534 - Sunucu dedektörleri

- Task ID: V1-RMD-534
- Status: Planned
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
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İstisna yakalayıcı yalnız 5xx sunucu hatalarını KRİTİK sayar; 4xx istemci hataları şiddetsiz sayaçtır.
- Yavaş istek ve sorgu eşiği, havuz > %85, kilit ve kilitlenme hata kodları (55P03, 40P01), kaçan zamanlanmış iş, N+1 (istek başına 20 sorgu), veritabanı hata kodu sınıflaması.
- Sorgu planı (EXPLAIN) yalnız SELECT için, sınırlı sürede alınır; dış çağrı adresleri temizlenir; izlemenin kendi yazımları ayıklanır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. 500 durumlu bir istek tam 1 KRİTİK olay; 404 ve 409 sıfır olay üretir.
2. Yapay 10 sn kilit bekleme 55P03 olayı üretir.
3. İzleme yazımları yavaş-sorgu olayı üretmez (öz kirlenme testi).
4. Olay yazıcısı Host'ta izleme havuzuyla bağlanır; hata olayı tabloları saklama kataloğuna (V1-RMD-549 `TelemetryRetention`) eklenir, silme işi zamanlanır ve her çalışma denetim kaydı yazar.
5. Çıktılar `evidence/V1-RMD-534/` altındadır.

## Handoff

- None
