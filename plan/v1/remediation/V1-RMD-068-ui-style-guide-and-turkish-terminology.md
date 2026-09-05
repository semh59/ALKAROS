# V1-RMD-068 - UI style guide and Turkish terminology dictionary

- Task ID: V1-RMD-068
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Farklı ajan oturumlarının ürettiği çeviri ve hata mesajı tutarsızlıklarının kök nedenini kapatmak için yazılı bir arayüz stil rehberi oluşturmak. Rehber; Türkçe terim sözlüğü, ham hata mesajı gösterme yasağı, enum ve durum değerlerinin çeviri zorunluluğu ile periyodik İngilizce string sızıntısı tarama yöntemini tanımlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-068-ui-style-guide-and-turkish-terminology.md`
- `docs/UI_STYLE_GUIDE.md`

## In scope

- Türkçe terim sözlüğü tablosu: en az `Catalog` karşılığı `Katalog`, `Unknown` karşılığı `Bilinmeyen` veya `Doğrulanamayan`, `Healthy`, `Degraded`, `Unhealthy`, `Failed` durum karşılıkları.
- Kural: kullanıcıya asla ham `error.message` veya İngilizce enum değeri gösterilmez; her zaman Türkçe karşılığı bulunur.
- Kural: kod, tanımlayıcı, log ve test adı İngilizce kalır; yalnızca kullanıcıya görünen metin Türkçe olur.
- Periyodik tarama yöntemi: `grep` tabanlı İngilizce string sızıntısı denetiminin nasıl ve hangi dosya kümesinde çalıştırılacağı.

## Out of scope

- Mevcut arayüz dosyalarındaki string düzeltmeleri; onlar `V1-RMD-069` ve `V1-RMD-070` görevlerine aittir.
- Otomatik çalışan bir denetim aracı yazmak.

## Dependencies

- V1-GOV-038

## Deliverables

- `docs/UI_STYLE_GUIDE.md` dosyası; terim sözlüğü, hata mesajı kuralı, enum çeviri kuralı ve tarama yöntemi bölümleriyle.

## Acceptance evidence

- `docs/UI_STYLE_GUIDE.md` dosyası dört bölümü de içerir ve markdownlint kurallarına uyar.
- Semih rehberi açar; `Catalog` ve `Unknown` terimleri için Türkçe karşılıkların tabloda listelendiğini doğrular.

## Handoff

- V1-RMD-069
