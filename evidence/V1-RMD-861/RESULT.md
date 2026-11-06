# V1-RMD-861 sonuç

`docker build --target frontend-build -f deploy/docker/Dockerfile -t alkaros-fe-verify .` çıkış kodu 0 (exit-and-image-listing.txt, ikinci koşu); docker-build.log aynı komutun ilk koşusunun günlüğüdür (günlükte çıkış kodu satırı yoktur).
- `pnpm install --frozen-lockfile` adımı önbellekten geldi (lockfile değişmedi); `pnpm build` (lint adımı 0 hata ve 11 uyarı, log:162; tsc ve vite aynı betik zincirinde hatasız geçti; "built in" log:199) ve `pnpm build:nfc-standalone` (log:213) gerçekten çalıştı.
- Görüntü içeriği (`docker run --rm --entrypoint sh <etiket> -c 'ls /out/app; ls /src/dist-nfc'`) exit-and-image-listing.txt içinde: `/out/app` altında `assets`, `cashier`, `index.html`, `waiter`; `/src/dist-nfc` altında `assets`, `index.html`.
- Geçici etiket silindi (aynı dosyada `docker rmi` çıkış kodu); yalnız build, run --rm ve rmi komutları çalıştırıldı; compose komutu kullanılmadı.
Açık risk: api ve web aşamaları (dotnet, caddy) bu görevde yeniden kurulmadı.
