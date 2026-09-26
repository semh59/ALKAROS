# V1-RMD-331 - Denetimin "en kritiği" dediği hesap kurtarma uç noktaları artık PosTerminal'den erişilebilir

- Task ID: V1-RMD-331
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K10 bulgusu: 95'ten fazla yönetim uç noktası hiçbir istemciden
çağrılmıyor. Denetimin kendi ifadesiyle **"en kritiği"**: şüpheli oturum iptali (`POST
/api/v1/management/security/users/{userId}/revoke-sessions`) veya kilitli kullanıcı kurtarma (`POST
.../force-unlock`) PosTerminal'den yapılamıyor, yalnızca doğrudan HTTP ile. Bu iki uç nokta zaten gerçek, test
edilmiş ve `security.manage` ile korunan sunucu tarafı davranışa sahipti (V1-RMD-266) — eksik olan tek şey bir
istemciydi.

Araştırma şunu ortaya çıkardı: bu iki eylemi çağırmak için gereken `userId`'yi bir kullanıcı adından çözecek HİÇBİR
uç nokta da yoktu (`RoleManagementEndpoints.cs`'de yalnızca rol/izin CRUD'u var, kullanıcı listeleme/arama yok).
`AccountRecoveryService`'in kendi belge yorumu bunu kasıtlı olarak kapsamı dışında bırakıyor ("never a username
lookup by a stranger") — ama bu grup zaten `security.manage` ile kapılı bir yöneticiye izin veriyor, bir yabancıya
değil, o yüzden aynı kapı altında salt-okunur bir arama eklemek bu tasarım kararını ihlal etmiyor.

Ayrıca şu mimari gerçek doğrulandı: `POST /api/v1/auth/login` (PosTerminal'in kullandığı GİRİŞ akışının kendisi),
giriş yapan kullanıcı `catalog.manage` iznine sahipse `alkaros.manager` çerezini OTOMATİK basıyor
(`DualScreenApplication.Endpoints.cs:79-89`) — ve bu, `SecurityAdministrationEndpoints.cs`'nin kullandığı AYNI
çerez adı. Yani bir yönetici PosTerminal'e normal şekilde giriş yaptığında tarayıcısında bu uç noktaların
gerektirdiği çerez zaten mevcut oluyor; ayrı bir "yönetim girişi" akışı inşa etmeye gerek yok.

## Owned surface

- `src/Clients/PosTerminal/src/routes/SecurityAdministration.tsx`
- `src/Clients/PosTerminal/src/routes/SecurityAdministration.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/api.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/contracts.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/App.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationTestDatabase.cs
- `plan/v1/remediation/V1-RMD-331-security-recovery-endpoints-reachable-from-posterminal.md`

## In scope

1. `GET /api/v1/management/security/users/lookup?username=...`: `IUserStore.GetByUsernameAsync`'i sarar, aynı
   `security.manage` filtresiyle korunur, bulunamazsa 404 döner.
2. `/settings/security` — `RelaySettings.tsx`/`TokenTerminalSettings.tsx` ile aynı tek-URL desenini izleyen yeni bir
   PosTerminal ekranı: kullanıcı adı arar, bulunan hesabı gösterir, "Tüm Oturumları Sonlandır" ve "Hesap Kilidini
   Kaldır" (yalnızca hesap kilitliyken etkin) düğmeleri sunar.
3. Denetimin kendi ifadesiyle "en kritiği" olarak işaretlediği İKİ uç noktanın (revoke-sessions, force-unlock) artık
   gerçekten bir istemciden çağrılabilir olduğunun gerçek bir HTTP testi (backend) ve gerçek bir bileşen testi
   (frontend) ile kanıtlanması.

## Out of scope

1. K10'un adlandırdığı diğer dört örnek — `RoleManagementEndpoints.cs` (rol/izin yönetimi), `PurchasingManagementEndpoints.cs`
   (tedarikçi CRUD), `PaymentSettlementEndpoints.cs` (mutabakat raporu ve manuel onaylar),
   `InventoryReportingEndpoints.cs` (kritik stok/gerçek-vs-teorik raporu) — hâlâ istemciden erişilemez durumda.
   Denetim bunları "95+" toplamın örnekleri olarak sayıyor; her biri kendi başına bir yönetim ekranı gerektiren,
   bu görevin kapsamına sığmayacak büyüklükte ayrı işler. Denetimin kendi "en kritiği" vurgusu — yalnızca
   revoke-sessions/force-unlock — bu görevde tam olarak kapatıldı; kalan örnekler ayrı, kendi Owned surface'ı olan
   görevler olarak açılmalı.
2. `IUserStore`'a yeni bir "tüm kullanıcıları listele" yeteneği eklemek — `lookup` bilinçli olarak tek-kullanıcı,
   tam-kullanıcı-adı araması: idari bir arama ekranının ayrıca sayfalama/kısmi eşleşme/yetkilendirme (örn.
   yöneticinin kendi şubesindeki kullanıcılarla sınırlı olması gibi) tasarım kararları gerektirir, bu görevin
   dar amacının (iki adlandırılmış kritik eylemi erişilebilir kılmak) ötesinde.

## Dependencies

- None

## Acceptance evidence

- Backend: `tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj`
  — 27/27 test geçti (yeni test dahil: `LookupResolvesAUsernameToTheUserIdTheOtherTwoActionsRequireAndOnlyAManagerMayUseIt`).
  Mutasyon kontrolü: yeni `/users/lookup` uç noktası geri alındığında test beklenen şekilde kırmızıya döndü
  (`Assert.Equal() Failure: Expected: Unauthorized, Actual: NotFound` — filtre hiç çalışmadan route eşleşmedi),
  dosya bayt-bayt geri yüklendi (`diff` ile doğrulandı, `IDENTICAL`), yeniden derleme sonrası paket tekrar 27/27
  yeşile döndü.
- Frontend: `npx vitest run src/routes/SecurityAdministration.test.tsx` — 5/5 test geçti. `npx tsc --noEmit` —
  sıfır hata. Tüm PosTerminal paketi (`npx vitest run`) — 30 dosya, 224 test, tümü yeşil (regresyon yok). Mutasyon
  kontrolü: `api.lookupUser`'ın URL'si kasıtlı olarak yanlış bir yola değiştirildi, 3/5 test beklenen şekilde
  kırmızıya döndü (mock fetch "unexpected fetch" hatası fırlattı), dosya bayt-bayt geri yüklendi (`diff` ile
  doğrulandı), tüm paket yeniden 224/224 yeşile döndü.
- Mimari doğrulama: `DualScreenApplication.Endpoints.cs:79-89`'un kendi kodu okunarak `alkaros.manager`
  çerezinin normal PosTerminal girişinde (yalnızca `catalog.manage` iznine bağlı olarak) otomatik basıldığı
  doğrulandı — bu ekranın kendi ayrı bir "yönetim girişi" akışına ihtiyacı olmadığını, mevcut oturumun yeterli
  olduğunu kanıtlıyor.

## Handoff

- None
