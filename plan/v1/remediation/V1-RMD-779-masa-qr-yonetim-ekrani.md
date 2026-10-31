# V1-RMD-779 - Yönetim ekranında masa QR kodu göster ve yazdır

- Task ID: V1-RMD-779
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V12-QRS-001, Yüksek) ekran yarısı, Semih kararlarıyla. V1-RMD-778 yöneticinin masa QR'ını üretme, yenileme ve iptal etme uç noktalarını açtı; bu görev Yönetim ekranına "Masa QR" bölümünü ekler.

1. Bölüm yalnız `integrations.manage` yetkisi olan oturumda listelenir (yönetici). Masa listesi her masa için QR durumunu (QR yok / QR var, üretim zamanı) gösterir.
2. QR yalnız üretme veya yenileme anında görünür ve yazdırılır; ham token sunucuda saklanmadığı için sonradan gösterilemez. Ekran bunu açık Türkçe söyler: kaybolursa "Yenile" ile yeni QR basılır, eski QR hemen geçersiz olur. Yenileme ve iptal onay ister (basılı eski etiket çalışmaz hale gelir).
3. QR, masa standı etiketi olarak yazdırılır (tarayıcı yazdırma; yalnız QR, masa numarası ve bölge görünür, Yönetim kabuğu yazdırmada gizlenir). Ham token ve adres yalnız bileşen durumunda tutulur: depolama, telemetri ve günlük yok.
4. QR çizimi için onaylı küçük bağımlılık `lean-qr` (MIT, sıfır çalışma zamanı bağımlılığı, v2.7.4 sabit) eklenir; modül matrisinden `<svg>` üretilir, `innerHTML` kullanılmaz. Bağımlılık Semih tarafından onaylanmıştır; lisans MIT, sürüm `package.json` ve `pnpm-lock.yaml` içinde sabitlenir.

## Owned surface

- `evidence/V1-RMD-779/**`
- `src/Clients/PosTerminal/src/features/management-table-qr/**` (yeni)
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management/sections.ts (V1-RMD-466) — yeni bölüm kaydı.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — bölüm sekme ve metinleri.
  - src/Clients/PosTerminal/package.json (V1-RMD-031) — `lean-qr` sürümü sabit.
  - src/Clients/PosTerminal/pnpm-lock.yaml (V1-RMD-031) — kilit kaydı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `features/management-table-qr`: istemci (`/api/v1/management/qr-tables`), bölüm bileşeni, QR SVG bileşeni, yazdırma stili ve testleri.
2. Hata kodlarının Türkçe çevirisi (`RELAY_NOT_READY`, `QR_ALREADY_EXISTS`, `QR_NOT_FOUND`, `TABLE_NOT_FOUND`); ham mesaj ve İngilizce enum ekrana basılmaz.
3. Bölüm kaydı ve metinler; bağımlılık eklenmesi.

## Out of scope

- Sunucu uç noktaları (V1-RMD-778).
- Yeni izin, yeni Yönetim kabuğu ve ham token saklama bu görevin dışındadır.

## Dependencies

- V1-RMD-778

## Acceptance evidence

1. Bölüm bileşeni testleri: yönetici dışı yetkide bölüm listelenmez; liste, üret, yenile (onaylı), iptal (onaylı), röle hazır değil hatası Türkçe; QR yalnız üretme yanıtından sonra görünür ve sayfa verisinde ham adres bulunmaz.
2. QR doğruluğu: üretilen SVG modül matrisi, aynı metni kodlayan referans matrisle eşit.
3. `vitest`, `oxlint` ve `tsc --noEmit` exit 0; yazdırma stilinde Yönetim kabuğu gizlenir (test).
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0; bağımsız ön yüz incelemesi bulguları giderilmiş.

## Handoff

- None
