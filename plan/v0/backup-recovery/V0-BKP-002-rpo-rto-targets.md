# V0-BKP-002 - Approve RPO and RTO targets

- Task ID: V0-BKP-002
- Status: Done
- Assignee: Semih (product owner)
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.23
- PDF:III.25

## Goal

İşletmenin veri kaybı ve kesinti toleransını ölçülebilir RPO/RTO acceptance target değerlerine dönüştürmek.

## Owned surface

- PO:2026-09-01 kararıyla `docs/recovery/rpo-rto-targets.md` yüzeyi V1-RMD-095'e devredildi; bu historical task closed kalır ve dosyayı bundan sonra V1-RMD-095 sahiplenir ve rakiplere göre kalibre eder.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kritik veri sınıfları, yerel/tesis dışı ritim, geri yükleme önceliği, sorumlu onaylayıcı ve ölçüm yöntemi.
- Yedek kopyalardaki PII saklama/imha, V0-CMP-003 envanterinin tüketimidir.

## Out of scope

- Yedekleme uygulaması veya desteklenmeyen garanti.

## Dependencies

- V0-BKP-001
- V0-CMP-003

## Onay

- Onaylayan: Semih — Founder / Product Owner
- Onay tarihi: 2026-09-01
- Karar: `docs/recovery/rpo-rto-targets.md` içindeki hedef tablosu, restoran POS rakiplerinin (Toast yerel-sync tampon; Square for Restaurants 24-72 saat çevrimdışı; Lightspeed Restaurant tam çevrimdışı; Oracle MICROS Simphony Production RTO/RPO + %99.9 SaaS; Türk pazarı "otomatik yedekleme") fiili taahhütleri ve sektör rehberi (saatlik yedek ideal, PITR ~5 dk RPO, 3-2-1) araştırılarak kalibre edildi. Kabul edilen değerler: devam eden sipariş (istemci) RPO ~0; finansal + mali + denetim RPO 5 dk / RTO 2 saat (V1-RMD-095 WAL arşivleme + PITR); sipariş/mutfak/stok RPO 1 saat / RTO 4 saat (saatlik `pg_dump`); ayarlar RPO 24 saat; %99.9 uptime kuzey yıldızı olarak, tek node V1'in sözleşemeyeceği not düşülerek.
- Ölçülmüş kanıt: `V0-BKP-001` (`evidence/v0/recovery/V0-BKP-001/**` + `V1-RMD-086` disposable PostgreSQL 18 round-trip) ve `V1-RMD-095` canlı yığına karşı uçtan uca PITR transkripti (`evidence/V1-RMD-095/**`).
- Reddedilen alternatif: streaming replikasyon / warm standby ile RPO ~0 — tek mağazalı V1 pilotu için gereksiz maliyet; `V15-BKP` kapsamında kalır.

## Deliverables

- V0-BKP-002 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Hedefler, ölçülen V0 geri yükleme kanıtına göre sayısal olarak onaylanır; ölçülmemiş garantiler mevcut değildir.

## Handoff

- V15-BKP-001
- V15-BKP-002
- V20-DRL-001
