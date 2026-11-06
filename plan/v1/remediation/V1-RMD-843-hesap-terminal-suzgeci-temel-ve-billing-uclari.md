# V1-RMD-843 - Hesap terminal süzgeci: temel, sahiplik kaydı ve Billing uçları

- Task ID: V1-RMD-843
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi bulgusu F0897 (PO:2026-11-06, V1-RMD-733'teki "tek işletme, süzgeç yok" notunu geçersiz kılar): bir kasa terminali yalnız kendi hesaplarını ve o hesaplara bağlı ödemeleri görür ve işler; `cash.session.override` yetkisi olan kullanıcı (yönetici, süpervizör) hepsini görür ve işler. Bugün `billId` alan hiçbir uç hesabı terminalle eşleştirmiyor; herhangi bir terminal oturumuyla başka bir terminalin hesabı işlenebiliyor.

Mekanizma (yeni kavram yok, `refund_requests.terminal_id` ile aynı desen): `billing.bills.terminal_id` (boş olabilir) sütunu. Hesabı ilk işleyen terminal sahibi olur (`from-order` ile açılışta, eski satırlarda ilk dokunuşta, tek `UPDATE ... WHERE terminal_id IS NULL`); sonra başka terminal `BILL_OTHER_TERMINAL` (403) alır, yetkili kullanıcı geçer. Ödemeler hesap üzerinden kapsanır (ödemenin hesabı zaten uç tarafından denetleniyor). Sahiplik denetimi uç katmanındadır; kapanış süpürücüsü ve sipariş kapatma servisi süzülmez.

Bu görev: migration, Billing modülünde sahiplik deposu, Host'ta tek denetim sınıfı ve endpoint filtresi, hata eşlemesi (Türkçe) ve `BillingSplitApplication` içindeki uçlar (hesap açma/iptal/indirim/bahşiş/geri alma/düzeltme okuma, iade istek/liste/tamamlama, bölme tasarımı okuma ve yazma). Ödeme alma, makbuz, cari hesaba yazma, nakit tahsilat ve ekranda gösterme uçları V1-RMD-844'te.

Karar notları (koordinatöre raporlanır): sahiplik terminal bazlıdır (kasa oturumu bazlı değil; açık hesaplar vardiya değişiminde de yaşar); eski boş satırlar ilk dokunuşta sahiplenir; "yönetici" `cash.session.override` sahipleridir (süpervizör dahil); hesap başka terminaldeyse 404 değil 403 döner (kasiyer kuyrukta gördüğü hesabın neden açılmadığını anlasın); kuyruk/gün sonu uyarısı gibi işletme geneli listeler süzülmez.

## Owned surface

- `evidence/V1-RMD-843/**`
- `database/migrations/V1/V1-RMD-843/**`
- `src/Modules/Billing/BillFoundation/IBillTerminalOwnerRepository.cs`
- `src/Modules/Billing/BillFoundation/PostgresBillTerminalOwnerRepository.cs`
- `src/Host/Experience/Billing/BillTerminalAccess.cs`
- `tests/Modules/Billing/BillFoundation/PostgresBillTerminalOwnerTests.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata eşlemesi.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103) — filtre bağlama ve kayıt.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — sütun ve geri alma testi.
  - tests/Host/Experience/Billing/ (V1-RMD-040) — eski testlerin güncellenmesi ve yeni BillTerminalAccessHttpTests.cs.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı testler (gerçek Postgres): başka terminalin kasiyeri sahipli hesapta her Billing ucundan `BILL_OTHER_TERMINAL` (403) alır, sahibi terminal geçer, `cash.session.override` sahibi başka terminalden geçer; `from-order` var olan başka terminal hesabını döndürmez; eski boş satır ilk dokunuşla sahiplenilir; iki terminal aynı anda sahiplenmeye çalışınca yalnız biri kazanır. Uç başına bir tablo satırı.
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır): filtreyi bir uçtan kaldırmak o uçun satırını düşürür.
3. Migration ileri/geri kanıtı boş veritabanında; `order.json`, `PhaseBMax`, manifest testi.
4. Eski testlerin yeni kurala göre güncellenmesi.
5. İngilizce kod ve test adları, Türkçe hata metni.

## Out of scope

- Ödeme alma, makbuz, cari hesap, nakit tahsilat ve ekranda gösterme uçları (V1-RMD-844); işletme geneli kuyruk ve gün sonu uyarısı; yeni izin kodu.

## Dependencies

- V1-RMD-842

## Acceptance evidence

1. Billing ve BillFoundation test projeleri tam yeşil; kırmızı/yeşil çıktı `evidence/V1-RMD-843/` altında.
2. Migration ileri/geri kanıtı.
3. Mutasyon: her uçtan filtre kaldırma, sahiplik koşulunu kaldırma, yetkili geçişini kaldırma testleri düşürür.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.
6. Elle deneme: iki kasa terminali açın; birinde bir masanın hesabını açın, diğerinden aynı hesaba ödeme/indirim deneyin ve "Bu hesap başka bir kasada açık" mesajını görün; yönetici oturumuyla aynı işlem geçer.

## Handoff

- None
