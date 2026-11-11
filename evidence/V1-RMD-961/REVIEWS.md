# V1-RMD-961 Reviews

## ui-turkce-metin-uzmani
- Verified in code: a modifier belongs to a product either directly (modifiers.product_id) or through an assigned group; the first section lists only the direct ones, the groups are a separate section, so the old "atanmadı" sentence was wrong. New heading and sentence match the code.
- Low: the sentence alone does not mention group options -> accepted, the group section sits right below.
- Medium, not changed: the modifier create form has no product picker, so product-specific modifiers can only come from the API or seed data; the section is usually empty.

## qa-test-kalitesi
- Medium: no test for a product that does have its own modifier -> test added (own listed with its price delta, another product's not listed, no "yok" sentence); mutants M3 and M4.
