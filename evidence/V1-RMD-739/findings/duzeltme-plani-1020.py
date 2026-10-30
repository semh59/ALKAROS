import json, collections, re
from pathlib import Path
src = Path('D:/project/ALKAROS/evidence/V1-RMD-739/classification/ANA_KAYITLAR.json')
out = Path('C:/Users/semih/AppData/Local/hermes/profiles/alkaros/cache/scratch/ALKAROS-1020-duzeltme-plani.md')
rows = json.loads(src.read_text(encoding='utf-8'))
names = {'EZ': 'Eşzamanlılık ve atomiklik', 'VD': 'Veri doğruluğu ve hesap', 'GY': 'Güvenlik, yetki, KVKK',
         'TE': 'Eksik teslimat / bağlanmamış kod', 'DY': 'Dayanıklılık ve performans', 'UI': 'Arayüz ve erişilebilirlik',
         'BK': 'Kod kalitesi (ölü kod, teknik borç)', 'MK': 'Mimari sınır ve sahiplik', 'KN': 'Kanıt ve test eksik',
         'BD': 'Bayat belge', 'PO': 'Semih kararı gereken iş kuralı/mevzuat'}
waves = [('Dalga 1 — para, veri, güvenlik', ['EZ', 'VD', 'GY']),
         ('Dalga 2 — eksik teslimat ve dayanıklılık', ['TE', 'DY']),
         ('Dalga 3 — arayüz, kod kalitesi, mimari', ['UI', 'BK', 'MK']),
         ('Dalga 4 — test/kanıt ve belgeler', ['KN', 'BD']),
         ('Karar bekleyen — Semih', ['PO'])]
open_rows = [r for r in rows if r['qualification'] in ('aday', 'belirsiz')]
C = collections.Counter
L = ['# ALKAROS — 1.020 bulgu: kategori ve düzeltme planı', '',
     'Kaynak: evidence/V1-RMD-739/findings (27–28 Ekim, commit 4aca38c7). Sınıflama: classification/ANA_KAYITLAR.json.', '',
     '## Özet', '',
     f'- Toplam kayıt: {len(rows)}',
     f'- Düzeltilecek veya doğrulanacak: {len(open_rows)}',
     f'- Bilgi notu (kusur değil, kayıt amaçlı): {sum(r["qualification"]=="bilgi" for r in rows)}',
     f'- Kusur değil (kanıtla elendi): {sum(r["qualification"]=="bulgu-degil" for r in rows)}',
     f'- Daha önce tam kapandığı kayıtla eşleşen: {sum(r["closure_status"]=="kayitli-kanitla-tam-kapanis-eslesmesi" for r in rows)}; kısmi kapanan, kalan kusuru olan: {sum(r["closure_status"]=="kismi-kalan-kusur-var" for r in rows)}', '',
     '## Kural (her bulgu için)', '',
     '1. Bugünkü kodda yeniden doğrula. Rapor 74 commit önce yazıldı; 975 satır yalnız kod okumasıyla iddia edildi.',
     '2. Yanlış çıkarsa kanıtla kapat; düzeltme yapılmaz.',
     '3. Doğruysa: kırmızı test → düzeltme → yeşil test → mutasyon kanıtı → iki bağımsız inceleme → Done.',
     '4. Belge hatası: tarihçe korunur, güncellik notu + belge guard testi.',
     '5. Aynı modülde aynı kategoriden bulgular tek görevde toplanır; farklı davranış ise ayrı görev olur.', '']
for title, cats in waves:
    rs = [r for r in open_rows if r['category'] in cats]
    L += [f'## {title} ({len(rs)})', '']
    for c in cats:
        cr = [r for r in rs if r['category'] == c]
        d = C(r['routing_domain'] for r in cr)
        L.append(f'- **{c} — {names[c]}: {len(cr)} bulgu, {len(d)} alan.** En yoğun: ' + ', '.join(f'{k} {v}' for k, v in d.most_common(6)))
    L.append('')
L += ['## Hazırlık işi (Dalga 1 öncesi)', '',
      '- 16 tam kapanış ve 740–749 görevlerine bağlı 21 kaydı register üzerinde işaretle; kanıtla teyit et.',
      '- Paket 02 (F0944/F0945) yanlış kabulünü geri çek.',
      '- 300 paketlik eski gruplama bırakılır; görevler bu dosyadaki kategori × alan sırasıyla açılır.', '',
      '## Bilinen sınırlar', '',
      '- Kategoriler önceki sınıflandırmadan; satır bazında bağımsız doğrulanmadı. Yeniden doğrulamada kategori değişebilir.',
      '- PO kayıtları 47 soruya bağlı; Semih cevabı olmadan kod yazılmaz.',
      '- Kesin görev sayısı yeniden doğrulamadan sonra belli olur; kategori × alan kaba üst sınırı yaklaşık 250 görevdir, aynı kök nedenler birleşince düşer.']
out.write_text('\n'.join(L) + '\n', encoding='utf-8')
print(out)
for title, cats in waves:
    print(title, sum(r['category'] in cats for r in open_rows))
print('groups', sum(len(C(r['routing_domain'] for r in open_rows if r['category'] == c)) for c in names))
