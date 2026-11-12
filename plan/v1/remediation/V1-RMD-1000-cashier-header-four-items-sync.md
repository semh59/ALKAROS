# V1-RMD-1000 - Kasa üst başlığı: Masalar, Bekleyen hesaplar, Kasa durumu, Diğer ve bağlantı göstergesi

- Task ID: V1-RMD-1000
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyerin iki başlığı da dört öğe taşır: Masalar, Bekleyen hesaplar (rozetli), Kasa durumu ve Diğer menüsü; yanında bağlantı göstergesi bulunur. Hızlı satış ekranının başlığı (977 görevinin duraklatılmış çalışması) Masalar ile Hızlı satış geçişi, Bekleyen hesaplar, Kasa durumu ve kullanıcı menüsünü (Diğer) gösterir; Mutfak, Cari hesaplar, Ürün menüsü, Yönetim, Müşteri ekranını eşleştir ve Çıkış menünün içine girer; bağlantı sorununda uyarı görünür. Masa planı kabuğunda ana gezinme Masalar ve Bekleyen hesaplar ile sınırlıdır, diğer yollar Diğer menüsündedir; başlıkta bağlantı ve son güncelleme göstergesi vardır. Masa planı ekranında Hızlı satış çerçeveli ikincil düğmedir. 977 bu görevle kapanır.

## Owned surface

- `evidence/V1-RMD-1000/**`
- `src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.low-items.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.management-link.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.help-alerts.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.kitchenless.test.tsx
  - src/Clients/PosTerminal/src/stale.test.ts
  - src/Clients/PosTerminal/src/design-system/Icon.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  - src/Clients/PosTerminal/src/shell/models.ts
  - src/Clients/PosTerminal/src/shell/shell.css
  - src/Clients/PosTerminal/src/shell/layout-contract.test.ts
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Hızlı satış başlığında dört öğe ve Diğer menüsü, bağlantı uyarısı (kırmızı test, mutasyon)
2. Kabukta ana gezinme Masalar ve Bekleyen hesaplar, geri kalanı Diğer; rozet; başlıkta bağlantı ve son güncelleme göstergesi
3. Masa planında Hızlı satış düğmesi

## Out of scope

- Sağ panel (1001), ödeme ekranı (1002), koyu tema

## Dependencies

- V1-RMD-999

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None
