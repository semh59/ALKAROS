# V1-RMD-752 - Hesap bölme düzeltilmiş tutar belgesini güncelle

- Task ID: V1-RMD-752
- Status: Done
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: documentation
- Surface state: Existing

## Goal

F0908 belge kusurunu gider: V1-RMD-103 içindeki SplitEngine'in düzeltilmiş tutarı kullanmadığı anlatımını tarihsel duruma ayır, güncel koşullu davranışı açıkla. İndirim dağıtımı veya ürün politikası değiştirilmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-752-bolme-tutari-belge-guncelligi.md`
- `evidence/V1-RMD-752/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md

## In scope

- Equal, amount ve custom bölmede adjustment verilince AdjustedPayableAmount/AdjustedTaxTotal, null iken Bill tutarları kullanıldığı anlatılır.
- Host SaveEqualAsync, SaveAmountsAsync ve SaveCustomAsync adjustment taşıması; SaveItemsAsync değişmiş toplamı reddetmesi açıklanır. Tüm bölme türleri destekliyor genellemesi yapılmaz.
- Eski kapsam/karar tarihçesi korunur; yeni politika onayı veya ödeme akışının tamamına doğruluk iddiası çıkarılmaz.
- Belge denetimi kırmızıdan yeşile döner, eski anlatım scratch kopyada geri getirildiğinde tekrar kırılır. Gerçek SplitDesign ve Host Billing süitleri çalıştırılır.

## Out of scope

- Üretim kodu, uygulama testleri, migration, dependency ve diğer plan dosyalarının değişmesi.
- Tarihsel Owned surface, Dependencies veya Acceptance evidence sözleşmesinin yeniden yazılması.
- Yeni dağıtım politikası veya kalem bazlı indirim dağıtımı tasarımı.

## Dependencies

- V1-RMD-751
- V1-RMD-103

## Acceptance evidence

1. Güncel not koşullu payable/tax desteğini, null dalını ve kalem bölme sınırını gerçek kodla eşler; eski bugün/hâlâ iddiası güncel mekanizma gibi sunulmaz.
2. Belge denetiminin kırmızı, yeşil ve eski metinle tekrar kırmızı çıktıları gerçek exit code ile evidence altında bulunur.
3. SplitDesign ve gerçek PostgreSQL Host Billing süitleri ile ilgili build sonuçları kayıtlıdır. Başarısız/çalıştırılmamış kontrol varken kapanmaz; yeni üretim/test kusuru ayrı sahiplikle ele alınır ve paketi açık tutar.
4. İki bağımsız inceleme belge/kod uyumunu, tarihçe korumasını ve test kapsamını kontrol eder; tüm notlar giderilir.
5. Plan, consistency ve task scope kontrolleri gerçek exit code ile kaydedilir; başlangıç değişiklikleri korunur.
6. Elle kontrol: nottaki üç engine çağrısını ve SaveItemsAsync ret koşulunu izle; adjustment=null yolunun özgün Bill tutarını kullandığını karşılaştır.

## Handoff

- V1-RMD-739
