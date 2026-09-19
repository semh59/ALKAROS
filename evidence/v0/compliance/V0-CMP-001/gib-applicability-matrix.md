# GİB applicability research — self-declared configuration matrix

> **Task:** V0-CMP-001
> **Status:** Done (via `V0-GOV-065`)
> **Access date:** 2026-08-02 (sources), 2026-09-18 (matrix + approval)
> **Approver:** Semih — Founder/Product Owner — 2026-09-18

## Verified public sources

- GİB YN ÖKC rehberi:
<https://www.gib.gov.tr/duyuru-arsivi/guncel/15314_yeni-nesil-odeme-kaydedici-cihaz-yn-okc-kullanimina-iliskin-rehber-yayimlandi>
- GİB e-Adisyon tebliğ metni: <https://gib.gov.tr/mevzuat/kanun/434/teblig/7885>
- GİB VUK Genel Tebliği No. 509 güncel metni, IV.12:
<https://cdn.gib.gov.tr/api/gibportal-file/file/getFile?objectKey=MEVZUAT_TEBLIGLER%2FUNIVERSAL%2F2026%2FMEVZUAT_TEBLIGLER_2026_VukTeb509_Guncel.pdf>
- GİB YN ÖKC SSS: <https://ynokc.gib.gov.tr/Home/SSS>

## Verified boundary

Güncel 509 metninin IV.12 bölümü, masada servis yapılan ve gerçek usulde
vergilendirilen hizmet işletmelerindeki adisyon belgesinin e-Adisyon olarak
düzenlenmesine ilişkin kuralları; dahil olma yöntemi ve GİB'in yazılı
bildirim/duyuru ile geçiş zorunluluğu getirebilmesini tanımlar. Ayrıca hizmet
tamamlandığında e-Fatura, e-Arşiv Fatura veya YN ÖKC perakende satış fişini
zorunlu kılar. Bu, hedef işletmenin gerçek usul, masada servis, e-Fatura/e-Arşiv
durumu veya GİB bildirimi bulunmadan hedef kapsam sonucunu seçmez.

## Decision (2026-09-18, `V0-GOV-065`)

`V0-CMP-001`'in özgün Blocker'ı, hedef işletme profili için ALKAROS'un
(veya bir mali müşavirin ALKAROS adına) merkezi bir uygulanabilirlik kararı
vermesini istiyordu. Semih'in açık kararıyla bu, **self-declared
configuration** ile değiştirildi: her restoran kendi vergi/hizmet
profilini kendi muhasebecisiyle doğrular ve kurulum sırasında beyan eder;
ALKAROS yalnızca GİB'in kendi yayımlanmış kuralını (aşağıdaki tablo)
doğru uygular.

## Self-declaration matrisi

Kurulum ekranında sorulacak (ve restoranın kendi muhasebecisiyle
doğrulayacağı) üç gerçek olgu, doğrudan `GIB-VUK509-2026` IV.12'den:

| # | Soru (Türkçe, kullanıcıya gösterilecek) | Kaynak |
| --- | --- | --- |
| 1 | İşletmede masada servis veriliyor mu? | IV.12 |
| 2 | İşletme gerçek usulde mi vergilendiriliyor? | IV.12 |
| 3 | İşletme e-Fatura veya e-Arşiv Fatura mükellefi mi? | IV.12 |

### Davranış eşlemesi (2^3 kombinasyon)

| Masada servis | Gerçek usul | e-Fatura/e-Arşiv | Sonuç |
| --- | --- | --- | --- |
| Evet | Evet | Evet | **e-Adisyon zorunlu** — IV.12; hizmet tamamlandığında e-Fatura/e-Arşiv Fatura veya YN ÖKC perakende satış fişi de zorunlu. |
| Evet | Evet | Hayır | e-Adisyon zorunlu DEĞİL (e-Fatura/e-Arşiv mükellefiyeti şartı sağlanmıyor); YN ÖKC perakende fişi rejimi geçerli olabilir — bkz. `GIB-YNOKC-GUIDE`/`GIB-YNOKC-SSS`. |
| Evet | Hayır | (fark etmez) | e-Adisyon zorunlu DEĞİL (gerçek usul şartı sağlanmıyor). |
| Hayır | (fark etmez) | (fark etmez) | e-Adisyon zorunlu DEĞİL (masada servis şartı sağlanmıyor) — adisyon, masada servis olmayan işletmede tanımsızdır. |

- Yalnız ilk satırda ALKAROS'un Token/Beko tabanlı e-Adisyon akışı
  (`V13-FSC-004`, zaten seçilen branch — bkz. `V13-FSC-005`'in
  `NotApplicable` kararı) etkinleştirilir.
- Diğer üç kombinasyonda e-Adisyon akışı hiç etkinleştirilmez; işletme
  normal (kâğıt) adisyon ile çalışmaya devam eder.
- **Kapsam dışı kalan, ayrı soru:** Token/Beko'nun ürettiği basket-kapanış
  belgesinin IV.12'nin öngördüğü e-Adisyon şeklini hukuken tam karşılayıp
  karşılamadığı bu matrisin dışındadır — bu, `V20-CMP-001` (nihai
  compliance sign-off) kapsamında, gerçek para ile çalışmadan önce ayrıca
  doğrulanır.
- Beyan yanlış verilirse (işletme kendi muhasebecisiyle yanlış
  doğrularsa) sorumluluk işletmenin kendisindedir — tıpkı Logo/Mikro/
  Paraşüt gibi mevcut muhasebe yazılımlarının vergi rejimi seçiminde
  olduğu gibi; ALKAROS bunu doğrulamaz, yalnız beyan edilen rejimi doğru
  uygular.
