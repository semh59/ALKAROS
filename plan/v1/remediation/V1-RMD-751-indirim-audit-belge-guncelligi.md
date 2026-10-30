# V1-RMD-751 - İndirim audit işlem sınırı belgesini güncelle

- Task ID: V1-RMD-751
- Status: Done
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: documentation
- Surface state: Existing

## Goal

F0388 belge kusurunu gider: V1-RMD-237 görevindeki tarihsel endpoint-sonrası audit tasarımını güncel store transaction akışından ayır. Üretim davranışı ve eski kabul sözleşmesi değiştirilmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-751-indirim-audit-belge-guncelligi.md`
- `evidence/V1-RMD-751/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-237-bill-discount-audit-event-write-path.md

## In scope

- Eski görev kapsamı tarihçe olarak korunur; güncel akış notunda BillingSplitApplication'ın audit bağımlılığını store'a ilettiği, indirim ve audit INSERT çağrılarının aynı connection/transaction içinde commit öncesinde yapıldığı açıklanır.
- Replay yolunun yeni audit üretmeden dönüşü ve void/comp kapsam sınırı doğru ayrılır.
- F0388 için belge kabul denetimi önce eski metinde kırmızı, düzeltmeden sonra yeşil çalıştırılır; eski metin geçici scratch kopyada geri getirilince denetim tekrar kırılmalıdır.
- İlgili gerçek PostgreSQL Host Billing testleri ve Audit EventStore testleri çalıştırılır. Yeni uygulama kodu/test dosyası yazılmaz; bulunan üretim/test kusuru ayrı sahiplik gerektirir ve bu paketi açık tutar.

## Out of scope

- Üretim kodu, uygulama testleri, migration, dependency ve diğer plan dosyalarının değişmesi.
- Tarihsel Owned surface, Dependencies veya Acceptance evidence sözleşmesinin yeniden yazılması.
- V1-RMD-739 programı veya paket 1'in topluca kapanması.

## Dependencies

- V1-RMD-237

## Acceptance evidence

1. Yeni güncellik notu gerçek store/endpoint kodunu dosya ve symbol ile izler; eski tasarım açıkça tarihsel olarak etiketlidir. Güncel durumda endpoint dönüşünden sonra ayrı audit çağrısı yapıldığı ileri sürülmez.
2. Belge denetiminin kırmızı, yeşil ve eski metinle yeniden kırmızı çıktıları gerçek exit code ile evidence altında kayıtlıdır.
3. Gerçek PostgreSQL Host Billing ve Audit EventStore süitleri ile ilgili build sonuçları kaydedilir; başarısız veya çalıştırılmamış kontrol varken kapanmaz. Bu testler tüm uygulamanın veya gerçek cihazın doğrulaması sayılmaz.
4. İki bağımsız inceleme belge/kod uyumunu, tarihçe korumasını ve test kapsamını kontrol eder; tüm notlar giderilir.
5. Plan, consistency ve task scope kontrolleri gerçek exit code ile kayıtlıdır. Başlangıç kullanıcı değişiklikleri korunur; yalnız allowlist farkı kabul edilir.
6. Elle kontrol: V1-RMD-237 güncellik notundan store'a git; adjustment ve audit çağrılarının CommitAsync öncesinde aynı transaction'ı kullandığını izle.

## Handoff

- V1-RMD-752
