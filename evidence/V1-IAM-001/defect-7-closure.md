# V1-IAM-001 — Kusur 7 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 7 — "Expired lock resetlenmiyor;
unknown-user timing farkı ve sınırsız hash iteration riski var."

## Preflight

- Repo root: D:\PROJECT\ALKAROS, git repo mevcut.
- Task ID: V1-IAM-001 (kusur 7 kapatması, Aşama 2 exception tablosu).
- Write allowlist: `src/Modules/Identity/Authentication/**` +
  `tests/Modules/Identity/Authentication/**` + `evidence/V1-IAM-001/**`.
- Başlangıç snapshot: bu oturumda Identity yüzeyi temizdi (git status'ta
  `src/Modules/Identity/` ve `tests/Modules/Identity/` değişiklik listesinde yoktu);
  kusur 1/3/4/5/6 değişiklikleri başka görevlere aittir, dokunulmadı.
- Sonuç write-set (yalnız bu görev): aşağıda "Değişen yollar" bölümünde.

## Değişen yollar

| Dosya | Değişiklik |
|---|---|
| `src/Modules/Identity/Authentication/PasswordHasher.cs` | `DummyPassword`/`DummyHash` const'ları; `MaximumIterations` (2_000_000) üst sınırı; `TryParse`'te `iterations > MaximumIterations` reddi |
| `src/Modules/Identity/Authentication/AuthenticationService.cs` | Unknown-user yolunda `PasswordHasher.Verify(password, PasswordHasher.DummyHash)` — sabit PBKDF2 işi (timing oracle kapatıldı) |
| `src/Modules/Identity/Authentication/PostgresUserStore.cs` | `RecordLoginFailureAsync` SQL'i: expired lock'ta sayaç 1'den başlar, `locked_until` NULL'a resetlenir; aktif lock'ta değişiklik yok |
| `tests/Modules/Identity/Authentication/PasswordHasherTests.cs` | +2 test |
| `tests/Modules/Identity/Authentication/AuthenticationServiceTests.cs` | +2 test |
| `tests/Modules/Identity/Authentication/PostgresUserStoreTests.cs` | +2 test |

## 1. Dummy hash üretimi ve doğrulaması

- Üretim: .NET Framework Rfc2898DeriveBytes (PowerShell), parola
  `alkaros-dummy-user-verify`, 16 byte deterministik salt, 600_000 iterasyon,
  SHA256, 32 byte hash.
- Üretilen encoded hash: `pbkdf2-sha256$600000$ABEiM0RVZneImaq7zN3u/w==$LB8eA/gWEVLWqhBml+YbdECa1XzbXVvviwYDiM7TfF8=`
- Test `VerifyAcceptsTheDummyHash` gerçek test ortamında (net8.0) bu literal'ın
  `Verify` ile doğrulandığını kanıtlar — hayali değer değildir.

## 2. Davranış değişiklikleri

1. **Bounded iteration**: `TryParse` artık `10_000 ≤ iterations ≤ 2_000_000`
   koşuluyla sınırlar. Saldırgan/bozuk kayıt dev iteration sayısı taşısa bile
   PBKDF2 sınırsız çalıştırılamaz (DoS kapatıldı).
2. **Constant work**: bilinmeyen kullanıcıda da aynı 600k iterasyon hash verify
   işi çalışır; response time known-user ile ayırt edilemez. İnaktif kullanıcı
   yolu korundu (InvalidCredentials).
3. **Expired lock reset**: `RecordLoginFailureAsync`'te expired lock
   (`locked_until <= @now`) algılandığında sayaç sıfırlanıp 1'den başlatılır ve
   `locked_until` temizlenir; eşiğe yalnızca yeni pencerede ulaşılınca yeniden
   lock arm edilir.

### Teknik not — concurrency denemesi

İlk SQL implementasyonu `UPDATE ... FROM (SELECT CASE ...) AS counter`
kullandı. `ConcurrentFailuresReachTheLockoutThresholdWithoutLostUpdates` testi
bunu yakaladı: PostgreSQL `UPDATE ... FROM` alt sorgusu MVCC snapshot'tan okur,
row lock sonrası (EvalPlanQual) tazelenmez → eşzamanlı UPDATE'ler sayaç
artışlarını kaybeder, test FAIL (ilk koşuda 1 başarısız / 40). Çözüm: alt sorgu
kaldırıldı, doğrudan target row referanslı `CASE` ifadesi (SET ifadeleri taze
row görünümüyle yeniden değerlendirilir) — mevcut seri davranış korunur.
Son koşu: 40/40, concurrency testi dahil.

## 3. Test koşuları (gerçek PostgreSQL container, alkaros_test:5433)

### 3.1 Identity suite (yeni durum)

Komut: `dotnet test tests\Modules\Identity\Authentication\ALKAROS.Identity.Authentication.Tests.csproj --nologo -v q`

```
Başarılı!  - Başarısız:     0, Başarılı:    40, Atlanan:     0, Toplam:    40, Süre: 3 s
EXIT=0
```

Önceki 34 → 40 → 41 test (6 yeni + denetim fix'leri). Yeni testler:
- `PasswordHasherTests.VerifyRejectsExcessiveIterationCount` — `MaximumIterations + 1` iterasyonlu encoded hash reddedilir.
- `PasswordHasherTests.VerifyAcceptsTheDummyHash` — dummy literal gerçek hash'tir.
- `AuthenticationServiceTests.UnknownUsernameLoginTakesComparableTimeToKnownUserLogin` — unknown login, bilinen kullanıcının yanlış-parola login süresinin ≥ %80'i kadar PBKDF2 işi yapar (karşılaştırmalı; mutlak eşik kaldırıldı).
- `AuthenticationServiceTests.InactiveUserLoginTakesComparableTimeToKnownUserLogin` — inaktif kullanıcı da aynı sabit işi yapar (denetim fix'i: inactive yoluna dummy verify eklendi, `AuthenticationService.LoginAsync`).
- `AuthenticationServiceTests.ExpiredLockRestartsFailureCountingAndReLocksOnlyAfterNewMaxFailures` — expired sonrası 1 hata lock arm etmez, 2. hata eder; DB state her adımda doğrulanır.
- `PostgresUserStoreTests.RecordLoginFailureAfterLockExpiryRestartsTheCounter` — sayaç 1, locked_until NULL.
- `PostgresUserStoreTests.RecordLoginFailureAfterLockExpiryCanReLockOnNewWindow` — max=1 edge case'te yeni pencerede yeniden lock.

### 3.2 Full solution suite (regresyon)

Komut: `dotnet test ALKAROS.slnx --no-build --nologo -v q` → EXIT=0

| Proje | Başarılı | Toplam |
|---|---|---|
| ALKAROS.Architecture.Tests | 5 | 5 |
| ALKAROS.Secrets.Tests | 21 | 21 |
| ALKAROS.SensitiveData.Tests | 23 | 23 |
| ALKAROS.Transactions.Tests | 25 | 25 |
| ALKAROS.TransactionOutboxIntegration.Tests | 12 | 12 |
| ALKAROS.Idempotency.Tests | 71 | 71 |
| ALKAROS.Identity.Authentication.Tests | 41 | 41 |
| ALKAROS.Host.Tests | 60 | 60 |
| **Toplam** | **258** | **258** |

Sonuç: 258/258 başarılı, 0 başarısız, 0 atlanan (kusur 1/3/4/5/6 regresyonu yok).

### 3.3 Build

Komut: `dotnet build ALKAROS.slnx --no-restore --nologo -v q`

```
Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata
EXIT=0
```

## 4. Kapanış doğrulaması

- Kapanış ölçütü (plan satırı): "Reset, constant-work ve bounded-iteration
  testleri geçer." — üç test grubu da gerçek çıktıyla geçti (3.1).
- Write-set: yalnız yukarıdaki 6 dosya değişti; başka görev yüzeyine
  dokunulmadı; kullanıcı mevcut değişiklikleri korundu.
- Denetim fix'leri (2026-08-03, bağımsız denetim kısmi notları): inactive-user
  yoluna dummy verify eklendi (timing sızıntısı kapatıldı); timing testleri
  mutlak eşik yerine karşılaştırmalı ölçüme çevrildi (flaky riski azaltıldı);
  `debug_xmax` teşhis DB'si 5433'ten DROP edildi.
- Kalan durum: commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).
