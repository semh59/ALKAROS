# V1-RMD-479 - Kasa sayımı isteği tekrarlanınca ikinci satır yazılmaması

Ağ zaman aşımından sonra aynı sayım isteğinin tekrarı artık ikinci satır yazmaz.

## Değişiklik

- `PostgresCashSessionRepository.RecordCountAsync`: tek SQL ile, aynı oturum + sayan + tutar + not için son 30 saniyede yazılmış bir sayım varsa onun kimliği döner, yoksa yeni satır yazılır.
  Anahtar ya da migration yok. Farklı tutar, sayan ya da not (ya da 30 saniyeden sonraki yeniden sayım) yeni satırdır.
- Kasadaki tutarı etkilemez (beklenen nakit defterden gelir); düzeltilen yalnız denetim izinin çoğalmasıdır.

## Kanıt

- `green-cash.log`: SessionLifecycle 15/15 (3 yeni: tekrar tek satır ve aynı kimlik, farklı tutar/sayan/not dört satır, 30 saniye sonrası yeniden sayım iki satır). `green-host.log`: CashSession Host testleri 22/22.
- `mutation-cash.log` (tekrar kontrolü kaldırıldı) ve `mutation-window.log` (pencere 30 güne çıkarıldı): ilgili test kırmızı; dosya geri alındı, hash özdeş.

## Açık kalan

- Eşzamanlı iki aynı isteğin tam aynı anda gelmesi yarışı kapatmaz (aralıkta iki satır yazılabilir); sayım denetim izi olduğundan unique anahtar için migration eklenmedi.
