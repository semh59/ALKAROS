# V1-RMD-851 - Statik istemci testleri: tam koşuda 5 sn zaman aşımı (kök neden: soğuk modül yükü)

- Task ID: V1-RMD-851
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9: `tests/Clients/StaticApps` tam koşusunda `waiter-app.test.js` testleri varsayılan 5 sn test süresini aşabiliyor. Ölçüm (evidence): her test dosyası uygulamanın modül grafiğini `vi.resetModules()` ile baştan yükler; ilk test soğuk yüklemeyi öder (tek başına ~0,5 sn, işlemciyi doyuran 24 yük işlemiyle ~2,2 sn) ve tam koşuda en ağır testler 4,6 sn'ye çıkar; 5 sn varsayılanı gerçek yükleme süresine pay bırakmıyor. İşçi sayısını düşürmek (gizleme) yerine test ve kanca süreleri, ölçülen soğuk yükleme maliyetine göre `vitest.config.js` içinde tek yerde belirlenir; yükte pay bırakıldığı kanıtlanır.

## Owned surface

- `evidence/V1-RMD-851/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/vitest.config.js — test ve kanca süresi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce ölçüm: tek başına, tam koşuda ve 24 yük işlemiyle test süreleri (json raporlar); varsayılan sürenin %20 kısıtıyla (4 sn) doymuş makinede tam koşu düşer (kırmızı).
2. Süreler yapılandırmada ölçülen en kötü durumun katı olacak biçimde belirlenir; işçi sayısı sınırlanmaz.
3. Aynı yükte tam koşu yeşil; mutasyon: yapılandırma varsayılana döndürülünce aynı kısıtlı koşu düşer.

## Out of scope

- Testlerin kendi iç `waitFor` süreleri, uygulama kodu, işçi sayısı.

## Dependencies

- None

## Acceptance evidence

1. Tam StaticApps koşusu exit 0 (yüksüz ve 24 yük işlemiyle); çıktılar `evidence/V1-RMD-851/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
