# V1-RMD-029 - Seat-aware order and bill-split workspace

- Task ID: V1-RMD-029
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Masa sandalyelerini veya sınırlı kişi etiketlerini kullanan; ödeme yürütüldüğünü iddia etmeden eşit, ürün/miktar ve
tutar dağıtımını destekleyen kurtarılabilir hesap bölme çalışma alanı sunmak.

## Owned surface

- PO:2026-08-29 kararıyla Billing features yüzeyi V1-RMD-041'e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-029/**`

## Dependencies

- V1-RMD-027
- V1-RMD-028
- V1-RMD-016

## Acceptance evidence

- Çalışma alanı authoritative hesap kalemlerini, ödenecek/vergi toplamlarını, sandalye/kişi sahiplerini, dağıtılmış ve
  dağıtılmamış toplamları ve açık eşit/ürün/tutar modlarını gösterir. Miktar ataması paylaşılan ve kısmi ürünleri
  destekler.
- İnceleme, gönderimden önce fazla dağıtımı, pozitif olmayan sahipleri, tutar uyuşmazlığını ve desteklenmeyen ödenmiş
  durumları engeller; sunucu doğrulaması authoritative kalır. Busy/error/offline/stale/unauthorized/conflict dağıtım
  taslağını korur.
- Başarılı kayıt/sıfırlama authoritative dağıtımları yeniden yükler ve sonucu duyurur. Hiçbir UI metni, animasyon veya
  optimistic state ödemenin başarılı olduğunu iddia etmez.
- Component/API testleri, tüm PosTerminal testleri, typecheck ve evidence kapsamlı build exit code `0` verir; otonom
  klavye, focus, 44x44, kontrast, overflow ve responsive kanıtı masaüstü drawer ile mobil tam ekran drill-down'ı kapsar.

## Handoff

- V1-RMD-030
