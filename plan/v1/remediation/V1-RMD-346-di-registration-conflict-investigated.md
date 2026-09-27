# V1-RMD-346 - Çelişen DI kayıtları (RegisterTransient/TryAddSingleton, ISecretProvider) araştırıldı: gerçek davranış riski yok

- Task ID: V1-RMD-346
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Aynı arayüz için çelişen DI kaydı:
`RegisterTransient` + `TryAddSingleton` (ikincisi sessizce yutuluyor, `KitchenModule.cs:28` vs
`KitchenOperationsEndpoints.cs:102`); `ISecretProvider` 3 modülde bağımsız kayıtlı." Bu oturumun önceki bir
bulgusu (K7, V1-RMD-319) TAM OLARAK bu sınıf bir çelişkiyi (`IUnitConverter`'ın Transient/Singleton çakışması)
GERÇEK bir davranış hatası olduğu için düzeltmişti — o yüzden bu bulgu ciddiye alınıp derinlemesine araştırıldı.

**Sonuç: bu iki örnekte GERÇEK bir davranış riski YOK, K7'den farklı olarak.**

1. `IKitchenTicketRepository`/`IPhysicalPrintRecoveryService`/vb. (`KitchenModule.cs` vs
   `KitchenOperationsEndpoints.cs`): bu oturumun kendi keşfettiği gerçek DI kompozisyon yolunda
   (`ModuleRegistry.ComposeRoot` + `HostComposition.ApplyComposedModuleServices`, düz `AddSingleton`/
   `AddTransient` kullanır, sonraki `TryAdd*` çağrılarına HER ZAMAN kazanır) modülün kendi `RegisterTransient`
   kaydı HER ZAMAN kazanır — `KitchenOperationsEndpoints.cs`'nin `TryAddSingleton` çağrıları prodüksiyonda ölü
   koddur. Ama bu repository'ler (`PostgresKitchenTicketRepository`, `PostgresPhysicalPrintRecoveryRepository`,
   vb.) TAMAMEN durumsuz (stateless) — her çağrıda `NpgsqlDataSource`'a delege ediyorlar, kendi içlerinde hiçbir
   önbellek/sayaç/mutable alan tutmuyorlar. Transient vs Singleton, durumsuz bir sarmalayıcı için davranışsal
   olarak eşdeğer (K7'nin `IUnitConverter`'ı ise TAM TERSİ: `_customConversions`/`_directlyRegisteredPairs`
   gibi mutable, çağrılar arası paylaşılması GEREKEN durum tutuyordu — bu yüzden orada Transient GERÇEKTEN bir
   bug'dı).
2. `ISecretProvider` (`OnlineOrderingModule.cs`, `SecurityModule.cs`, `QrOrderingModule.cs`): üçü de AYNI somut
   türü (`EnvironmentVariableSecretProvider`) kaydediyor, üçü de Transient. `EnvironmentVariableSecretProvider`
   her çağrıda doğrudan `Environment.GetEnvironmentVariable` okuyor — kendi içinde hiçbir durum yok. Üç ayrı
   kayıt, DI konteynerinin normal `GetService<T>()` davranışıyla EN SONUNCUSUNA çözülür; hangisi "kazanırsa"
   davranış birebir aynı, çünkü üçü de aynı, durumsuz implementasyonu üretiyor.

## Owned surface

- Kod değişikliği yok (bu görevin kendisi — bkz. Acceptance evidence).
- `plan/v1/remediation/V1-RMD-346-di-registration-conflict-investigated.md`

## In scope

1. Gerçek "serve" kompozisyon yolunun (`ModuleRegistry.ComposeRoot`/`HostComposition.ApplyComposedModuleServices`)
   bu iki örnek için GERÇEKTEN hangi kaydın kazandığının doğrulanması.
2. Kazanan ve kaybeden implementasyonların (repository sınıfları, `EnvironmentVariableSecretProvider`) kendi
   kaynak kodunun okunarak durumsuz olup olmadıklarının doğrulanması — K7'nin gerçek bug'ıyla (mutable
   `_customConversions`) doğrudan karşılaştırıldı.
3. Bu deseni (`TryAddSingleton`/`TryAddTransient`, bir modülün kendi kaydından SONRA çağrılan "savunmacı
   varsayılan" registration) tüm `src/Host/Experience/**`'te kaç kez tekrarlandığının kabaca ölçülmesi
   (~400 `TryAdd*` çağrısı) — bunun bu kod tabanının GENEL, kasıtlı bir deseni olduğu (standalone test
   host'ları için "eğer modül zaten kaydetmediyse" savunmacı varsayılan), Mutfak'a özgü bir kaza olmadığı
   doğrulandı.

## Out of scope

1. ~400 `TryAdd*` çağrısının HER BİRİNİN, kazandıkları kaydın altındaki implementasyonun durumsuz olup
   olmadığını tek tek doğrulamak — bu, K7'nin kendi bulgusunun (gerçek bir mutable-state bug'ı) tesadüfen
   yakalandığı gibi, sistematik bir tarama gerektirir. Böyle bir tarama inşa etmek (belki
   `consistency_audit.py`'ye yeni bir kural olarak) bu bulgunun kendi kapsamının çok ötesinde, ayrı bir görev.
2. `TryAddSingleton`/`TryAddTransient` kalıbını "temizlemek" (örn. ölü `TryAddSingleton` çağrılarını
   `TryAddTransient` olarak değiştirmek, ya da tamamen kaldırmak) — davranışsal olarak hiçbir fark yaratmıyor
   (ikisi de durumsuz implementasyonlar için eşdeğer), bu yüzden kozmetik bir değişiklik olurdu; gerçek bir
   hata düzeltmesi değil.

## Dependencies

- None

## Acceptance evidence

- `PostgresKitchenTicketRepository`, `PostgresPhysicalPrintRecoveryRepository` ve kardeşlerinin kaynak kodu
  okunarak hiçbir mutable alan taşımadıkları doğrulandı — yalnızca `NpgsqlDataSource`'a delege ediyorlar.
- `EnvironmentVariableSecretProvider`'ın kaynak kodu okunarak her çağrıda doğrudan
  `Environment.GetEnvironmentVariable` okuduğu, hiçbir durum tutmadığı doğrulandı.
- `grep -c "TryAdd" src/Host/Experience/*/*.cs`: ~400 çağrı — bu desenin Mutfak'a özgü olmadığı, tüm
  `Experience` katmanının genel kalıbı olduğu doğrulandı.
- K7'nin (V1-RMD-319) kendi gerçek bug'ıyla (mutable `_customConversions`/`_directlyRegisteredPairs`) doğrudan
  karşılaştırıldı: oradaki fark GERÇEK bir davranış hatasıydı çünkü durum kaybediliyordu; burada durum yok,
  kaybedilecek bir şey yok.

## Handoff

- None
