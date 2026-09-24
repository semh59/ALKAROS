# V1-RMD-272 - Kayıtlı ama çağrılmayan servisler için erişilebilirlik kapısı

- Task ID: V1-RMD-272
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Bu oturumda tekrar tekrar görüldü: "DI'da kayıtlı ve birim testli" olan servis, çalışan
uygulamadan hiç çağrılmıyor olabilir (Faz 1'in 6 servisi, Faz 2'nin 3 yüzeyi). Testler geçer,
görev `Done` olur, ama üretimde hiçbir şey yapmaz. Bunu bir daha sessizce oluşmaz hale getirmek
için `consistency_audit.py`'ye 8. kural eklendi:

`src/Modules/**` içindeki bir modül kaydı (`context.Register*<...>`) tarafından kaydedilen bir tür,
`src/Host/**` dosyalarından başlayan hiçbir başvuru yolu ile ulaşılamıyorsa denetim HATA verir. Yöntem:
`src/**/*.cs` üzerinde tür-adı başvuru grafiği kurulur; her DI kaydı için (servis arayüzü → gerçekleme)
kenarı eklenir (kapsayıcı gerçeklemeyi arayüz üzerinden çözer); grafik her Host dosyasından gezilir.
Test kodu hiçbir zaman çağıran sayılmaz.

Bilerek ulaşılamayan tür `tools/consistency-audit/unreachable_services_allowlist.json` içinde
bir görev referansıyla listelenmelidir; referanssız giriş hata verir; artık ulaşılabilir olan ya da
kaydı kalkmış giriş "bayat" sayılıp hata verir (liste yalnız kısalır).

**Bugünkü borç (dürüst kayıt):** kapı ilk çalıştığında Faz 1/Faz 2 dışında 38 ulaşılamayan kayıtlı tür
buldu (porsiyon rezervasyonu, rezervasyon bakiye izdüşümü, reçete sürümleme/birim dönüşümü, fire kaydı,
stok hareket defteri, iade niyeti, hesap ödeme kapanış izdüşümü, mutfak yönlendirme servisi, oturum
rotasyonu, sır çözümleyici, yeniden şifreleme). Hepsi `V1-RMD-273` (Planned) referansıyla listeye alındı;
liste bugünkü durumu gizlemez, gelecekteki yenilerini engeller.

## Owned surface

- `plan/v1/remediation/V1-RMD-272-reachability-gate-for-registered-services.md`
- `tools/consistency-audit/unreachable_services_allowlist.json`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py
  (yalnız 8. kural: erişilebilirlik kapısı)

## In scope

1. 8. kural, izin listesi, bayat/referanssız giriş denetimi.
2. Mevcut ulaşılamayan türlerin (38) görev referansıyla listeye alınması.

## Out of scope

- Listedeki servislere çağıran eklemek (`V1-RMD-273`).
- Host dışı (yalnız modüller arası) çağrıların üretimde çalıştığını kanıtlamak: kural yalnız
  "Host'tan ulaşılabilir mi" sorusunu cevaplar; ulaşılabilir olması yeterli kanıt değildir, yalnız gereklidir.
- Yansıma ya da adla değil dize ile çözülen kayıtları yakalamak (bilinen kör nokta).

## Dependencies

- V1-RMD-265
- V1-RMD-266
- V1-RMD-267
- V1-RMD-268
- V1-RMD-269
- V1-RMD-270
- V1-RMD-271

## Acceptance evidence

- `python tools/consistency-audit/consistency_audit.py`: temiz (38 izinli tür, hepsi `V1-RMD-273` referanslı).
- **Mutasyon kontrolü:** izin listesinden bir tür çıkarılıp var olmayan bir tür eklenince denetim 2 ihlalle
  kırıldı (biri "ulaşılamıyor", biri "bayat giriş"); liste geri konunca temiz.
- Bu görevin öncesinde ölü olan Faz 1 ve Faz 2 türleri (`AccountRecoveryService`, `DiagnosticBundleService`,
  `RetentionExecutionService`, `OffsiteBackupUploadService`, `RestoreVerificationOrchestrator`,
  `PaymentReconciliationScanner`, `PaymentSettlementReportService`) artık kapıyı listesiz geçiyor.
- `python tools/plan-audit/plan_audit_tool.py validate` çalıştırıldı.

## Handoff

- None
