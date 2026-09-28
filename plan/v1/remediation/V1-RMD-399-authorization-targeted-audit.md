# V1-RMD-399 - Yetkilendirme hedefli derin denetimi (kimlik doğrulama → oturum → rol/izin → grant/delegasyon → uç nokta yetkisi → sahiplik)

- Task ID: V1-RMD-399
- Status: InProgress
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

- `evidence/V1-RMD-399/REPORT.md`: karar taraması, uç nokta envanteri, invariantlar, kapsam matrisi, bulgular,
  mutasyon, kalibrasyon ve çürütme sonuçları.
- `evidence/V1-RMD-399/probes/`: probe kaynakları ve gerçek koşu çıktıları.

## Handoff

- None
