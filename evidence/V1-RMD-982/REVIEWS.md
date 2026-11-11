# V1-RMD-982 incelemeleri

## 1. qa-backend
Bulgular: MapAsync iptal belirtecini gecirmiyor (Orta); her okumada ek sorgu (Orta/Dusuk); diger donus noktalari testsiz (Dusuk).
Aksiyon: iptal belirteci 7 cagri noktasindan gecirildi. Ek sorgu kabul edildi (tek indeksli SELECT, kalem yoksa atlanir). Acik: GetAsync/SaveEqual/merge yollarinda alan testi yok (hepsi ayni MapAsync'ten geciyor).

## 2. qa-api-uclar
Bulgular: sozlesme geriye uyumlu, ApiConventions beklemiyor; test null/yok ayrimini yapmiyor (Orta); ham kod Turkce etikete cevrilmeden ekrana basilmamali (Dusuk, istemci gorevi); Billing orders.order_items'i okuyor (mevcut kalip).
Aksiyon: test alanin varligini zorunlu kildi (GetProperty); ApiConventions 17/17 gecti. Acik: istemci (PosTerminal/Cashier) alani henuz gostermiyor.
