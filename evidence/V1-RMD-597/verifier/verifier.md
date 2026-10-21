# V1-RMD-597 — bağımsız doğrulayıcı (qa-bulgu-dogrulayici) sonuçları

7 doğrulayıcı çalıştı: B001+B002, B003+B004, B005+B006, B007+B008, B009+B010 (Yüksek adayları, ikişerli), B011–B025 ve B026–B040 (toplu). 40 bulgunun hiçbiri tamamen çürütülmedi; 20'sinin önemi düşürüldü, 5 alt madde çürütüldü.

| ID | Sonuç | Ham önem → Gerçek önem | Bugün tetiklenir mi | Not |
|---|---|---|---|---|
| B001 | KANITLANDI | Yüksek → Yüksek | evet | kiosk-lock.js:224, auth.js:117-118, waiter-app.js:865-871; sunucu oturumu iptal etmez |
| B002 | KANITLANDI | Yüksek → Yüksek | evet | RoleManagementService.cs:72-131 denetim yazmaz; authorization-model.md:224 ile çelişir. Düzeltme Host katmanında veya port ile (modül sınırı) |
| B003 | KANITLANDI | Yüksek → Orta | evet | AuthenticationService.cs:156; ön koşul geçerli oturum |
| B004 | KANITLANDI | Yüksek → Orta | evet | Tasarım kasıtlı (contracts.ts:58-60) ama Semih senaryosu 7 adımdan 4–7'si çalışmaz; geçici yol 15 dk veya şifreyle giriş + PIN yeniden kurma |
| B005 | KANITLANDI | Yüksek → Orta | evet | workspace.tsx:542; çift POST sunucuda zararsız (409) |
| B006 | KISMEN | Yüksek → Orta | evet | 12 sebep kodundan 10'u ham doğrulandı; "izin kodları ham" iddiası ÇÜRÜTÜLDÜ (yalnız 3 izin düşebilir, üçü de etiketli) |
| B007 | KISMEN | Yüksek → Orta | gizli | authorization_policies boşken çevrimdışı kuyruk dolmaz; 429 iddiası çürütüldü (uygulama içi limiter yok) |
| B008 | KISMEN | Yüksek → Orta | yapılandırmaya bağlı | Tutar tavanı iddiası çürütüldü (tasarım: ilk yanıt veren kazanır); izin sahipliği doğru |
| B009 | KISMEN | Yüksek → Yüksek | evet (temsil yolu) | Politika değerlendiricisi ölü kod (tablo boş) ama aynı tutar hatası PostgresAuthorizationDelegationRepository.cs:78'de canlı |
| B010 | KANITLANDI | Yüksek → Orta | evet | Kümülatif tavan Çıkarım (tasarım belirsiz) |
| B011 | KISMEN | Orta | gizli | Çift sayım haksız red üretir (MaxCount aşılmaz); aşım yalnız paralel istekte |
| B012 | KANITLANDI | Orta | gizli | |
| B013 | KANITLANDI | Orta | gizli/yarışa bağlı | |
| B014 | KANITLANDI | Orta → Düşük | gizli | İstemci genel Türkçe mesaj gösteriyor |
| B015 | KANITLANDI | Orta | evet | Test 110 TL ile açığı kaçırıyor |
| B016 | KANITLANDI | Orta | evet | B007/B011/B012'yi kapı gibi gizler |
| B017 | KISMEN | Orta → Düşük | gizli | Derinlemesine savunma |
| B018 | KANITLANDI | Orta → Düşük | evet (yavaş) | session_id FK eklenirse housekeeping kırılır |
| B019 | KANITLANDI | Orta → Düşük | evet | |
| B020 | KANITLANDI | Orta | evet | |
| B021 | KISMEN | Orta → Düşük | evet | |
| B022 | KISMEN | Orta → Düşük | kısmen | |
| B023 | KANITLANDI | Orta | evet | |
| B024 | KANITLANDI | Orta | evet | |
| B025 | KANITLANDI | Orta → Düşük | ara hatada | |
| B026 | KANITLANDI | Orta | evet | |
| B027 | KANITLANDI | Orta | gizli | |
| B028 | KISMEN | Orta → Düşük | — | E2E gerçek /pin /unlock /session/current çağırıyor; test-kalitesi T4 "unlock pasif kullanıcıyı reddetmiyor" iddiası YANLIŞ (AuthenticationService.cs:116-117 reddediyor) |
| B029 | KANITLANDI | Orta | evet | |
| B030 | KISMEN | Orta | evet | Tur kaybolmuyor, listede görünüyor, yalnız yeniden gönderme yok |
| B031 | KANITLANDI | Orta → Düşük | evet | |
| B032 | KANITLANDI | Orta → Düşük | evet | |
| B033 | KISMEN | Orta (karar) | evet | Bütçe kararı V1-WTR-012'de Semih onayıyla kayıtlı; yalnız authorization-model.md'de yok; e-posta/telefon KVKK runbook'ta var (iddia çürütüldü) |
| B034 | KANITLANDI | Orta (karar) | hayır (devDependency) | |
| B035 | KANITLANDI | Düşük | hayır | |
| B036 | KISMEN | Düşük | — | DESIGN §1 atıf iddiası ve V1-RMD-405 "kırmızı test" iddiası ÇÜRÜTÜLDÜ (ilke DESIGN.md:20'de; test pg_dump sürüm uyuşmazlığı) |
| B037 | KISMEN | Düşük | gizli | |
| B038 | KISMEN | Düşük | evet | |
| B039 | KISMEN | Düşük | büyük ölçüde hayır | "Çerez Secure TrustedProxies'e bağlı" çürütüldü (HTTPS dışı istek reddediliyor); HSTS yokluğu bilinçli (Caddyfile:25-28) |
| B040 | KISMEN | Düşük | karışık | İkram bütçesi UTC günü PO kararı (V1-WTR-012:16); SubjectServingUserId yok sayma bilinçli |

Çürüyen alt maddeler: B006 izin kodu, B007 429, B008 tutar tavanı, B028-T4, B033 e-posta/telefon, B036 DESIGN atıf + RMD-405 kırmızı, B039 Secure çerez.
Mükerrerler: B040#4≈B035, B033 pin_hash≈B038, B033 void tutarı≈B009, B032 kilit≈B001.
