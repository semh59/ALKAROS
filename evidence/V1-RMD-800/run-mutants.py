import subprocess, sys, os
R='D:/PROJECT/ALKAROS'
SP=os.environ['TEMP']+'/g5mut'; os.makedirs(SP,exist_ok=True)
DS='tests/Host/MigrationComposition'; DSF='FullyQualifiedName~DualScreenHalfPortionTests'
TD='tests/Host/Experience/Orders/TableDraft'; TDF='FullyQualifiedName~TableDraftHalfPortionHttpTests'
BL='tests/Host/Experience/Billing'; BLF='FullyQualifiedName~AFractionalLineOfAProduct|FullyQualifiedName~AFractionOfAUnitIsRefused|FullyQualifiedName~AHalfPortionOfAProduct|FullyQualifiedName~ALineSoldInFractions|FullyQualifiedName~TheHalfThatIsLeft'
TDS='src/Host/Experience/Orders/TableDraft/TableDraftService.cs'
PR='src/Host/Experience/Billing/PaymentRefunds.cs'
mutants=[
 ('m17-draft-exemption-by-id-only',TDS,'if (!heldQuantities.TryGetValue(i.Id, out var heldQuantity) || heldQuantity != i.Quantity)','if (!heldQuantities.TryGetValue(i.Id, out var heldQuantity))',TD,TDF),
 ('m18-create-ignores-flag','src/Host/Experience/Catalog/CatalogManagementStore.cs','allowsHalfPortion: request.AllowsHalfPortion);','allowsHalfPortion: false);','tests/Host/Experience/Catalog','FullyQualifiedName~AProductCreatedAllowingHalfPortions'),
 ('m19-list-reads-false','src/Host/Experience/Catalog/CatalogManagementStore.cs','reader.GetBoolean(14)),','false),','tests/Host/Experience/Catalog','FullyQualifiedName~AProductCreatedAllowingHalfPortions'),
 ('m20-backfill-wrong-condition','database/migrations/V1/V1-RMD-800/216-product-half-portion.up.sql','quantity <> trunc(quantity)','quantity < 0',DS,'FullyQualifiedName~TheHalfPortionMigrationFlags'),
]
for label,f,old,new,proj,flt in mutants:
    o=f'{SP}/{label}.old'; n=f'{SP}/{label}.new'
    open(o,'wb').write(old.encode()); open(n,'wb').write(new.encode())
    subprocess.run(['sh',f'{R}/evidence/V1-RMD-800/mutate.sh',label,f,o,n,proj,flt],cwd=R)
    d=f'{R}/evidence/V1-RMD-800/mutation-{label}'
    print(label, open(d+'/result.txt').read().strip() if os.path.exists(d+'/result.txt') else 'NO RESULT', 'restored' if os.path.exists(d+'/restored.txt') else 'NOT RESTORED', flush=True)
