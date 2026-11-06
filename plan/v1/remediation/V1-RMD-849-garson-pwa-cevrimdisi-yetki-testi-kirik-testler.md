# V1-RMD-849 - Garson PWA çevrimdışı yetki testi: kırık üç test

- Task ID: V1-RMD-849
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9: `tests/Clients/WaiterPwa/Frontend/test_offline_authorized_void_comp.py` üç testi kırmızı. Kök neden (1) node çıktısı `text=True` ile Windows kod sayfasında çözülüyor, Türkçe mesaj bozuluyor (iki test); (2) `test_confirm_paths_queue_offline_and_reconnect_reconciles` iptal için `authorizeOffline('bills.void', context, 0,` bekliyor, oysa V1-RMD-671'den beri kod kalemin brüt tutarıyla (`item ? item.totalPrice : 0`) çağırıyor (void-sent yetkisi aynı tutarla istenir; yeniden denemede eşleşmesi için). Test koda değil, koda uyarlanır; kaynak metni arayan üçüncü test davranışı gerçekten çalıştıran bir teste çevrilir.

## Owned surface

- `evidence/V1-RMD-849/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/WaiterPwa/Frontend/test_offline_authorized_void_comp.py — çıktı kodlaması, bayat dize beklentisi, davranış testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı: 3 test düşüyor (log).
2. Alt süreç çıktısı UTF-8 çözülür; iptal beklentisi güncel çağrıya uyarlanır; çevrimdışı iptalin brüt tutarla kuyruğa alındığını gerçekten çalıştıran test (Node harness) eklenir.
3. Mutasyon: iptal tutarını 0'a çeviren ve bütçe denetimini atlayan iki mutant testi düşürür (cp+cmp ile geri alınır).

## Out of scope

- Garson PWA üretim kodu, diğer Python testleri.

## Dependencies

- None

## Acceptance evidence

1. Dosyadaki testlerin tamamı yeşil (exit 0) ve `tests/Clients/WaiterPwa/Frontend` tam koşusu; çıktı `evidence/V1-RMD-849/`.
2. Mutasyon kanıtı.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
