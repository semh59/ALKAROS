# V1-RMD-032 - Integrated designer and release acceptance

- Task ID: V1-RMD-032
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Goal

Container içindeki ürünü restoran operasyonu ve görsel sistem olarak bağımsız doğrulamak; eksik akışları, zayıf
masaüstü kompozisyonunu, mock destekli davranışı veya kanıtsız production iddiasını reddetmek.

## Owned surface

- `docs/audit/V1_DESKTOP_POS_AND_CONTAINER_ACCEPTANCE_2026-08-28.md`
- `evidence/V1-RMD-032/**`

## Dependencies

- V1-RMD-019
- V1-RMD-020
- V1-RMD-028
- V1-RMD-029
- V1-RMD-030
- V1-RMD-033

## Acceptance evidence

- Taze container build'i ve data volume; yönetici, kasiyer, mutfak ve müşteri ekranı rollerinde salon kurulumu,
  sandalyeler, sipariş, rezervasyon, taşıma, birleştirme/ayırma, hesap bölme tasarımı, menü yönetimi, mutfak ve restart
  akışlarını işletir.
- Otonom kanıt 1920x1080, 1440x900, 1366x768, 1280x800, 1024x768, 768x1024, 430x932, 390x844 ve 320x568;
  breakpoint ±1, yüzde 200/400 reflow, reduced motion, focus sırası, accessibility snapshot, console/network, ekran
  görüntüleri ve bounding box'ları kapsar. Critical/serious erişilebilirlik bulgusu sıfırdır.
- Tasarımcı hükmü bilgi hiyerarşisi, mekânsal kavrama, tarama hızı, eylem hiyerarşisi, yoğunluk, tipografi, boşluk,
  durum açıklığı ve hata kurtarmayı değerlendirir. Yalnız responsive ayakta kalma kabul değildir.
- Eksik zorunlu akış, kesilmiş kritik durum, stale mali görünüm, mock yanıt, güvensiz secret/TLS yolu, başarısız
  persistence/restart, critical/high repository bulgusu veya eksik zorunlu dış kanıt nihai hükmü
  `NOT PRODUCTION READY` olarak tutar.

## Handoff

- V1-GOV-004
