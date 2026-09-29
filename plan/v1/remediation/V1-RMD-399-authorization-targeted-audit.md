# V1-RMD-399 - Yetkilendirme hedefli derin denetimi (kimlik doğrulama → oturum → rol/izin → grant/delegasyon → uç nokta yetkisi → sahiplik)

- Task ID: V1-RMD-399
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

Repo public olduğu için yetkilendirme yüzeyini V1-RMD-393/398 yöntemiyle, gerçek PostgreSQL üzerinde çalışan probe
testleriyle denetlemek: giriş/PIN/kilit açma ve oturum yaşam döngüsü → rol ve izin kataloğu → grant isteği,
onay, delegasyon ve dört göz ilkesi → her HTTP uç noktasının doğru izni ve doğru oturum türünü istemesi → kaynak
sahipliği (terminal, kasa oturumu, garsonun kendi siparişi, hesap) → yönetim uç noktaları. Önce `docs/domain/
authorization-model.md` ve IAM görev kararları taranır; bilinçli kararlar bulgu sayılmaz. Üretim kodu değiştirilmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-399-authorization-targeted-audit.md`
- `evidence/V1-RMD-399/**`

## In scope

- Kapsam matrisi Goal'deki her adımdan kod okunmadan önce çıkarılır; her adım en az bir probe ile sınanır
  (V1-RMD-393/398 kalibrasyonlarının ikisi de matrisin atladığı adımda kaçırdı).
- Uç nokta envanteri: tüm `Map*` rotaları, istedikleri oturum türü ve izin kodu makinece çıkarılır; izin istemeyen
  mutasyon uç noktaları listelenir.
- Kör kalibrasyon, mutasyon denetimi ve bağımsız çürütme.

## Out of scope

- Üretim kodu, test projesi veya migration değişikliği; ağ/TLS altyapısı; dış entegrasyon kimlik bilgileri.

## Dependencies

- V1-RMD-398

## Acceptance evidence

- `evidence/V1-RMD-399/coverage-matrix.md`: kod okunmadan önce yazılan 25 satırlık matris (commit `a6859e23`).
- `evidence/V1-RMD-399/probes/`: 25 probe (gerçek sunucu + bütün migration'lı PostgreSQL 18, gerçek giriş ucu);
  kör koşu `run-1-blind.log`: 16 geçti, 9 başarısız (bulgu).
- `evidence/V1-RMD-399/endpoint-inventory.tsv`: 321 rota×yöntem, oturumsuz ve izinsiz oturumla gerçek HTTP sonucu;
  oturumsuz ulaşılabilen özel rota yok, izinsiz oturumda korumayı geçen 8 rota (kasa oturumu 7, kart tahsilatı 1).
- `evidence/V1-RMD-399/mutation/`: 10 koruma mutasyonu, 10/10 öldürüldü.
- `evidence/V1-RMD-399/calibration/`: mühürlü kör tohum; karar mühür açılmadan "yakalandı" yazıldı, doğru çıktı.
- `evidence/V1-RMD-399/refutation.md`: bağımsız çürütme; 6 bulgu doğrulandı, 1 karar sorusuna düşürüldü.
- `evidence/V1-RMD-399/REPORT.md`: bulgular H-01..H-07, Q-01, N-1, N-2, S-01, T-01 ve düzeltme önerileri.
- Semih için özet: garson ve mutfak personeli kasa çekmecesini açıp para çıkışı yazabiliyor (Yüksek); şef garson
  onay ekranına ulaşamıyor; çevrimdışı mutabakat istemcinin söylediği rolü kullanıyor; 8 saatlik oturum kararı
  girişte uygulanmıyor; delegasyon oluşturulamıyor; personel pasifleştirilemiyor.

## Handoff

- None
