# V1-RMD-755 - Hesap bölme testlerinin gerçek derlemesini ve kapanış kanıtını tamamla

- Task ID: V1-RMD-755
- Status: Done
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-753'ün kanıtsız kapanışını düzelt: derlenmeyen adjustment çağrılarını gerçek API ile onar, eksik Amount/Custom adjustment ve null senaryolarını çalıştır. Semih'in A kararına göre toplam veya vergi değiştiğinde kalem bölme reddedilir; ikisi de değişmediğinde başarı kanıtlanır. Eski DLL, sıfır test eşleşmesi ve hatalı scope sonucu başarı kabul edilmez.

## Owned surface

- `evidence/V1-RMD-755/**`
- Sınırlı ek: tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs
- Sınırlı ek: tests/Modules/Billing/SplitDesign/PostgresSplitDesignTests.cs
- Sınırlı ek: tests/Host/Experience/Billing/BillingSplitHttpTests.cs
- Sınırlı ek: plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md

## In scope

- Amount ve Custom testleri gerçek imzaya named adjustment ile bağlanır. İndirim, bahşiş ve null dalları parametreli, tutar/vergi beklentileri açık test edilir; ham tutar hedefinin reddi kanıtlanır.
- Host indirimli Equal/Custom kayıtları ve yeniden okumada tutar/vergi korunumu kanıtlanır. V1-RMD-754 regresyon testi korunur; aynı senaryonun tekrarı yerine tutar ve vergi birlikte değişmediğinde gerçek kalem bölme başarısı sınanır.
- Mevcut PostgreSQL testinde tahsis kimlikleri sırasız kümeyi temsil ediyorsa yalnız karşılaştırma sırası düzeltilir; eleman ve adet denetimi korunur.
- V1-RMD-753 metadata durumu eksik kanıt nedeniyle Blocked olur; zorunlu Blocker yalnız bu geçiş için eklenir. Tarihsel Goal, kapsam, acceptance ve bağımlılıklar değiştirilmez. 755 kapanışı ayrı kanıt olarak bağlanır.
- Mutasyonlar yalnız evidence altındaki izole kaynak kopyasında uygulanır; canlı üretim dosyasına yazılmaz. Her mutant ayrı çalışır, build/setup hatası mutant ölümü sayılmaz.

## Out of scope

- Üretim kodu, project/solution/lockfile, migration ve global araç değişiklikleri.
- Paket 02 kabulü veya başka bulguların kapanışı.
- V1-RMD-753 tarihsel kabul sözleşmesinin yeniden yazılması.

## Dependencies

- V1-RMD-754
- V1-RMD-298
- V1-RMD-413

## Acceptance evidence

1. Önce gerçek build ile CS1503 kırmızısı kaydedilir; düzeltmeden sonra SplitDesign ve Host Billing build 0 hata/0 uyarı ve tam süitleri exit 0 olur. Yeni test adlarının tümü TRX içinde çalışmış ve başarılı olmalıdır.
2. Amount/Custom indirim, bahşiş, null tutar/vergi ve ham hedef reddi; Host Equal/Custom indirimli kayıt ve yeniden okuma; toplam ve vergi birlikte değişmediğinde kalem bölme başarısı doğrulanır. Vergisi değişen net-sıfır senaryoda 754 testi 409 ve değişmeyen tahsisleri doğrular.
3. İzole kopyada adjustment tutar/vergi kullanımını ve Host adjustment aktarımını bozma denemeleri yeni testlerde gerçek assertion/domain hatası üretir; her mutant, komut, exit ve kaynak hash'i kayıtlıdır.
4. Başlangıç snapshot'ına göre staged/unstaged/untracked/deleted/renamed delta kesin allowlist içindedir. Doğru görev-local commit aralığı scope exit 0 verir. Boş committed diff pending değişiklik kanıtı sayılmaz.
5. Plan ve consistency exit 0; iki bağımsız son incelemede artık bulgu kalmaz. V1-RMD-753 kapanışı yanlış olduğuna dair düzeltme kanıtı saklanır.
6. Semih senaryosu: indirimli hesapta kişi/serbest bölmeyi kaydet ve tekrar aç; indirimli toplam korunur. Vergisi değişmiş net-sıfır bahşiş/indirimde ürün bölme anlaşılır 409 verir; tutar/vergi değişmemiş hesapta ürün bölme kaydedilir.

## Handoff

- None
