# V1-RMD-886 sonucu

`DeployFilesTests` `.git` bulamıyordu; kök neden ortam değil, bayat test: V1-RMD-782 (bd143e1b) `.dockerignore`'dan `.git` satırını bilerek kaldırdı (`Directory.Build.props` `git rev-parse HEAD` çalıştırır, `.git` bağlamda yoksa `dotnet publish` kırılır; `.git` yalnız `backend-build` aşamasında, çalışma imajlarına girmiyor). Karşı yönde koruma `tests/Deployment/test_container_contract.py` içinde zaten vardı.
- Düzeltme: test `TheBuildContextExcludesTheEnvironmentFileButKeepsGitForTheCommitStamp` oldu; `.env` dışlanır, `.git`, `.git/`, `**/.git` biçimlerinin hiçbiri dışlanmaz (Python sözleşmesiyle aynı regex).

## Kanıt
- Kırmızı: red-1.log (DeployFilesTests 25 testten 1'i başarısız). Yeşil: green-3.log, final-4.log (25/25, exit 0, son mutasyondan sonra yeniden derlenmiş).
- Mutasyon (mutations.txt): `.env` satırı silinir, `!.env` eklenir, `.git` / `**/.git` / `.git/` eklenir: hepsi öldü. Props'ta commit kalıbını bozan mutant geçersiz çıktı (derleme hatası, test öldürmesi sayılmadı), o assert testten çıkarıldı. Her mutasyon cp+cmp ile geri alındı, yeniden derlendi.

## Açık risk
- Yok. `.git` bağlamda kalmasının imaj güvenliği: son imajlar (api, connector, web) `.git` kopyalamıyor.
