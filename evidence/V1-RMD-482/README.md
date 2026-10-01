# V1-RMD-482 - Denetim bölümü bırakma komutunun düzenli çalıştırılması

`compose.ops.yaml` içine `housekeeping` ile aynı kalıpta `audit-disposal` servisi eklendi (`ops` profili, `--apply`, parola gizli dosyadan). Cron ile çalıştırılır:
`docker compose -f compose.yaml -f compose.ops.yaml run --rm audit-disposal`. Runbook ve go-live listesi (§4a) en fazla altı ayda bir çalıştırmayı ister.

## Kanıt

- `test-contract.log`: dağıtım sözleşme testi (servis çekirdek yığında yok, `ops` profilinde, komut `--apply` ile) yeşil.
- `mutation.log`: servis komutundan `--apply` çıkarılınca test kırmızı; dosya geri alındı, `cmp` özdeş.
- `compose-config.log`: `docker compose ... --profile ops config` servisi sorunsuz çözümler.
- Komutun kendisinin gerçek Postgres kanıtı `evidence/V1-RMD-481/gercek-deneme.log`.

## Açık kalan

- Cron satırını gerçek sunucuda kurmak elle iştir (go-live §4a).
- Aynı dosyadaki 3 eski test (`Dockerfile FROM` sayısı, çekirdek yığındaki parola/entrypoint sayıları) bu görevden önce de kırmızıydı; bu görevin alanı dışında bırakıldı.
