# V1-RMD-587 kanıtı

Günlük saklama işi artık şu tabloları da siler: `operations.system_health_snapshots`, `observability.notification_queue`, `observability.notification_suppressions` (30 gün) ve `observability.health_checks` (her satır kendi onaylı saklama kimliğine göre 7/30/90/365 gün; tanımsız kimlik 365 gün).

- Gerçek Postgres testi (`test-runs.log`): ServerDetectors 11/11; her tabloda eski satır silinir, yeni satır kalır; sınırdan 1 saat önce/sonra satırlar, küçük harf ve boşluklu kimlik, tanımsız kimlik ve 5000'i aşan toplu silme (5007 satır, birden çok parti) kapsanır; denetim kaydında tablo adları ve silinen adetler görünür. Komşu paketler (InteractionTelemetry 16, AlertFoundation 12, Foundation 29) geçti.
- Mutasyon: 13 mutant, 13'ü öldü, sağ kalan yok (`mutations.log`, betik `mutate.py`). İlk koşuda iki sağ kalan (7 ve 90 günlük sınır) vardı; testteki satırlar sınırdan 1 saat önce/sonraya çekilerek kapatıldı.
- Kapsam dışı bırakıldı (PO kararı gerektirir): `observability.alerts` ve `alert_events` eklenir-silinmez denetim izi olarak tasarlandı; süresiz büyürler (büyüme hızı ölçülmedi).
- Gerçek Docker yığınında günlük işin çalıştırılması denenmedi: doğrulanamadı (iş 24 saatte bir çalışır; testte `RunOnceAsync` doğrudan çağrıldı).
