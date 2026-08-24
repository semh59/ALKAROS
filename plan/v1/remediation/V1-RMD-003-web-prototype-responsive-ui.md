# V1-RMD-003 - Web prototype responsive UI remediation

- Task ID: V1-RMD-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Goal

Mevcut WebPrototype iş akışlarını değiştirmeden cashier, waiter phone ve waiter tablet yüzeylerini tutarlı, responsive,
klavye ile kullanılabilir ve açık/koyu temada okunabilir tek bir ürün arayüzüne dönüştürmek.

## Owned surface

- `src/Clients/WebPrototype/**`
- `evidence/V1-RMD-003/**`

## In scope

- Ürün dışı prototype kontrollerini operasyon arayüzünden görsel olarak ayırmak ve küçük ekran taşmasını kaldırmak.
- Tables, order entry, menu, kitchen/printer ve waiter akışlarında ortak tipografi, boşluk, button, card, input, chip,
  status ve dialog durumlarını uygulamak.
- Phone, tablet, 1024 px ve geniş desktop yerleşimlerini gerçek reflow ile desteklemek.
- Mevcut etkileşimleri semantic controls, görünür keyboard focus, dialog focus yönetimi ve erişilebilir isimlerle
  kullanılabilir hale getirmek.
- Koyu tema kontrastını ve bütün state/feedback sunumlarını ortak design token'larla düzeltmek.

## Out of scope

- Backend/domain davranışı, yeni ödeme veya sipariş özelliği, API sözleşmesi ve production deployment.
- WebPrototype dışındaki client, test, project, lockfile, build veya global configuration yüzeyleri.

## Dependencies

- V1-RMD-002

## Acceptance evidence

- Mevcut WebPrototype E2E doğrulamaları ve JavaScript syntax kontrolü exit code `0` verir; browser console'da yakalanmamış
  error bulunmaz.
- 390x844, 768x1024, 1024x768 ve 1440x900 viewport'larında yatay taşma olmaz; temel dokunma hedefleri en az 44x44 px
  olur ve ana görevler içerik kaybı olmadan tamamlanır.
- Dialog'lar programatik isim, initial focus, Escape close ve focus restoration sağlar; tables/product cards ile bütün
  icon-only actions keyboard ve screen reader tarafından kullanılabilir olur.
- Açık ve koyu temada normal metin kontrastı en az 4.5:1, büyük metin ve anlamlı UI sınırları en az 3:1 olur.
- Semih; masa açma, ürün/modifier ekleme, siparişi mutfağa gönderme, menu ürün durumunu değiştirme, printer uyarısını
  inceleme ve aynı akışı waiter phone/tablet yüzeyinde sürdürme senaryosunu elle doğrulayabilir.
