# qa-veritabani — V1-RMD-597 ham rapor özeti

101/231 (78 göç dosyası + 23 SQL kaynağı). Test PG 55432'de geçici DB ile çalıştırıldı (canlı yığına dokunulmadı); DB adı tmp/qa/V1-RMD-597/qa-veritabani/dbname.txt (silme kararı Semih'te). Zincir: disk=order.json=Manifest=193; 39 down round-trip şema farkı yok; tohum idempotent (40/6/89). Kritik/Yüksek yok.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Orta | PostgresUserStore.cs:232-240 (Çalıştırıldı) | ForceUnlock pin_* sütunlarını sıfırlamaz; PIN kilidi kalır. |
| 2 | Orta | 045 ck_authorization_grants_resolution (Çalıştırıldı) | DB, onaylayanı=talep eden olan manual+granted satırı ve onaylayansız manual+granted kabul eder; koruma yalnız uygulamada. |
| 3 | Orta | 045 trigger (Çalıştırıldı) | Trigger requester_role_code, subject_type, subject_id, subject_serving_user_id değişimini engellemez. |
| 4 | Orta | 047,051,107 (Çalıştırıldı) | offline_authority_budgets.user_id/session_id ve revoked_by_user_id FK'siz. |
| 5 | Orta | data-housekeeping.md:12-15 | denial_events, offline bütçe/satır/replay, süresi dolmuş delegasyon temizlenmez; grants/tightenings saklama politikası belgesi yok. |
| 6 | Düşük | 050; EXPLAIN Seq Scan | session_id indeksi yok; GetBySessionAsync ölü. |
| 7 | Düşük | PostgresDeviceSessionRepository.cs:183,211-219 | operation_id PK global: oturumlar arası çakışma sessiz yutulur; ölü API. |
| 8 | Düşük | order.json:12 phaseBRange 139 vs PhaseBMax 194 | Metadata sapması. |
| 9 | Düşük | 074 | Identity göçü inbox_messages indeksi oluşturur (sahiplik sınırı). |
| 10 | Düşük | PermissionSplitDatabase.cs:35-54 | 089,090,091,093,132,074,094,107 için göç testi yok. |
| 11 | Düşük | PostgresRoleRepository.cs:296,350 (Çalıştırıldı) | ORDER BY display_name Türkçe sıralama değil (en_US collation). |
| 12 | Düşük | 005 ux_users_username (Çalıştırıldı) | u1 ve U1 birlikte eklenir; I/İ normalizasyonu yok. |
| 13 | Düşük | (Çalıştırıldı) | Kod sütunlarında FK yok; sayaçlarda CHECK >=0 yok; email unique yok. |
| 14 | Düşük | Program.cs:728-732 | KVKK anonimleştirme pin_hash/pin sayaçlarını temizlemez. |
| 15 | Düşük | 107 NOT VALID yok; IF NOT EXISTS yok; 050.down | Geri alma sınırlı. |
| 16 | Düşük | gen_random_uuid() tohumlar | Ortamlar arası farklı id. |
| 17 | Düşük | ManagementSessionLookup.cs:21-28 | Her istek UPDATE; `manager:` önek sözleşmesi DB'de CHECK'siz. |
| 18 | Düşük | EXPLAIN Q2,Q5 | Temizlik DELETE'i tarama; bazı FK'lerde indeks yok; ix_authorization_policies_permission fazlalık (Çıkarım). |

Temiz: manifest uyumu, round-trip, tohum tekrarı, rol eşleşmesi (26 kod), atomik sayaçlar, planlar (granted_rate, auto_window, device_sessions_user), parametreli SQL, LIMIT korumaları, denial_events RESTRICT.
Doğrulanamayan: 005.down (başka modül FK), 170 göçü (psql ile), üretim collation, gerçek yarış koşusu, test varlığı iddiaları yalnız dosya adı.
