# V1-RMD-276 - Ödemesi tamamlanan hesap "Ödendi" durumuna geçer

- Task ID: V1-RMD-276
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik borcu incelenirken bulunan en önemli hata: çalışan uygulamada HİÇBİR kod bir
hesabı `Allocated`/`PartiallyPaid`/`Paid` durumuna geçirmiyordu (veritabanında tetikleyici de yok).
Tahsilat ekranı "Hesap Ödendi" gösteriyor, ama `billing.bills` satırı `Open` kalıyor, `closed_at`
yazılmıyor, tahsis/ödenen toplamlar 0 kalıyordu. `V13-ALC-002` "son Bill kapanış durumu"nu kapsam dışı
bırakmıştı ve bunu üstlenen başka görev yoktu (`V13-FSC-002` mali politikaya bağlı).

Bu görev `IBillClosureService` ekler (`Billing.PaymentClosure`): onaylı ödemeler hesabı tam kapsıyorsa
ve çözülmemiş (Pending/Unknown/ReconciliationRequired) ödeme yoksa hesap `Open → Allocated → Paid`
geçişini yapar, tahsis/ödenen/para üstü toplamlarını ve `closed_at`'ı yazar. Idempotenttir (zaten
ödenmiş hesap değişmez), iptal edilmiş hesabı kapatmaz, eşzamanlı değişiklikte (iyimser kilit) yeniden
değerlendirir. Modül artık `ModuleRegistry`'de ve Host projesinde.

Genel tahsilat (`/tenders/`) ve nakit tahsilat (`/cash-tender`) uç noktaları başarılı tahsilattan sonra
kapatmayı dener; tahsilat yanıtı `billClosed` alanını taşır. Kapatma bilerek en-iyi-çabadır: para zaten
kaydedildiği için hesap durumunu çevirememek başarılı bir ödemeyi hataya çevirmemeli; hesap açık kalır ve
sonraki tahsilat/kapatma denemesi tamamlar.

**Bilerek YAPILMAYANLAR (karar bekliyor / ayrı görev):** masa otomatik boşaltılmaz ya da "Temizleniyor"a
alınmaz (elle kalır); mali belge (e-Adisyon) üretimi ve `V13-FSC-002` kapanış kapısı bu noktaya sonradan
bağlanır; kısmi ödemede `PartiallyPaid` durumu kullanılmaz (durum makinesi `Allocated` sonrası kısmi
ödemeyi ayrıca tanımlıyor; bu görev yalnız tam ödemeyi kapatır).

## Owned surface

- `plan/v1/remediation/V1-RMD-276-paid-bill-closes.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/IBillClosureService.cs
  (V13-ALC-002 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillClosureService.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureModule.cs
  (yalnız yeni servisin kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/Bill.cs
  (V1-BIL-001 sahipliğinde kalır — yalnız `WithPaymentTotals`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj
  (yalnız `Billing.PaymentClosure` proje başvurusu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs
  (yalnız modülün kataloğa eklenmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde kalır — yalnız kapatma çağrısı, yardımcı ve `BillClosed` alanı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde kalır — yalnız nakit tahsilattan sonra kapatma çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md
  (yalnız 5. satırdaki Bill → Payment okuma kenarı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (yalnız katalog modül sayısı 30 → 31)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (yalnız onaylı kenar)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (3 yeni test ve hesap durumu yardımcıları)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  (mevcut nakit tahsilat testine kapanış doğrulaması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-272 sahipliğinde — yalnız ulaşılabilir olan 2 kapanış izdüşümü girişi silindi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tüm packages.lock.json dosyaları (yeni proje başvurusunun geri yükleme çıktısı)

## In scope

1. Kapatma servisi, Host bağlantısı, iki tahsilat uç noktasında kapatma, `billClosed` alanı.
2. Gerçek Postgres HTTP testleri, Cashier E2E'nin bozulmadığının doğrulanması.

## Out of scope

- Masa boşaltma / temizleme, sipariş kapatma, mali belge, yeniden açma akışı arayüzü.
- Kısmi ödeme durumu (`PartiallyPaid`).

## Dependencies

- V13-ALC-002
- V1-BIL-001
- V13-PAY-005
- V13-CSH-004
- V1-RMD-258

## Acceptance evidence

- Host.Experience.PaymentTender (UTF8 Postgres 18): 24/24. Yeni: 40 TL kısmi EFT sonrası hesap `Open`,
  60 TL ikincisi sonrası `Paid`, tahsis 100 / ödenen 100, `closed_at` dolu, yanıtta `billClosed=true`; ödenmiş
  hesaba yeni tahsilat 409; yalnız çözülmemiş kart denemesi olan hesap `Open` kalır.
- Host.Experience.CashSession: 14/14; 80 TL nakit tahsilat 80 TL'lik hesabı `Paid` yapar.
- **Mutasyon kontrolü:** kapatma servisinde kayıt satırı devre dışı bırakılınca yeni test `Paid` beklerken
  `Open` gördü; geri alınınca geçti.
- Host bileşim testleri 161/161 (katalog sayısı güncellendi); Cashier E2E (gerçek Host + Chromium) 29/29 bozulmadı; modül sınır testleri 9/9; PaymentClosure modül
  testleri 13/13; `consistency_audit.py`, `plan_audit_tool.py validate`, `project_manifest_tool.py` temiz.

## Handoff

- None
