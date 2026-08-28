# V1-RMD-011 - WebPrototype security headers, visual quarantine and production decoupling

- Task ID: V1-RMD-011
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

WebPrototype mock istemcisini üretimden belirgin şekilde ayrıştırmak; kalıcı karantina banner'ı, CSP başlıkları ve
dokunma hedefi standartlarını pekiştirmek.

## Owned surface

- `src/Clients/WebPrototype/index.html`
- `src/Clients/WebPrototype/app.js`
- `src/Clients/WebPrototype/styles.css`
- `src/Clients/WebPrototype/mock-runtime.js`
- `src/Clients/WebPrototype/tests/app.security.test.js`
- `src/Clients/WebPrototype/tests/mock-runtime.test.js`
- `src/Clients/WebPrototype/tests/ui.accessibility.test.js`
- `evidence/V1-RMD-011/**`

## In scope

- `WebPrototype` sayfasına kalıcı ve açık karantina ve simülatör uyarısı eklemek.
- Prototip butonlarında 44x44px minimum dokunma hedefi standardını sağlamak.
- Güvenlik başlıkları, CSP kuralları ve offline mock dayanıklılık testlerini doğrulamak.
- Üretim `PosTerminal` istemcisi ile prototip arasında kesin izolasyonu garanti altına almak.

## Out of scope

- Production `PosTerminal` veya backend Host kodunu değiştirmek.

## Dependencies

- V1-GOV-006
- V1-RMD-010

## Deliverables

- Karantina rozeti ve güvenlik iyileştirmeleri içeren `WebPrototype`.
- Güncellenmiş ve geçen Node test takımı.

## Acceptance evidence

- `node --test src/Clients/WebPrototype/tests/*.test.js` exit code 0 verir; test sayısı sabit başarı ölçütü değildir.
- 320px dahil mobil/desktop yüzeylerde viewport üstünde kalıcı, açık `MOCK / NOT PRODUCTION` karantinası görünür;
  production API/contract/credential kullanımı ve accidental shipping build referansı negatif testle reddedilir.
- CSP `script-src`, `object-src`, `base-uri`, `frame-ancestors` ve bağlantı sınırlarını enforce eder; inline bypass veya
  yalnız meta varlığı kabul edilmez. Tüm interactive target'lar gerçek bounding box'ta >=44x44 px olur.
- Online/offline metinleri tutarlı; loading/error/retry/conflict mock state'leri ayrı görüntülenir ve console/network,
  accessibility ve focus-order kanıtı kaydedilir.
- `evidence/V1-RMD-011/**` altında test çıktıları saklanır.

## Handoff

- V1-RMD-021
