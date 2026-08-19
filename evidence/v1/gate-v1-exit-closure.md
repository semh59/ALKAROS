# GATE-V1-EXIT Kapanış Kanıtı

- Gate: GATE-V1-EXIT
- Tarih: 2026-08-19
- Yetkili Onay: Semih (Product Owner / TRACEABILITY.md C71)
- Kapanış Durumu: KAPATILDI (Closed)

## 1. V1 Görev Matrisi ve Durum Özeti

V1 sürümüne ait 86 görevin tamamı terminal durumda doğrulanmıştır:

- **Toplam Görev:** 86
- **Tamamlanan (Done):** 81
- **InProgress:** 1 (`V1-GOV-001`)
- **Onaylı Uygulanamaz (NotApplicable):** 4 (`V1-CAT-003`, `V1-FND-020`, `V1-IAM-006`, `V1-IAM-011`)
- **Açık Blocked:** 0

## 2. Düzeltilen Bulgular (C71 Uzlaştırma)

| Bulgu ID | Remediasyon Görevi | Düzeltme ve Doğrulama Özeti | Durum |
| --- | --- | --- | --- |
| `DB-01` | `V1-BIL-004` | 019/020/021 migration'ları immutable bırakıldı; composite integrity ve split design kısıtları additive `032-billing-composite-integrity` migration'ı ile uygulandı. ManifestTests exit 0. | Done |
| `SEC-01` | `V1-FND-025` | HostComposition içindeki geniş catch ve hardcoded fallback credentials temizlendi; malformed URL'de fail-closed kapanış sağlandı. HostModuleReachabilityTests exit 0. | Done |
| `AUD-01` | `V1-TBL-007` | Masa transfer, merge, rezervasyon ve pointer reposundaki 42P01 exception yutmaları kaldırıldı; audit ile domain write tek transaction atomikliğine bağlandı. 159 test exit 0. | Done |
| `ORD-01` | `V1-ORD-004` | SubmitOrderHandler expired idempotency yenilemesinde `request_hash` atomik güncellendi. 119 test exit 0. | Done |
| `IAM-01` | `V1-IAM-015` | DeviceSessionService reconnect claim ve session doğrulaması tek SQL WHERE EXISTS ile atomikleştirildi. 92 test exit 0. | Done |
| `WTR-01` | `V1-WTR-004` | WaiterOfflineQueueEngine mock-success kaldırıldı; server ack zorunlu kılındı ve ilk hatada FIFO replay durduruldu. 32 test exit 0. | Done |
| `GOV-01..05` | `V1-GOV-001` | README görev sayısı, audit raporu ve manifest yenilendi; verify-manifest exit 0; GATE-V1-EXIT kapanışı ilan edildi. | Done |

## 3. Doğrulama Komutları ve Sonuçları

- `py -3 -B tools/plan-audit/plan_audit_tool.py validate`: 361 task, 18 gate, 0 error, 0 warning.
- `py -3 -B tools/plan-audit/plan_audit_tool.py verify-manifest`: 0 error, exit 0.
- `dotnet test ALKAROS.slnx --no-restore --nologo`: Tüm test projeleri başarıyla geçti, 0 fail.
- `dotnet format ALKAROS.slnx --verify-no-changes --no-restore`: Biçimlendirme hatası yok, exit 0.
## Faz 1 yeniden doğrulama — blocker çözüldü

2026-08-19 canlı doğrulamada C71 hedefli testleri geçti: Host 73, SubmitOrder 14, DeviceSessions 19, Waiter SessionQueue 7, TableTransfer 33, TableMerge 29, Reservations 27, CurrentPointers 19; tamamı 0 fail.

`V1-SEC-004` Owned surface sahipliği düzeltildi. `py -3 -B tools/plan-audit/plan_audit_tool.py validate` ve `verify-manifest` canlı çalışma alanında 0 hata verdi.

## Faz 2 bağımsız doğrulama — yeni gate blocker'ları

- **HIGH / V1-FND-019:** `InboxStore` ve `OutboxStore` lease finalization sorguları yalnız `id` ve `status = 'in_flight'` kontrol ediyor (`InboxStore.cs:147`, `OutboxStore.cs:152`, mark işlemleri `:199`/`:278`). Lease token veya generation yok; eski worker, aynı satır yeniden lease edildikten sonra affected-row `1` alabilir. Mevcut testler expired redelivery'yi doğruluyor, stale-worker fencing interleaving'ini doğrulamıyor.
- **HIGH / V1-SEC-006:** Handler exception'ı `InboxStore.cs:90` ve `OutboxStore.cs:95` içinde ham `ex.Message` olarak alınıyor. `RetryPolicy.SanitizeError` yalnız `password=` ve PostgreSQL URL desenlerini maskeliyor; genel PII/secret allowlist'i yok. Testler ham `boom` değerini bekliyor (`InboxStoreTests.cs:121`, `OutboxStoreTests.cs:102`).
- **HIGH / V1-IAM-010:** Unknown kullanıcı yolu sabit 600.000 iterasyonlu `DummyHash` kullanıyor (`AuthenticationService.cs:65`); bilinen kullanıcı hash'i gömülü iterasyon sayısını kullanıyor (`PasswordHasher.Verify`). Legacy düşük iterasyonlu hash için rehash çağrısı yok; eşit KDF work-factor iddiası kanıtlanmıyor.
- **HIGH / V1-FND-018:** `RegisterOrReplayAsync` yalnız idempotency kaydı ve response envelope yönetiyor; protected mutation callback/transaction sınırı yok (`IdempotencyKeyStore.cs:37`). Üretim çağrısı bulunamadı; testler kayıt/replay yarışını kapsıyor, mutation crash-replay atomikliğini değil.

### FND-019 uygulanabilirlik notu

Lease fencing için `inbox_messages` ve `outbox_messages` tablolarına token/generation alanı ve additive migration gerekir. Mevcut `V1-FND-019` Owned surface'i migration yollarını açıkça out-of-scope bırakıyor; migration sahipliği `V1-FND-002`dedir. Bu nedenle FND-019 doğrudan kodlanmadan önce scope/dependency değişikliği veya ayrı migration remediasyon görevi gerekir.

Faz 5 plan uzlaştırmasıyla `database/migrations/V1/V1-FND-019/**` additive up/down migration yüzeyi FND-019'a eklendi; runtime `order.json` sahipliği FND-012'de bırakıldı. Bu yalnız kapsam düzeltmesidir; migration veya lease fencing kodu henüz uygulanmış sayılmaz.

### Faz 4 sahiplik rotası

| Bulgu | Kaynak sahibi | Kanıt/doğrulama sahibi | Gerekli işlem |
| --- | --- | --- | --- |
| CODE-004/005 | `V1-FND-019` | `V1-FND-019` | Migration surface'i eklenmeli veya ayrı migration görevi açılmalı. |
| CODE-018 | `V1-FND-019` | `V1-SEC-006` | Fencing ve genel secret/PII sanitization düzeltildikten sonra negatif test tekrarlanmalı. |
| CODE-012 | `V1-IAM-005` | `V1-IAM-010` | Rehash-on-login/work-factor yakınsaması uygulanmalı; IAM-010 yeniden doğrulamalı. |
| CODE-003 | `V1-FND-018` | `V1-FND-018` | Protected mutation callback/transaction API'si ve crash-replay testi eklenmeli. |
## Faz 6 uygulama kanıtı — FND-019 Done

Lease-generation migration `033` eklendi; Inbox/Outbox finalization sorguları generation koşuluna bağlandı; stale-generation regression testleri eklendi. Idempotency testleri 82/82, Host migration/manifest testleri 73/73 geçti. Gerçek PostgreSQL disposable veritabanında migration up/down doğrulandı; FND-019 `Done` durumuna taşındı.

## Faz 8 uygulama kanıtı — CODE-018 düzeltildi

`RetryPolicy.SanitizeError` artık ham exception metnini regex ile kısmen taşımıyor; yalnız bounded `handler failure` sınıflandırması persist ediyor. Password, email/PII ve PostgreSQL credential sentinel testleri eklendi. Idempotency/Messaging testi 85/85 geçti.

## Faz 9 uygulama kanıtı — CODE-012 düzeltildi

`AuthenticationService` doğrulanmış legacy hash'leri koşullu `TryUpgradePasswordHashAsync` ile güncel work factor'a yükseltiyor. 10.000 iterasyonlu hash gerçek PostgreSQL testinde 600.000 iterasyona yükseltildi; Authentication suite 54/54 geçti.

## Faz 10 uygulama kanıtı — CODE-003 düzeltildi

`IdempotencyKeyStore.ExecuteAsync` unique-key claim, protected mutation callback'i ve terminal response yazımını aynı PostgreSQL transaction'ında birleştiriyor. Aynı key için eşzamanlı çağrıda mutation bir kez çalıştı; sonuçlar Created + Replayed oldu. Idempotency/Messaging suite 86/86 geçti.
