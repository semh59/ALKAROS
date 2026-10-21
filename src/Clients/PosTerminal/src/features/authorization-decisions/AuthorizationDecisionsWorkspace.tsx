import { useState } from "react";
import { Button, StateMessage } from "../../design-system";
import { commonActions, stateText } from "../../strings";
import {
  authorizationDecisionText as text,
  formatTime,
  permissionLabel,
  reasonLabel,
  roleName,
  type ActiveDelegation,
  type AuthorizationDecisionState,
  type OpenTightening,
  type PendingGrant,
} from "./models";
import "./authorization-decisions.css";

export interface AuthorizationDecisionsWorkspaceProps {
  state: AuthorizationDecisionState;
  pendingGrants: readonly PendingGrant[];
  delegations: readonly ActiveDelegation[];
  tightenings: readonly OpenTightening[];
  onApprove: (grantId: string) => void | Promise<void>;
  onDeny: (grantId: string) => void | Promise<void>;
  onRevokeDelegation: (delegationId: string) => void | Promise<void>;
  onClearTightening: (tighteningId: string) => void | Promise<void>;
  onRefresh: () => void | Promise<void>;
  errorMessage?: string;
  lastUpdated?: string;
}

function money(value: number): string {
  return `₺${value.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function AuthorizationDecisionsWorkspace({
  state,
  pendingGrants,
  delegations,
  tightenings,
  onApprove,
  onDeny,
  onRevokeDelegation,
  onClearTightening,
  onRefresh,
  errorMessage,
  lastUpdated,
}: AuthorizationDecisionsWorkspaceProps) {
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string>();
  async function run(action: () => void | Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionError(undefined);
    try {
      await action();
    } catch (reason) {
      setActionError(reason instanceof Error && reason.message ? reason.message : text.actionFailed);
    } finally {
      setBusy(false);
    }
  }

  if (state === "loading") {
    return (
      <div className="authz-decisions authz-decisions--state" aria-busy="true">
        <StateMessage tone="info" title={text.loadingTitle}>
          <p>{text.loadingBody}</p>
        </StateMessage>
      </div>
    );
  }
  if (state === "unauthorized") {
    return (
      <div className="authz-decisions authz-decisions--state">
        <StateMessage tone="unauthorized" title={stateText.managerUnauthorizedTitle}>
          <p>{text.unauthorizedBody}</p>
        </StateMessage>
      </div>
    );
  }
  if (state === "offline") {
    return (
      <div className="authz-decisions authz-decisions--state">
        <StateMessage tone="offline" title={stateText.offlineTitle}>
          <p>{text.offlineBody}</p>
          <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.1" onClick={() => void onRefresh()}>{commonActions.retry}</Button>
        </StateMessage>
      </div>
    );
  }
  if (state === "error") {
    return (
      <div className="authz-decisions authz-decisions--state">
        <StateMessage tone="error" title={text.errorTitle}>
          <p>{errorMessage ?? stateText.unexpectedError}</p>
          <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.2" onClick={() => void onRefresh()}>{commonActions.reload}</Button>
        </StateMessage>
      </div>
    );
  }

  return (
    <section className="authz-decisions" aria-label={text.title}>
      <header className="authz-decisions__header">
        <div>
          <span className="authz-decisions__kicker">{text.kicker}</span>
          <h2>{text.title}</h2>
          <p>{lastUpdated ? `${text.since} ${formatTime(lastUpdated)}` : text.subtitle}</p>
        </div>
        <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.3" variant="secondary" onClick={() => void onRefresh()}>{commonActions.refresh}</Button>
      </header>

      {actionError && <p className="authz-decisions__error" role="alert">{actionError}</p>}

      <div className="authz-decisions__group" aria-label={text.pendingHeading}>
        <h3>{text.pendingHeading}</h3>
        {pendingGrants.length === 0 ? (
          <p className="authz-decisions__empty">{text.pendingEmpty}</p>
        ) : (
          <ul className="authz-decisions__list">
            {pendingGrants.map((grant) => (
              <li key={grant.grantId} className="authz-decisions__row">
                <div className="authz-decisions__facts">
                  <strong>{permissionLabel(grant.permissionCode)}</strong>
                  <span>{money(grant.amount)}</span>
                  <span>{reasonLabel(grant.reasonCode)}</span>
                  <span className="authz-decisions__muted">{text.requestedBy} {roleName(grant.requesterRoleCode)}</span>
                  <span className="authz-decisions__muted">{text.requestedAt} {formatTime(grant.requestedAt)}</span>
                </div>
                <div className="authz-decisions__actions">
                  <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.4" disabled={busy} onClick={() => void run(() => onApprove(grant.grantId))}>{text.approve}</Button>
                  <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.5" variant="quiet" disabled={busy} onClick={() => void run(() => onDeny(grant.grantId))}>{text.deny}</Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="authz-decisions__group" aria-label={text.delegationHeading}>
        <h3>{text.delegationHeading}</h3>
        {delegations.length === 0 ? (
          <p className="authz-decisions__empty">{text.delegationEmpty}</p>
        ) : (
          <ul className="authz-decisions__list">
            {delegations.map((delegation) => (
              <li key={delegation.delegationId} className="authz-decisions__row">
                <div className="authz-decisions__facts">
                  <strong>{permissionLabel(delegation.permissionCode)}</strong>
                  <span>≤ {money(delegation.limitAmount)}</span>
                  <span className="authz-decisions__muted">{text.until} {formatTime(delegation.expiresAt)}</span>
                </div>
                <div className="authz-decisions__actions">
                  <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.6" variant="quiet" disabled={busy} onClick={() => void run(() => onRevokeDelegation(delegation.delegationId))}>
                    {text.revoke}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="authz-decisions__group" aria-label={text.tighteningHeading}>
        <h3>{text.tighteningHeading}</h3>
        {tightenings.length === 0 ? (
          <p className="authz-decisions__empty">{text.tighteningEmpty}</p>
        ) : (
          <ul className="authz-decisions__list">
            {tightenings.map((tightening) => (
              <li key={tightening.tighteningId} className="authz-decisions__row">
                <div className="authz-decisions__facts">
                  <strong>{permissionLabel(tightening.permissionCode)}</strong>
                  <span>{tightening.recentCount} işlem</span>
                  <span>{tightening.triggerRatio.toFixed(1)}× taban</span>
                  <span className="authz-decisions__muted">{formatTime(tightening.triggeredAt)}</span>
                </div>
                <div className="authz-decisions__actions">
                  <Button data-telemetry-id="b.authorization-decisions.authorizationdecisionsworkspace.7" variant="quiet" disabled={busy} onClick={() => void run(() => onClearTightening(tightening.tighteningId))}>
                    {text.clear}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
