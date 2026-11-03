#!/bin/sh
cd /d/PROJECT/ALKAROS
X=evidence/V1-RMD-791/791-run.sh
sh $X d-apiconv tests/Architecture/ApiConventions
sh $X d-boundaries tests/Architecture/ModuleBoundaries
sh $X d-errors tests/Host/Errors
sh $X d-manifest tests/Host/MigrationComposition "FullyQualifiedName~Manifest"
echo done > evidence/V1-RMD-791/chain1.done
