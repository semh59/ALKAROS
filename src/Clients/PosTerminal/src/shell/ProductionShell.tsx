import { useEffect, useId, useState, type MouseEvent, type ReactNode } from "react";
import { Button, ContextDrawer, Icon, StateMessage } from "../design-system";
import alkarosLogo from "../design-system/brand/alkaros-logo-on-dark.png";
import { isPlainClick } from "../router";
import { commonActions, connectivityLabels } from "../strings";
import {
  allowedNavigation,
  classifyViewport,
  type Connectivity,
  type Freshness,
  type RouteAuthorization,
  type ShellNavigationItem,
  type ShellSession,
  type ViewportMode,
} from "./models";
import "./shell.css";

function useViewportMode(): ViewportMode {
  const [mode, setMode] = useState(() => classifyViewport(window.innerWidth));
  useEffect(() => {
    const update = () => setMode(classifyViewport(window.innerWidth));
    window.addEventListener("resize", update);
    return () => window.removeEventListener("resize", update);
  }, []);
  return mode;
}

export interface ProductionShellProps {
  session: ShellSession;
  authorization: RouteAuthorization;
  connectivity: Connectivity;
  freshness: Freshness;
  navigation: readonly ShellNavigationItem[];
  activeNavigationId?: string;
  /**
   * When set, a plain left-click on a nav link is handled in-app instead of
   * loading the document. The href is kept, so middle-click / modifier-click
   * still open a new tab (F-6).
   */
  onNavigate?: (href: string) => void;
  workspaceTitle: string;
  workspaceDescription?: string;
  contextTitle?: string;
  context?: ReactNode;
  contextOpen?: boolean;
  onContextOpen?: () => void;
  onContextClose?: () => void;
  headerActions?: ReactNode;
  children: ReactNode;
}

export function ProductionShell(props: ProductionShellProps) {
  const {
    session, authorization, connectivity, freshness, navigation, activeNavigationId,
    onNavigate, workspaceTitle, workspaceDescription, contextTitle, context, contextOpen = false,
    onContextOpen, onContextClose, headerActions, children,
  } = props;

  const handleNavClick = (href: string) => (event: MouseEvent<HTMLAnchorElement>) => {
    if (!onNavigate || !isPlainClick(event)) return;
    event.preventDefault();
    onNavigate(href);
  };
  const mode = useViewportMode();
  const workspaceId = useId();
  const identity = session.status === "authenticated" ? session.identity : null;
  const allowedItems = identity ? allowedNavigation(navigation, identity.capabilities) : [];
  const showPersistentContext = mode === "wide" && context != null;
  const showContextSheet = mode !== "wide" && context != null && contextOpen;

  return (
    <div className="production-shell" data-viewport={mode}>
      <a className="production-shell__skip-link" href={`#${workspaceId}`}>Ana içeriğe geç</a>
      <header className="production-shell__header">
        <div className="production-shell__brand">
          <img className="production-shell__brand-mark" src={alkarosLogo} alt="ALKAROS" />
        </div>
        {identity && <div className="production-shell__identity" role="group" aria-label="Aktif çalışma bağlamı">
          {identity.branchName && <IdentityItem label="Şube" value={identity.branchName} />}
          <IdentityItem label="Terminal" value={identity.terminalName} />
          <IdentityItem label="Kullanıcı" value={identity.userName} />
          <IdentityItem label="Rol" value={identity.roleLabel} />
        </div>}
        {headerActions && <div className="production-shell__header-actions">{headerActions}</div>}
      </header>

      <nav className="production-shell__nav" aria-label="Ana navigasyon">
        {allowedItems.map((item) => <a key={item.id} className="production-shell__nav-link" href={item.href} onClick={handleNavClick(item.href)} aria-current={item.id === activeNavigationId ? "page" : undefined}>
          <span className="production-shell__nav-symbol" aria-hidden="true"><Icon name={item.icon} /></span>
          <span className="production-shell__nav-label">{item.label}</span>
        </a>)}
      </nav>

      <main id={workspaceId} className="production-shell__workspace" tabIndex={-1}>
        <div className="production-shell__workspace-header">
          <div className="production-shell__workspace-heading"><h1>{workspaceTitle}</h1>{workspaceDescription && <p>{workspaceDescription}</p>}</div>
          {mode !== "wide" && context != null && !contextOpen && <button type="button" className="production-shell__context-trigger" onClick={onContextOpen}>Bağlamı aç</button>}
        </div>
        <AccessContent session={session} authorization={authorization}>{children}</AccessContent>
      </main>

      {showPersistentContext && <ContextDrawer presentation="persistent" title={contextTitle ?? workspaceTitle}>{context}</ContextDrawer>}
      {showContextSheet && <ContextDrawer presentation="sheet" title={contextTitle ?? workspaceTitle} onClose={onContextClose}>{context}</ContextDrawer>}

      <SystemStatus connectivity={connectivity} freshness={freshness} />
    </div>
  );
}

function IdentityItem({ label, value }: { label: string; value: string }) {
  return <div className="production-shell__identity-item"><span>{label}</span><strong title={value}>{value}</strong></div>;
}

function AccessContent({ session, authorization, children }: { session: ShellSession; authorization: RouteAuthorization; children: ReactNode }) {
  if (session.status === "unauthenticated") {
    return <div className="production-shell__access-state"><StateMessage tone="unauthorized" title="Oturum gerekli">
      <p>{session.reason === "expired" ? "Oturumunuzun süresi doldu. Devam etmek için yeniden giriş yapın." : "Bu çalışma alanını açmak için giriş yapın."}</p>
      <Button onClick={session.onSignIn}>Giriş yap</Button>
    </StateMessage></div>;
  }
  if (authorization.status === "forbidden") {
    return <div className="production-shell__access-state"><StateMessage tone="forbidden" title="Bu alana erişim izniniz yok">
      <p>Oturum açık, ancak bu çalışma alanı rolünüz için yetkilendirilmemiş.</p>
      {authorization.onReturn && <Button variant="secondary" onClick={authorization.onReturn}>İzinli alana dön</Button>}
    </StateMessage></div>;
  }
  return children;
}

function SystemStatus({ connectivity, freshness }: { connectivity: Connectivity; freshness: Freshness }) {
  const connectionLabel = connectivity.status === "online" ? connectivityLabels.online : connectivity.status === "reconnecting" ? connectivityLabels.reconnecting : connectivityLabels.offline;
  const connectionIcon = connectivity.status === "online" ? "check" : connectivity.status === "reconnecting" ? "refresh" : "offline";
  return <footer className="production-shell__status" aria-label="Sistem durumu" aria-live="polite">
    <div className={`production-shell__status-item production-shell__status-item--${connectivity.status}`} role={connectivity.status === "offline" ? "alert" : "status"}>
      <span className="production-shell__status-symbol" aria-hidden="true"><Icon name={connectionIcon} /></span><strong>{connectionLabel}</strong>
      {connectivity.status === "offline" && connectivity.onRetry && <button type="button" className="production-shell__status-action" onClick={connectivity.onRetry}>{commonActions.retry}</button>}
    </div>
    <div className={`production-shell__status-item production-shell__status-item--${freshness.status}`} role={freshness.status === "stale" ? "alert" : "status"}>
      <span className="production-shell__status-symbol" aria-hidden="true"><Icon name={freshness.status === "fresh" ? "check" : "clock"} /></span>
      <span><span className="production-shell__status-label-prefix">Veri: </span><time dateTime={freshness.dateTime}>{freshness.label}</time></span>
      {freshness.status === "stale" && <button type="button" className="production-shell__status-action" onClick={freshness.onRefresh}>{commonActions.refresh}</button>}
    </div>
    <span className="production-shell__status-spacer" />
  </footer>;
}
