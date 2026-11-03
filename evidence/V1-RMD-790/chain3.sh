#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-790/790-run.sh j-errors tests/Host/Errors
sh evidence/V1-RMD-790/790-run.sh j-settings tests/Host/Experience/Settings "FullyQualifiedName~SettingsManagementHttpTests"
sh evidence/V1-RMD-790/790-run.sh j-manifest tests/Host/MigrationComposition/Manifest
echo done > evidence/V1-RMD-790/chain3.done
