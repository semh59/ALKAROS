# V1-RMD-399 — Yetkilendirme hedefli derin denetimi

Tarih: 2026-09-28. Taban: `b1973f35` (görevin InProgress commit'i). Üretim kodu değiştirilmedi.

Yöntem V1-RMD-393/398 ile aynıdır: kapsam matrisi koddan önce yazıldı (`coverage-matrix.md`, commit `a6859e23`),
plan kararları kod okunmadan önce tarandı, her matris satırı gerçek sunucu + gerçek PostgreSQL üzerinde çalışan bir
probe ile sınandı, sonuç kör kalibrasyon, mutasyon denetimi ve bağımsız çürütme ile sınandı.

## 1. Karar taraması (bulgu sayılmayanlar)

Kod okunmadan önce `docs/domain/authorization-model.md`, `V1-IAM-001..031`, `docs/engineering/authz-wave-remediation-plan.md`
ve `docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-26.md` tarandı. Aşağıdakiler bilinçli karar ya da daha önce kapatılmış
bulgudur; probe'lar bunları yalnız geri dönüş (regresyon) açısından sınar:

| Konu | Kaynak | Denetimdeki durum |
| --- | --- | --- |
| Çevrimdışı bütçe imzalı JWT değil, sunucuda tutulan satır | V1-IAM-022 "Tasarım sapması" | Karar; bulgu değil. |
| Delegasyon süresi arka plan işi yerine sorgu süzgeciyle biter | V1-IAM-021, model §1 | Karar; bulgu değil. |
| Kendi grant isteğini / kendi davranışsal kısıtını onaylama yasağı | V1-RMD-316 (K9) | Düzeltilmiş; C1, C5 geçti. |
| Çevrimdışı mutabakatta başkasının `budgetId`'si | V1-IAM-025 bağımsız denetim notu | Düzeltilmiş; C7c geçti. |
| Grant tekrar oynatmada tutar/sebep/konu eşleşmesi | `AuthorizationGrantService.MatchesReplay` | Düzeltilmiş; kod okundu. |
| Müşteri ekranı eşleştirme ve ekran oturumu iptali yalnız oturum ister | V1-IAM-024 In scope | Karar; D2 izin listesinde. |
| NFC siparişi oturumsuz, doğrudan `Accepted` | V12-NFC-001 Goal ("restoranın yerel ağı üzerinden") | Karar; güven sınırı yerel ağdır. |
| Yönetim uçlarının çoğunun istemcisi yok | K10 / V1-RMD-296 (Planned) | Bilinen; bu denetimin konusu değil. |

## 2. Uç nokta envanteri

`endpoint-inventory.tsv`: uygulamanın uç nokta kaynağından makinece çıkarılan 321 rota×yöntem satırı; her satırda
oturumsuz ve izinsiz oturumlu isteğin gerçek HTTP sonucu ve sınıfı vardır.

- Oturumsuz (D1): 292 özel rota 401/403 döndü; 21 kamusal rota (sağlık, giriş, ekran eşleştirme, QR, NFC, imzalı
  platform webhook'ları, `/api/{**path}` 404 geri dönüşü) tasarım gereği açıktır; ilk turda ulaşılamayan 8 rota
  ikinci turda iyi biçimlenmiş istekle 401 döndü. **Oturumsuz ulaşılabilen özel rota yok.**
- İzinsiz oturum (D2): 186 mutasyon rotasından 161'i 401/403 döndü; 13'ü tasarım gereği yalnız oturum ister
  (kaynaklarıyla probe'da listeli); 7'si ikinci turda 403 döndü. **Kalan 8 rota koruma kontrolünü geçti**: kasa
  oturumunun 7 ucu (bkz. H-02) ve kartla tahsilat ucu (bkz. Q-01).

## 3. Kapsam matrisi sonuçları

Kör koşu: `probes/run-1-blind.log` — 25 probe, 16 geçti, 9 başarısız.

| # | Probe | Sonuç |
| --- | --- | --- |
| A1 | `A1LoginLocksAccountAfterRepeatedFailures` | Geçti |
| A2 | `A2LogoutEndsSessionOnNextRequest` | Geçti |
| A3 | `A3LoginSessionLivesAtMostEightHours` | **Başarısız → H-01** (12,00 saat) |
| A4 | `A4DisplaySessionCannotMutateCashierResources` | Geçti |
| A5 | `A5ManagerRevokeSessionsEndsVictimSessionOnNextRequest`, `A5DeactivatedUserSessionIsRejected` | Geçti (ikisi) |
| B1 | `B1SeededRoleMatrixMatchesDecisionRecord` | Geçti (6 rol × 15 izin, §3/§3.1/§3.2 ile birebir) |
| B2 | `B2RevokedPermissionTakesEffectOnNextRequest` | Geçti |
| B3 | `D2SecondPassPermissionlessSessionIsRefused` (rol/kullanıcı yönetimi) | Geçti |
| C1 | `C1RequesterCannotApproveOwnGrant` | Geçti |
| C2 | Kod okuma: `AuthorizationGrantService.MatchesReplay` | Sağlam (tutar/sebep/konu/isteyen eşleşmesi) |
| C3 | `C3WaiterCompOnAnotherWaitersCheckIsRefused` / `C3WaiterCompOnUnassignedCheckIsNotEscalated` | Geçti / **Başarısız → H-07** |
| C4 | `C4DelegationCanBeCreatedThroughSomeRoute` | **Başarısız → H-04** |
| C5 | `C5UserCannotClearOwnTightening` | Geçti |
| C6 | `C6SupervisorLoginReachesDecisionSurface` | **Başarısız → H-03** |
| C7 | `C7OfflineReconciliationUsesTheCallersRealRole` / `…AppliesOwnCheck` / `C7OfflineBudgetOfAnotherUserIsRejected` | **H-05** / **H-06** / Geçti |
| D1 | `D1EveryNonPublicRouteRejectsAnonymousCaller` + `D1SecondPass…` | Geçti |
| D2 | `D2EveryMutationRouteRejectsPermissionlessSession` + `D2SecondPass…` | **Başarısız → H-02, Q-01** / Geçti |
| D3 | `D3RoleWithoutCashDrawerCannotOperateTheDrawer` (waiter, kitchen-staff) / `D3WaiterCannotReserveOrSplit` | **Başarısız → H-02** / Geçti |
| D4 | D1/D2 taraması (yönetim rotaları kasiyer çerezini kabul etmiyor) | Geçti |
| E1 | `E1SessionForTerminalACannotActOnTerminalB` | Geçti |
| E2 | V1-RMD-393 F-08 (kasa oturumunda terminal sahipliği) | Bilinen; tekrar sınanmadı |
| E3 | Garson siparişi sahipliği: yalnız void/comp için karar var (§3 karar 1) | C3 ile sınandı |
| F1 | D2 taraması, `security/**` rotaları izinsiz oturumda 403 | Geçti |
| F2 | `D2SecondPassPermissionlessSessionIsRefused` (`/management/users`) | Geçti |
| F3 | `endpoint-inventory.tsv` kamusal 21 satır, tek tek incelendi | Hepsi karar kapsamında (§1) |

## 4. Bulgular

Önem derecesi bağımsız çürütme ajanının gerekçeli değerlendirmesiyle birlikte verilmiştir.

### H-02 (Yüksek) — Kasa çekmecesi işlemleri hiçbir izin istemiyor

`src/Host/DualScreen/DualScreenApplication.CashSession.cs` içindeki bütün uçlar (oturum açma, sayım başlatma,
sayım, kapatma, mutabakat, para giriş/çıkışı, nakit tahsilat) yalnız `RequireCashierAsync` (oturum kontrolü) çağırır;
`ApplicationPermissions.CashDrawer` kaynak kodda hiçbir yerde kontrol edilmez. Model §2 `cash.drawer`'ı "çekmece açma /
sayım" koruması olarak tanımlar; §3'te garson ❌, `kitchen-staff` yalnız `kitchen.advance` tutar.
Kanıt: gerçek girişle `waiter` ve `kitchen-staff` kasa oturumu açtı (201); D2'de izinsiz oturum 7 kasa ucunun
korumasını geçti. Neden: `authz-wave-remediation-plan.md` eşleme tablosu 2026-09-04'te "Cash — uç yok, no-op" diye
yazıldı; kasa uçları 2026-09-18'de eklendi ve hiç izin koduna bağlanmadı. V1-RMD-393 F-09 (kasiyerin kendi
mutabakatı) bunun bir örneğidir; F-08 (terminaller arası erişim) ayrı bulgudur.

### H-03 (Orta) — Şef garson (supervisor) karar ekranına hiç ulaşamıyor

Yönetim çerezi girişte yalnız `catalog.manage` tutan kullanıcıya verilir
(`DualScreenApplication.Endpoints.cs:80-84`); `supervisor:` cihaz oturumunu üreten hiçbir kod yok, oysa
`ManagementSessionLookup` onu karar ekranı, rol yönetimi, mutabakat, gün sonu, gözlemlenebilirlik ve stok
raporları için kabul eder. Kanıt: gerçek `supervisor` girişi yalnız kasiyer çerezi aldı; bekleyen onaylar 401.
Model §4 adım 3 ve §3 karar 3 ile çelişir. Kapalı başarısız olur (yetki yükseltmesi değil), ama onay akışı yoğun
saatte tek yöneticiye düşer.

### H-05 (Orta) — Çevrimdışı mutabakat istemcinin bildirdiği rolü kullanıyor

`OfflineReconciliationEndpoints.cs` yalnız bütçe sahibini ve `RequesterUserId`'yi doğrular; `RequesterRoleCode`
istemciden gelir, `OfflineGrantReconciler` canlı politika kontrolünü bu rolle yapar ve grant satırına bu rolü yazar
(uçtaki yorum rolün de doğrulandığını söylüyor, ama doğrulamıyor). Kanıt: garson politikası `always_deny`'a
çekildikten sonra dürüst rol → `Denied`, "manager" rolü → `Pending`. Sonuç onaylı değil bekleyen olur; ama canlı
sıkılaştırma atlanır ve denetim satırında sahte rol kalır. Bugün bu ucu çağıran istemci yok; V1-RMD-297 (Planned)
WaiterPwa'yı bu uca bağlayınca yol olağan hâle gelir.

### H-06 (Düşük-Orta) — Çevrimdışı yolda "kendi hesabı" kuralı yok

Aynı yol model §3 karar 1'deki kendi hesabı kontrolünü uygulamaz; `SubjectServingUserId` de istemciden gelir.
Kanıt: garsonun başka garsonun hesabındaki çevrimdışı ikramı `Pending` (yöneticiye ulaşıyor); aynı istek çevrimiçi
otomatik reddediliyor (C3a geçti).

### H-01 (Düşük) — 8 saatlik oturum kararı çalışma zamanında uygulanmıyor

V1-IAM-031 yalnız `SessionTokenIssuer.DefaultLifetime`'ı 8 saate indirdi; giriş ucu kendi cihaz oturumunu
`TimeSpan.FromHours(12)` ile açar (kasiyer, yönetici, PIN ile kilit açma rotasyonu). Kanıt: gerçek girişten sonra
oturum ömrü 12,00 saat. Kararın kabul senaryosu ("8 saat sonra 401") bugün yanlış.

### H-04 (Düşük) — Delegasyon oluşturacak hiçbir yol yok

`IAuthorizationDelegationRepository.CreateAsync`'in modül dışında çağıranı yok; karar ekranı yalnız listeler ve iptal
eder. V1-IAM-021'in hedefi ("yönetici yetkisini devreder") erişilemez; `DelegationEscalationResolver` yalnız elle SQL
ile eklenen satırda çalışır.

### H-07 (Düşük, karar sorusu) — Sahipsiz hesapta garson ikramı yöneticiye gidiyor

`Order.ServingUserId` boşsa kendi hesabı kontrolü atlanır; istek yönetici kuyruğuna düşer (202). Sahipsiz hesabın
"başka garsonun hesabı" sayılıp sayılmayacağı kararda yazmıyor; PO kararı gerekir.

### Q-01 (Karar sorusu) — Kartla tahsilat yalnız oturum istiyor

Modelde ödeme alma izni hiç yok; kilitli tasarım (`check-and-table.md` §2.1) garsonun hesabı kasaya göndermesini,
kasanın tahsil etmesini öngörür ama bunu zorlayan izin yok. Nakit tahsilat H-02 kapsamındadır.

### Çürütme ajanının ek bulguları (kodda doğrulandı)

- **N-1 (Düşük):** Grant değerlendirmesinde rol `roleIds[0]` ile seçilir; `PostgresRoleRepository.GetRoleIdsForUserAsync`
  sorgusunda `ORDER BY` yok. Çok rollü kullanıcıda kendi hesabı kuralının ve hangi rol politikasının uygulanacağı
  satır sırasına bağlıdır.
- **N-2 (Orta):** Personeli pasifleştiren hiçbir ürün yolu yok (kaynakta `identity.users.active = false` yazan kod
  yok). İşten ayrılan personel için yalnız oturum iptali var; kişi parolasıyla yeniden girebilir.
- **S-01 (Düşük, bilinen):** `CatalogManagerEndpointFilter` İngilizce hata metni döndürür ve PosTerminal bunu ekrana
  basar; V1-RMD-131'de not edilmiş, açık görevi yok.
- **T-01 (test boşluğu):** Pasif kullanıcının mevcut kasiyer oturumunu reddeden kontrolü sınayan test yok (kalibrasyon
  bunu gösterdi).

## 5. Mutasyon denetimi

`mutation/run-mutations.sh`, `mutation/results.log`: her mutasyon bir kopyada tek bir korumayı kaldırır ve onu görmesi
gereken probe'u koşar. **10/10 öldürüldü**: kendi onayı yasağı (M01), kendi hesabı kuralı (M02), iptal edilmiş oturum
(M03), rol atama izni (M04), bütçe sahipliği (M05), katalog yöneticisi izni (M06, D2 taraması), rezervasyon izni (M07),
kendi kısıtını temizleme yasağı (M08), terminal bağlama (M09), giriş kilidi eşiği (M10).

## 6. Kör kalibrasyon

Ayrı bir ajan gizli bir hata tohumladı; yama ve açıklama SHA-256 ile mühürlendi (`calibration/SEAL.sha256`,
09:58:09Z). Tohumlu kopyada probe'lar koşuldu ve karar mühür açılmadan yazıldı (`calibration/VERDICT.md`):
**YAKALANDI** — yalnız `A5DeactivatedUserSessionIsRejected` kırmızıya döndü; tahmin "oturum sorgusundan `active`
koşulunun düşmesi" idi. Mühür açıldı, özetler doğrulandı: tohum `DualScreenStore.AuthenticateCashierAsync`
içindeki `AND u.active` koşulunun silinmesiydi. Mevcut test takımında bunu yakalayacak test yok (T-01).

## 7. Bağımsız çürütme

Ayrı bir ajan her bulguyu karar belgeleri, probe doğruluğu, mevcut azaltıcılar ve açık görevler açısından çürütmeye
çalıştı: H-01, H-02, H-03, H-04, H-05, H-06 **doğrulandı**; H-07 karar sorusuna **düşürüldü**; Q-01 açık karar sorusu;
S-01 **bilinen**. Hiçbiri önceki denetimlerde (authz-wave planı, 2026-09-26 bağımsız denetim, V1-RMD-393) yok;
H-02 kök neden olarak yenidir ve V1-RMD-393 F-09'u kapsar.

## 8. Düzeltme önerileri (ayrı görevler)

1. H-02: kasa oturumu uçlarını `cash.drawer`'a bağlamak; mutabakatı ayrı bir yetkiye (ör. `cash.session.override`)
   almak — F-09 ile birlikte.
2. H-03: girişte `supervisor` rolüne `supervisor:` cihaz oturumu vermek.
3. H-05 + H-06: çevrimdışı mutabakatta rolü sunucudan okumak, kendi hesabı kuralını sunucudaki siparişten uygulamak
   (V1-RMD-297'den önce).
4. N-2: personel pasifleştirme ucu (oturumları da iptal eder) + T-01 testi.
5. H-01: giriş/kilit açma ömrünü `SessionTokenIssuer.DefaultLifetime`'a bağlamak.
6. H-04: yönetici delegasyon oluşturma ucu.
7. N-1: çok rollü kullanıcıda deterministik rol seçimi.
8. PO kararı: H-07 (sahipsiz hesap) ve Q-01 (ödeme alma izni).
