# V1-IAM-023 - Behavioural Tightening

- Task ID: V1-IAM-023
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

Davranışsal sıkılaştırma (karar dokümanı §1, hiçbir rakibin göndermediği
önleyici katman): grant motoru, parasal grant sınıfı izinler için (`bills.void`
/ `bills.comp` / `bills.discount`) kişi başına yuvarlanan bir oranı doğrudan
`identity.authorization_grants` üzerinden okur. Bir kullanıcının son 24 saatteki
`granted` sayısı, otuz günlük tabanının üç katına ya da üzerine çıkarsa (ve
pencerede en az üç eyleme ulaşmışsa) o kullanıcı, rolü normalde kendiliğinden
onaylasa bile bir `identity.behavioural_tightenings` satırıyla "yetki isteğine"
taşınır: sonraki her istek yönetici kararına düşer. `always_deny` politikası
yine kazanır; kapı asla reddetmez, yalnızca kendiliğinden onayı `Escalate`'e
düşürür. Grant servisine yeni bir pre-policy kapı kancası (`IPrePolicyGate`)
eklenir; `BehaviouralTighteningGate` ilk kancadır. Bir yönetici temizleyince
(`BehaviouralTighteningService.ClearAsync`) kapsam sonraki istekte yeniden
kendiliğinden onaya döner; açılış ve temizleme birer denetim satırıdır ve tablo
üzerindeki tetikleyici bunları değişmez tutar (silme yok, yeniden açma yok).
`IPrePolicyGate` DI kaydı ile temizleme HTTP yüzeyi V1-IAM-020 ve V1-IAM-024
entegrasyonuna aittir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-023-behavioural-tightening.md`
- `database/migrations/V1/V1-IAM-023/**`
- `src/Modules/Identity/IdentityModule.cs`
- `src/Modules/Identity/Authorization/Grants/**`
- `src/Modules/Identity/Authorization/Behavioural/**`
- `tests/Modules/Identity/Authorization/Behavioural/**`
- `evidence/V1-IAM-023/**`
- Yüzey devri (giriş): database/MigrationComposition/order.json, tests/Host/MigrationComposition/Manifest/ManifestTests.cs ve src/Modules/Identity/IdentityModule.cs, migration 048 ile dalga DI evi için V1-IAM-022'den bu göreve devredildi (PO:2026-09-04). src/Modules/Identity/Authorization/Grants/**, pre-policy kapı kancası (IPrePolicyGate) ve AuthorizationGrantService'in kapı yürüyüşü için V1-IAM-021'den bu göreve devredildi (PO:2026-09-04); tests/Modules/Identity/Authorization/Grants/** V1-IAM-021'de kalır.
- Yüzey devri (çıkış): database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs, pos.cashier.mutate kaldırma migration'ı için V1-IAM-024'e devredildi (PO:2026-09-05).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 048 değerine güncellenir (V1-RMD-089/9. dalga deseni).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Identity.Authorization.Tests`
  178/178 (yeni `Behavioural/**` 25 test — `BehaviouralRateAssessorTests` saf
  (tabanda düz oran spike değil, pencerede 3x spike, minimum eylem tabanı,
  taban sıfırken oran sonsuz olmaz, kısa pencere tabanı ölçekler, negatif/sıfır
  reddi); `BehaviouralTighteningMigrationTests` metin (sayaç ve temizle
  CHECK'leri, açık satır için kısmi UNIQUE index, append-once-clear
  tetikleyicisi, seed yok, down tetikleyici+fonksiyon+tablo düşürür);
  `PostgresBehaviouralTighteningRepositoryTests` + `BehaviouralTighteningDownMigrationTests`
  gerçek 005..048 zinciriyle (open + FindActive, ikinci open mevcut satırı
  döndürür, clear damgalar ve FindActive null döner, iki kez ya da bilinmeyen
  clear reddi, clear'dan sonra yeniden sıkılaştırılabilir, tetikleyici DELETE
  ile alan mutasyonunu reddeder, rate source yalnız pencere içi granted
  satırları kapsam bazında sayar; down tablo düşürür grants kalır);
  `BehaviouralTighteningGateTests` gerçek repo + DB (spike yokken izlenen
  eylem yine kendiliğinden onaylanır; spike bir tightening açar ve kendiliğinden
  onayı `pending` isteğe çevirir; açık tightening sonraki isteklerde
  escalation'ı sürdürür; yönetici temizleyince akış kendiliğinden onaya döner;
  izlenmeyen izin hiç kapılanmaz; `always_deny` politikası forced escalation'a
  rağmen kazanır)); `Host.Tests` `Manifest.ManifestTests` 16/16 (`PhaseBMax`
  048, 47 pozisyon, son giriş tabloları `["behavioural_tightenings"]`);
  `Host.Experience.Composition.Tests` 4/4 (DI grafiği `IEnumerable<IPrePolicyGate>`
  ile çözülür).
- Migration ileri: 001..048 zinciri boş `alkaros_fm6` veritabanına uygulandı;
  `identity.behavioural_tightenings` ile append-once-clear tetikleyicisi oluştu.
  Geri: `048-*.down.sql` uygulandı; tetikleyici, fonksiyon ve tablo düştü,
  `identity.authorization_grants` yerinde kaldı.
- Semih için gerçek senaryo: garson rolüne `bills.void` için `always_allow`
  tanımlıdır (normalde kendiliğinden onay). Test kullanıcısının son iki saatine
  dört `granted` void ekilir; sonraki void isteği artık kendiliğinden geçmez,
  bir tightening satırı açılır (`recent_count >= 4`, `trigger_ratio >= 3`) ve
  istek yöneticiye `pending` düşer. Yönetici `ClearAsync` der; bir sonraki void
  yeniden `granted` / `policy_path=auto` olur. Açılan ve temizlenen satırların
  ikisi de denetim izidir.

## Handoff

- V1-IAM-024
