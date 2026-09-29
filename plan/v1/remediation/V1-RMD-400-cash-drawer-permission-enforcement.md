# V1-RMD-400 - Kasa oturumu uçlarını `cash.drawer` iznine bağlamak; mutabakatı amir ve dört göz kuralına almak

- Task ID: V1-RMD-400
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi (H-02, Yüksek): `DualScreenApplication.CashSession.cs` içindeki kasa oturumu uçlarının hepsi
yalnız oturum kontrolü yapıyor; `cash.drawer` izni kaynak kodda hiçbir yerde kontrol edilmiyor. Gerçek girişle bir
garson ve bir mutfak personeli kasa oturumu açıp para çıkışı yazabiliyor. Model §2 `cash.drawer`'ı "çekmece açma /
sayım" koruması olarak tanımlar; §3'te garson ❌. V1-RMD-393 F-09: kasiyer kendi kasa oturumunu mutabakatlayabiliyor;
`docs/domain/cash-session-design.md` §6 mutabakatı Supervisor/Accountant/Admin'e verir.

Bu görev: kasa oturumunun bütün uçlarını `cash.drawer` iznine bağlar; mutabakatı ayrıca amir izni
`cash.session.override` (V1-RMD-236, supervisor/manager) ve dört göz kuralına (oturumun kendi kasiyeri mutabakat
yapamaz, V1-RMD-316 deseni) bağlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-400-cash-drawer-permission-enforcement.md`
- `evidence/V1-RMD-400/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde kalır) — yalnız her uçtaki oturum kontrolü `cash.drawer` izin kontrolüne çevrilir;
  `/reconcile` ayrıca `cash.session.override` ve dört göz kontrolü alır. İş mantığı değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  (V13-CSH-004 sahipliğinde kalır) — `SeedCashierSessionAsync` gerçek kasiyer gibi `cash.drawer` tutar; izinsiz
  oturumun ve mutabakat kurallarının testleri eklenir.

## In scope

- Açma, önerilen açılış bakiyesi, etkin oturum, sayım başlatma, sayım, beklenen nakit, kapatma, para giriş/çıkışı,
  nakit tahsilat: `cash.drawer`.
- Mutabakat: `cash.drawer` + `cash.session.override` + mutabakatı yapan kişi oturumun kasiyeri olamaz.
- Ret 403 `FORBIDDEN`, Türkçe mesaj (mevcut `DualScreenApplication` hata eşlemesi).

## Out of scope

- Terminaller arası kasa oturumu erişimi (V1-RMD-393 F-08) — ayrı görev.
- Kartla tahsilat izni (V1-RMD-399 Q-01) — V1-RMD-401.
- Yeni izin kodu veya migration.

## Dependencies

- V1-RMD-399

## Acceptance evidence

- `dotnet build tests/Host/Experience/CashSession -c Release`: 0 uyarı, 0 hata.
- `ALKAROS.Host.Experience.CashSession.Tests` (gerçek PostgreSQL 18): 16/16 geçti
  (`evidence/V1-RMD-400/cashsession-tests.log`). Yeni: `ASessionWithoutCashDrawerIsForbiddenOnDrawerRoutes`
  (izinsiz oturum açma/etkin oturum/para çıkışı 403, kasa oturumu yazılmıyor) ve
  `ReconcileNeedsASupervisorWhoIsNotTheSessionsOwnCashier` (düz kasiyer 403, oturumun kendi amiri 403, başka amir 200).
- Aynı iki test üretim değişikliği geri alınınca kırmızı (`evidence/V1-RMD-400/red-without-fix.log`, 2/2 başarısız).
- V1-RMD-399 probe'ları düzeltilmiş kopyada (`evidence/V1-RMD-400/audit-probes-after-fix.log`): gerçek girişli
  `waiter` ve `kitchen-staff` kasa oturumu açamıyor (D3 geçti); izinsiz oturum taramasında kasa oturumunun 7 ucu artık
  403, korumayı geçen tek rota kartla tahsilat (V1-RMD-401 kapsamı).
- Semih'in elle deneyebileceği senaryo: garson hesabıyla Kasa ekranına girip kasa açmayı denemek "Bu işlem için
  yetkiniz yok." ile reddedilir; kasiyer kasayı açar, kapatır; mutabakatı yalnız başka bir şef garson/yönetici yapar.

## Handoff

- None
