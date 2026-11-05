# V1-RMD-835 - Aramada büyük I / noktasız ı katlaması: kasa ve yönetim ekranları

- Task ID: V1-RMD-835
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-828'de garson uygulamasında kapatılan hatanın kalanı: tr-TR küçültmede büyük "I" noktasız "ı" olur, İngilizce klavyedeki "i" onu bulamaz ("isparta" aramasında "Isparta" çıkmaz). Aynı arama katlaması kasa (hızlı satış ürün araması, React ve vanilla kasa), katalog, çevrimiçi menü ve masa ekranlarında da var. Düzeltme: React ekranları tek ortak yardımcıyı (`foldSearch`) kullanır; vanilla kasa betiği modül içe aktaramadığı için kendi tek satırlık eşini taşır.

## Owned surface

- `src/Clients/PosTerminal/src/search.ts`
- `src/Clients/PosTerminal/src/search.test.ts`
- `src/Clients/PosTerminal/src/routes/Cashier.search-fold.test.tsx`
- `evidence/V1-RMD-835/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — ürün araması.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx (V1-RMD-097) — kayıt araması.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (V1-RMD-097) — arama testi.
  - src/Clients/PosTerminal/src/features/online-menu/OnlineMenuTab.tsx (V1-RMD-567) — ürün araması.
  - src/Clients/PosTerminal/src/features/online-menu/OnlineMenuTab.test.tsx (V1-RMD-462) — arama testi.
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx (V1-RMD-502) — masa araması.
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx (V1-RMD-502) — arama testi.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CUI-004) — ürün araması.
  - tests/Clients/StaticApps/cashier-search-fold.test.js (V1-RMD-109) — kasa araması testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; her ekran için mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.

## Out of scope

- Aksanlı harflerin (ş ğ ç ö ü) katlanması; sunucu tarafı arama; garson uygulaması (V1-RMD-828'de bitti).

## Dependencies

- V1-RMD-828

## Acceptance evidence

1. PosTerminal vitest tam paket, tsc ve StaticApps tam paket yeşil: "isparta", "ISPARTA", "ıspar" ve "İSTANBUL" beş ekranda doğru kaydı bulur.
2. Mutasyon: her ekranda katlama kaldırılınca test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
