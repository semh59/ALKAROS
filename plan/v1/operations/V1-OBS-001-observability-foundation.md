# V1-OBS-001 - Implement observability correlation foundation

- Task ID: V1-OBS-001
- Status: Done
- Assignee: Antigravity-v1-obs-001
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28

## Goal

V1 flow'ları için structured event contract, correlation/request ID ve bounded status-audit persistence eklemek.

## Owned surface

- `src/Modules/Observability/Foundation/**`, `tests/Modules/Observability/Foundation/**`,
  `database/migrations/V1/V1-OBS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Onaylanan politika tarafından yönlendirilen korelasyon yayılımı, temel sağlık status kataloğu, redaksiyon kancası ve
  kalıcı saklama politikası kimliği.

## Out of scope

- Tam alert kuralları, ölçüm arka ucu ve hassas yük şifrelemesi.

## Dependencies

- V1-FND-001
- V0-DAT-002
- V0-CMP-003

## Deliverables

- `src/Modules/Observability/Foundation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Order yazdırma kuyruğuna gönderme ID tek korelasyonla izlenebilir; sağlık status standarttır; gizli test işaretçisi
  senaryosu ilgili testlerle doğrulanır (ilgili test: `tests/Modules/Observability/Foundation/**`; çalıştırma komutu,
  komut çıktısı ve exit code evidence'e eklenir); onaylanmış bir saklama politikası kimliği olmadan ısrar reddedilir.
- **2026-09-23 kabul edilen risk + tamamlayıcı önlem** (Semih onaylı, V15
  Faz 0/1 audit turunda bulunan bir bulgunun takibi): `V15-SUP-001`'in
  `SecretPatternScanner`'ı yalnız kart-benzeri rakam dizilerini ve
  24+ karakterlik token/JWT-benzeri değerleri yakalıyor — kısa, insan
  tarafından yazılmış bir şifre/PIN/OTP, yanlış/etiketsiz bir anahtar
  altında durursa bu değer-bazlı katmandan geçebilir. Bu, regex ile kısa
  string'leri "şifre" diye tahmin etmenin yanlış-pozitif maliyeti çok
  yüksek olduğu için BİLİNÇLİ olarak düzeltilmedi — birincil savunma
  (`ObservabilityRedactionHook`'un anahtar-bazlı kontrolü) alanlar doğru
  adlandırıldığında (`password`, `secret`, `pin` vb.) uzunluktan bağımsız
  zaten yakalıyor. Tamamlayıcı, sıfır yanlış-pozitif riskli önlem olarak
  `SensitiveKeys` listesi genişletildi: `passwd`, `passphrase`,
  `one_time_password`, `otp`, `security_code`, `verification_code`,
  `recovery_code`. Kalan artık kabul edilen risk yalnızca "kısa bir
  sır TAMAMEN farklı/tahmin edilemeyen bir anahtar adı altında" durumu —
  bu, mevcut mimaride (serbest-formatlı `BeforeStateJson`/`AfterStateJson`/
  `MetadataJson`) teorik olarak mümkün ama regex ile pratik bir çözümü yok.
  7 yeni test (`IsSensitiveKeyIdentifiesProtectedFields`), 29/29 yeşil;
  regresyon: `Support.DiagnosticBundle` 22/22, `Observability.
  StructuredLogging` 29/29, `Audit.EventStore` 22/22.

## Handoff

- V15-OBS-001
- V15-OBS-002
- V15-OBS-003
