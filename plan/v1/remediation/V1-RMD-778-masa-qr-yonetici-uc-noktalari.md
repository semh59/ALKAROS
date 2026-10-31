# V1-RMD-778 - Masa QR kodunu yönetici üretsin, yenilesin, iptal etsin (sunucu)

- Task ID: V1-RMD-778
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V12-QRS-001, Yüksek), Semih kararlarıyla. `TableTokenService.IssueAsync/RotateAsync/RevokeAsync` üretimde hiçbir yerden çağrılmıyor; `table_tokens` tablosuna yalnız repository ve testler yazıyor. Hiç masa token'ı yok, bu yüzden müşteri `/api/v1/qr/sessions` adımını geçemiyor ve QR kanalı çalışmıyor. V12-CWB-001 ve V12-QRO-004 bunu "personel için HTTP yüzeyi ayrı görev" diye ertelemiş, o görev açılmamış.

Kararlar:

1. Masa QR'ını yalnız yönetici yönetir: üret, yenile, iptal et. Mevcut `integrations.manage` izni kullanılır (yalnız yönetici rolünde; röle bağlantı ayarlarıyla aynı kademe). Yeni izin, migration ve rol katalog değişikliği yoktur.
2. Uç noktalar `/api/v1/management/qr-tables` altındadır, `/api/v1/qr` altında değildir: `/api/v1/qr` röle (genel) origin'inden erişilebilir, yönetim uçları erişilemez. Kimlik doğrulama `alkaros.manager` çerezi (yalnız yönetici, süpervizör değil) ve `integrations.manage` izniyle yapılır.
3. Masa QR'ı sabit basılı etikettir: otomatik yenileme yok, masa kapanışında token değişmez. Yenileme yalnız yöneticinin elle yaptığı işlemdir. Ham token saklanmaz; QR yalnız üretme veya yenileme anında yanıtla döner. Kaybolursa yenile ile yeni QR basılır, eskisi hemen geçersiz olur.
4. Süre: basılı sabit QR 4 saatte ölmemeli. `TableTokenService.DefaultLifetime` 4 saatten 10 yıla çıkar. `expires_at` NOT NULL kalır, şema ve doğrulama (`ValidateAsync`, `IsExpired`) değişmez; kullanılan "süresiz" budur. İptal edilene kadar geçerlidir. Müşteri oturumu 4 saat kalır (V12-QRS-003). `docs/architecture/qr-relay-topology.md` kural 2 amendment ile güncellenir.
5. Düzeltme: `IssueAsync` süresi dolmuş ama iptal edilmemiş token'ı "aktif" sayıp `TableTokenAlreadyActiveException` atıyordu (`GetActiveForTableAsync` yalnız `revoked_at IS NULL` bakar). Süresi dolmuş token artık tek işlemde (`RotateAsync`, neden `expired`) yenisiyle değişir; süresi dolmamış aktif token için `AlreadyActive` aynen kalır. `RevokeAsync` boş nedeni doğrulamıyordu; artık `ArgumentException` atar.
6. Kabul edilmiş risk (kod yok, V12-QRS-003 kapsamı): müşteri oturumu token'a bağlanmaz ve token iptalinde oturum kapatılmaz. QR siparişi "bekleyen" açılır ve personel kabulüyle siparişe döner (V12-QRO-001/003); garson onayı riski kapatır. Kayıt `docs/architecture/qr-relay-topology.md` 2c bölümünde ve RESULT.md'dedir (V12-QRS-003, V12-CWB-001 ve V12-QRO-004 görev dosyaları kanıt klasörleri olmadığı için kapanış kapısınca değiştirilemez; "ayrı görev" ve "4 saat" ifadeleri orada tarihsel kalır).

Uç noktalar (hepsi yönetici):

- `GET /api/v1/management/qr-tables`: masa listesi (masa no, bölge adı, aktif mi) ve her masa için QR var mı, ne zaman üretildi. Ham token asla dönmez.
- `POST /api/v1/management/qr-tables/{tableId}/issue`: ilk QR'ı üretir. `POST .../rotate`: yeniler. `POST .../revoke`: iptal eder (neden sabit ve yönetici kimliğini taşır).
- Üret ve yenile `https://{röle ana makine adı}/qr/?t={token}` adresini döner. Röle bağlantısı etkinleştirilmemişse token üretilmeden önce 409 `RELAY_NOT_READY` döner (kullanılamaz QR basılmaz).
- Tekrar-anahtarı yoktur: yanıt ham token taşıdığı için saklı tekrar yanıtı token'ı kalıcılaştırırdı; üret ve yenile doğal olarak tekrarlanamaz (ikinci üretme 409). `idempotency_allowlist.json` bunu gerekçesiyle kaydeder.

## Owned surface

- `evidence/V1-RMD-778/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/RelaySettings/TableQrManagementEndpoints.cs (V12-QRT-003 dizin ön ekinin altında, yeni dosya) — yönetici uç noktaları, kimlik süzgeci, sözleşmeler.
  - src/Host/Experience/RelaySettings/RelaySettingsEndpoints.cs (V12-QRT-003) — yeni uç noktaların kayıt ve eşleme çağrısı.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata alanı, Türkçe mesajlar.
  - src/Modules/QrOrdering/TokenLifecycle/ (V12-QRS-001) — 10 yıllık süre, süresi dolmuş token ile Issue, boş neden doğrulaması, aktif token listesi.
  - src/Modules/QrOrdering/CustomerSession/CustomerSessionService.cs (V12-QRS-003) — yalnız süre yorumu.
  - src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs (V12-CWB-001) — yalnız süre yorumu.
  - docs/architecture/qr-relay-topology.md (V0-ARC-009) — kural 2 amendment.
  - tests/Modules/QrOrdering/TokenLifecycle/ (V12-QRS-001) — süre, Issue düzeltmesi, liste, boş neden testleri.
  - tests/Host/Experience/RelaySettings/ (V12-QRT-003) — uç nokta ve uçtan uca QR akışı testleri.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-008) — RelaySettings için Tables kenarı.
  - tests/Architecture/ApiConventions/idempotency_allowlist.json (V1-RMD-430) — üç yönetim ucunun kaydı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `TableTokenService`: süre 10 yıl, `IssueAsync` süresi dolmuş token'ı değiştirir, `RevokeAsync` neden doğrular; `ITableTokenRepository.ListActiveAsync`.
2. Yönetici uç noktaları (liste, üret, yenile, iptal), `integrations.manage`, `/api/v1/management/qr-tables`.
3. Röle hazır değilse 409 `RELAY_NOT_READY`; masa yoksa 404 `TABLE_NOT_FOUND`; QR yoksa 404 `QR_NOT_FOUND`; QR varsa 409 `QR_ALREADY_EXISTS`.
4. Uçtan uca kanıt: üretilen adres `/api/v1/qr/sessions` ile oturuma çevrilir; yenilemeden sonra eski token `QR_TOKEN_REVOKED` döner.
5. Bekleyen QR siparişinin personel kabulü yolunun çalıştığının test kanıtı (RESULT.md).
6. Süre dokümanı ve yorumlar, kabul edilmiş risk kaydı.

## Out of scope

- Ekran, QR çizimi ve yazdırma (V1-RMD-779).
- Müşteri oturumunu token'a bağlama, iptalde oturum kapatma (V12-QRS-003, kabul edilmiş risk).
- Yeni izin, migration, şema değişikliği, otomatik yenileme, denetim olayı deposu.

## Dependencies

- V12-QRS-001
- V12-QRT-003

## Acceptance evidence

1. TokenLifecycle testleri gerçek Postgres'te yeşil: 10 yıllık süre, süresi dolmuş token ile Issue yenisini verir (eski `expired` ile iptal), aktif token ile Issue `AlreadyActive`, boş nedenle Revoke `ArgumentException`, `ListActiveAsync`.
2. RelaySettings HTTP testleri yeşil: oturumsuz, kasiyer çerezi ve süpervizör yönetim oturumu 401; `integrations.manage` izni olmayan yönetim oturumu her rotada 403, yönetici liste/üret/yenile/iptal, sistem masası listede yok, röle hazır değilken 409 ve token üretilmemiş, ham token liste yanıtında yok; üretilen adres `/api/v1/qr/sessions` ile 200, yenilemeden sonra eski token 401 `QR_TOKEN_REVOKED`.
3. Mutasyon: `IssueAsync` süresi dolmuş dalını kaldırmak ve `DefaultLifetime`'ı 4 saate döndürmek ilgili testi kırmızıya çevirir; kaynak `cp`/`cmp` ile geri alınır.
4. QrOrdering, Host (RelaySettings, Orders.Confirmation), ApiConventions ve ModuleBoundaries testleri exit 0.
5. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0; iki bağımsız inceleme bulguları giderilmiş.

## Handoff

- None
