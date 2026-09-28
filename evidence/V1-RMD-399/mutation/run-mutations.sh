#!/usr/bin/env bash
# V1-RMD-399 mutation check: each mutation removes ONE authorization guard in a disposable clone of /src,
# then runs the probe that is supposed to see it. KILLED = the probe (or sweep line) turned red.
# Run inside the SDK container (see ../probes/README.md for the environment variables).
set -uo pipefail
P=evidence/V1-RMD-399/probes/AuthorizationProbes
run() { # id file perl-expression filter kill-grep
  local id=$1 file=$2 expr=$3 filter=$4 kill=$5
  rm -rf /mut399 && git clone -q /src /mut399 && cd /mut399
  mkdir -p evidence/V1-RMD-399/probes && cp -r /src/$P evidence/V1-RMD-399/probes/
  perl -0pi -e "$expr" "$file"
  if git diff --quiet -- "$file"; then echo "$id NOT-APPLIED"; cd /; return; fi
  dotnet restore $P -p:RestoreLockedMode=false -v q >/dev/null 2>&1
  if ! dotnet build $P -c Release --no-restore -v q >/tmp/m399-build.log 2>&1; then echo "$id BUILD-FAILED"; cd /; return; fi
  dotnet test $P -c Release --no-build --filter "$filter" --logger "console;verbosity=detailed" >/tmp/m399-$id.log 2>&1
  if grep -qE "$kill" /tmp/m399-$id.log; then echo "$id KILLED"; else echo "$id SURVIVED"; fi
  cd /
}
run M01 src/Host/Experience/Authorization/AuthorizationDecisionStore.cs 's/pending\.RequesterUserId == approverUserId/pending.RequesterUserId == Guid.Empty/' 'FullyQualifiedName~C1Requester' 'Failed .*C1Requester'
run M02 src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs 's/if \(OwnCheckOnly\.Contains\(request\.PermissionCode\)/if (OwnCheckOnly.Contains(request.PermissionCode + "-mutated")/' 'FullyQualifiedName~C3WaiterCompOnAnother' 'Failed .*C3WaiterCompOnAnother'
run M03 src/Host/DualScreen/DualScreenStore.cs 's/AND s\.revoked_at IS NULL/AND (s.revoked_at IS NULL OR true)/' 'FullyQualifiedName~A2Logout|FullyQualifiedName~A5Manager' 'Failed .*(A2Logout|A5Manager)'
run M04 src/Modules/Identity/Authorization/RoleManagementService.cs 's/(AssignUserAsync\(Guid actorUserId, Guid userId, Guid roleId, CancellationToken cancellationToken = default\)\s*\{)\s*await _authorization\.AuthorizeAsync\(actorUserId, PermissionCodes\.RolesManage, cancellationToken\);/$1/' 'FullyQualifiedName~D2SecondPass' 'Failed .*D2SecondPass'
run M05 src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs 's/if \(budget\.UserId != principal\.UserId\)/if (budget.UserId == Guid.Empty)/' 'FullyQualifiedName~C7OfflineBudgetOfAnother' 'Failed .*C7OfflineBudgetOfAnother'
run M06 src/Host/Experience/Catalog/CatalogManagementEndpoints.cs 's/await _authorization\.AuthorizeAsync\(\s*actorId,/if (actorId == Guid.Empty) await _authorization.AuthorizeAsync(actorId,/' 'FullyQualifiedName~D2EveryMutation' 'D2-PASSED-GUARD\s+POST /api/v1/management/catalog/'
run M07 src/Host/Experience/Tables/TableManagementApplication.cs 's/(MapPost\("\/reservations", async \(.*?)ApplicationPermissions\.TablesReserve/$1ApplicationPermissions.TablesStatus/s' 'FullyQualifiedName~D3WaiterCannotReserve' 'Failed .*D3WaiterCannotReserve'
run M08 src/Host/Experience/Authorization/AuthorizationDecisionStore.cs 's/target\.UserId == managerUserId/target.UserId == Guid.Empty/' 'FullyQualifiedName~C5UserCannotClear' 'Failed .*C5UserCannotClear'
run M09 src/Host/DualScreen/DualScreenStore.cs 's/AND s\.device_id = \@device_id/AND s.device_id LIKE '"'"'cashier:%'"'"'/' 'FullyQualifiedName~E1Session' 'Failed .*E1Session'
run M10 src/Modules/Identity/Authentication/AuthenticationService.cs 's/DefaultMaxFailedAttempts = 5;/DefaultMaxFailedAttempts = 50;/' 'FullyQualifiedName~A1Login' 'Failed .*A1Login'
