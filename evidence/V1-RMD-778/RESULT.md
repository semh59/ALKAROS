# V1-RMD-778 sonuç

Yönetici masa QR'ını üretir, yeniler, iptal eder; üretilen adres müşteri tarafındaki mevcut `/api/v1/qr/sessions` yoluyla gerçekten oturuma çevriliyor.

## Yapılanlar
- `GET/POST /api/v1/management/qr-tables` (liste, `{tableId}/issue|rotate|revoke`): `alkaros.manager` çerezi (süpervizör hariç) + `integrations.manage`. `/api/v1/qr` altında değil: o önek röle origin'inden erişilebilir, yönetim uçları erişilemez (Host origin kapısı `/api/v1/qr` dışı API'yi röle origin'inde 404'ler).
- İzin seçimi: yeni izin eklenmedi (migration + `PermissionSplitDatabase` listesi gerekirdi). `integrations.manage` yalnız yönetici rolünde ve röle bağlantı ayarlarıyla aynı kademe; manager çerezi girişte `catalog.manage` olan kullanıcıya veriliyor.
- Süre: `DefaultLifetime` 4 saat -> 10 yıl (`expires_at` NOT NULL, şema ve `ValidateAsync` değişmedi; "iptal edilene kadar" bunun karşılığı). Müşteri oturumu 4 saat. Neden: süresiz için NULL gerekirdi, bu migration ve doğrulama değişikliği demek; 10 yıl en küçük doğru çözüm. `qr-relay-topology.md` kural 2 + 2c amendment.
- `IssueAsync` düzeltmesi: süresi dolmuş ama iptal edilmemiş token tek işlemde `expired` nedeniyle yenisiyle değişir; süresi dolmamışta `AlreadyActive` aynen. `RevokeAsync` boş nedeni reddeder (önceki bulgu).
- Üret/yenile önce masayı ve röle ana makine adını (`RelayTunnelInfo.Hostname`) denetler: yoksa 404 `TABLE_NOT_FOUND` / 409 `RELAY_NOT_READY` ve hiç token üretilmez. Dönen adres `https://{host}/qr/?t={token}` (token `Uri.EscapeDataString`; müşteri sayfası `?t=` okuyor).
- Liste ham token taşımaz; sistem "KASA-1" satırı (`...0001`) listelenmez ve QR üretilemez.
- Idempotency: üç POST `withoutIdempotencyKey` listesinde, gerekçeli (yanıt ham token taşıdığı için saklı tekrar yanıtı token'ı kalıcılaştırırdı).

## Kabul edilmiş risk (Semih kararı, kod yok)
Müşteri oturumu token'a bağlanmaz, iptalde oturum kapanmaz. QR siparişi "bekleyen" açılır, personel onayı siparişe çevirir. Kayıt: `qr-relay-topology.md` 2c ve bu dosya (V12-QRS-003 md'si Done ve kanıt klasörü olmadığından kapanış kapısı değişikliğe izin vermiyor).

## Onay yolu doğrulaması (bekleyen QR siparişi -> personel kabulü)
Çalışıyor: WaiterPwa `js/sheets/pending-orders.js` `GET /orders/pending` ve `POST /orders/{id}/accept|reject` çağırıyor; sunucu uçları `OrderManagementEndpoints.cs` (`/pending`, `/accept`, `/reject`). Testler yeşil: `tests/Host/Experience/Orders/Confirmation` 39/39 (kabul, ret, QR rezervasyonu, süre dolumu), `tests/Modules/QrOrdering/PendingOrders` 13/13. Sonuç: "garson onayı kapıyor" dayanağı kodda doğru.

## Kanıt (hepsi exit 0)
- `tokenlifecycle`/`final-tokenlifecycle`: 24/24. `relaysettings`/`final-relaysettings`: 18/18 (11 yeni; uçtan uca: üretilen adres `/api/v1/qr/sessions` 200, yenileme sonrası eski token 401 `QR_TOKEN_REVOKED`, iptal sonrası 401).
- `apiconventions`: 5/5 (3 yeni uç gerçek Host'ta yayınlanmış ve oturumsuz 401; allowlist bayat değil). `moduleboundaries`: 17/17 (RelaySettings -> Tables kenarı eklendi).
- `orders-confirmation` 39/39, `host-qrordering` 35/35, `host-composition` 22/22, `mod-CustomerSession` 27/27, `mod-RelaySecurity` 7/7, `mod-PendingOrders` 13/13.
- Mutasyonlar (geçerli derlenen, `cp`+`cmp` ile geri alındı): `mut1-issue-expired-v2` (süresi dolmuş dalı kaldır) -> `IssuingForATableWhoseTokenTimedOut...` kırmızı; `mut2-lifetime-4h` -> 10 yıl testi kırmızı; `mut3-no-relay-check` -> `WithoutAnActivatedRelay...` kırmızı; `mut4-allow-supervisor` -> `ASupervisorSessionIsRefused...` kırmızı. (`mut1-issue-expired` derlenmedi (CS0162), geçersiz mutant; yerine v2.)
- `plan-audit.log` 0 hata/0 uyarı, `consistency-audit.log` clean (ilk koşuda `ListActiveAsync` LIMIT'siz diye 1 ihlal verdi; 5000 sınırı eklendi), `task-scope.log` valid.

## İnceleme bulguları (qa-guvenlik-yetki, sonra qa-test-kalitesi)
- Yarış: iki eşzamanlı Issue'da kaybeden 404 `QR_NOT_FOUND` yerine 409 `QR_ALREADY_EXISTS` döner (düzeltildi, sahte depoyla test; TokenLifecycle 25/25).
- Kasiyer çerezi testi ayırt edici değildi; izin eksik yönetim oturumuyla 4 rotada 403 testi eklendi. Plan kabulü buna göre netleştirildi (süpervizör ve kasiyer çerezi 401, izinsiz yönetim oturumu 403).
- Süresi dolmuş token listede "QR yok" ve HTTP Issue onu değiştirir testi eklendi (RelaySettings 19/19); `mut5-list-keeps-expired` (süre filtresi kaldırıldı) bu testi kırdı, kaynak `cp`+`cmp` ile geri alındı.
- Bilerek yapılmadı: Issue/Rotate'te yönetici kimliği denetim izine yazılmıyor (yalnız Revoke `manager:{id}` taşır; yeni denetim deposu kapsam dışı); Rotate/Revoke için röle-hazır-değil yolu (revoke röle gerektirmez).

## Açık risk
- Röle alt alan adı yeniden etkinleştirilip farklı etiketle kurulursa basılı tüm QR'lar yanlış adrese gider (adres QR içinde sabit); QR'lar yeniden basılmalı.
- Ham token saklanmadığı için sayfa yenilenirse/kapatılırsa QR bir daha gösterilemez; çözüm "Yenile" (eski anında ölür).
- Yenile isteği ağ hatasından sonra tekrarlanırsa iki kez yenilenir (son QR geçerli); allowlist'te gerekçeli.
- Önceden kalan: `plan-audit` HEAD'de `UNOWNED_PRODUCTION_FILE` veriyordu (777'nin yeni dosyası); V1-RMD-777 md'de sahip satırı düzeltilerek ayrı plan commit'iyle (5f1a077f) giderildi.
