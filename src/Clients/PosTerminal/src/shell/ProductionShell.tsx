import { useEffect, useId, useRef, useState, type MouseEvent, type ReactNode } from "react";
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
  /** Extra actions (not links) listed at the end of the "Diğer" menu. */
  moreActions?: (close: () => void) => ReactNode;
  children: ReactNode;
}

export function ProductionShell(props: ProductionShellProps) {
  const {
    session, authorization, connectivity, freshness, navigation, activeNavigationId,
    onNavigate, workspaceTitle, workspaceDescription, contextTitle, context, contextOpen = false,
    onContextOpen, onContextClose, headerActions, moreActions, children,
  } = props;
  const [moreOpen, setMoreOpen] = useState(false);
  const moreToggle = useRef<HTMLButtonElement>(null);
  const moreMenu = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!moreOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setMoreOpen(false);
      moreToggle.current?.focus();
    };
    const closeOnOutside = (event: Event) => {
      const target = event.target as Node;
      if (!moreMenu.current?.contains(target) && !moreToggle.current?.contains(target)) setMoreOpen(false);
    };
    window.addEventListener("keydown", closeOnEscape);
    document.addEventListener("mousedown", closeOnOutside);
    return () => {
      window.removeEventListener("keydown", closeOnEscape);
      document.removeEventListener("mousedown", closeOnOutside);
    };
  }, [moreOpen]);

  const handleNavClick = (href: string) => (event: MouseEvent<HTMLAnchorElement>) => {
    setMoreOpen(false);
    if (!onNavigate || !isPlainClick(event)) return;
    event.preventDefault();
    onNavigate(href);
  };
  const mode = useViewportMode();
  const workspaceId = useId();
  const identity = session.status === "authenticated" ? session.identity : null;
  const allowedItems = identity ? allowedNavigation(navigation, identity.capabilities) : [];
  // A role with no primary item (a kitchen-only user) keeps its few items in the main navigation.
  const hasPrimary = allowedItems.some((item) => item.primary !== false);
  const mainItems = hasPrimary ? allowedItems.filter((item) => item.primary !== false) : allowedItems;
  const moreItems = hasPrimary ? allowedItems.filter((item) => item.primary === false) : [];
  const hasMore = moreItems.length > 0 || moreActions != null;
  const moreId = useId();
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
          {identity.terminalName && <IdentityItem label="Terminal" value={identity.terminalName} />}
          <IdentityItem label="Kullanıcı" value={identity.userName} />
          <IdentityItem label="Rol" value={identity.roleLabel} />
        </div>}
        <SyncIndicator connectivity={connectivity} freshness={freshness} />
        {headerActions && <div className="production-shell__header-actions">{headerActions}</div>}
      </header>

      <nav className="production-shell__nav" aria-label="Ana navigasyon">
        {mainItems.map((item) => <NavLink key={item.id} item={item} active={item.id === activeNavigationId} onClick={handleNavClick(item.href)} />)}
        {hasMore && <button data-telemetry-id="b.shell.productionshell.more" ref={moreToggle} type="button" className="production-shell__nav-link production-shell__more-toggle" aria-expanded={moreOpen} aria-controls={moreOpen ? moreId : undefined} aria-current={moreItems.some((item) => item.id === activeNavigationId) ? "true" : undefined} onClick={() => setMoreOpen((open) => !open)}>
          <span className="production-shell__nav-symbol" aria-hidden="true"><Icon name="more" /></span>
          <span className="production-shell__nav-label">Diğer ▾</span>
        </button>}
        {hasMore && moreOpen && <div id={moreId} ref={moreMenu} className="production-shell__more">
          {moreItems.map((item) => <NavLink key={item.id} item={item} active={item.id === activeNavigationId} onClick={handleNavClick(item.href)} />)}
          {moreActions?.(() => setMoreOpen(false))}
        </div>}
      </nav>

      <main id={workspaceId} className="production-shell__workspace" tabIndex={-1}>
        <div className="production-shell__workspace-header">
          <div className="production-shell__workspace-heading"><h1>{workspaceTitle}</h1>{workspaceDescription && <p>{workspaceDescription}</p>}</div>
          {mode !== "wide" && context != null && !contextOpen && <button data-telemetry-id="b.shell.productionshell.1" type="button" className="production-shell__context-trigger" onClick={onContextOpen}>Bağlamı aç</button>}
        </div>
        <AccessContent session={session} authorization={authorization}>{children}</AccessContent>
      </main>

      {showPersistentContext && <ContextDrawer presentation="persistent" title={contextTitle ?? workspaceTitle}>{context}</ContextDrawer>}
      {showContextSheet && <ContextDrawer presentation="sheet" title={contextTitle ?? workspaceTitle} onClose={onContextClose}>{context}</ContextDrawer>}

      <SystemStatus connectivity={connectivity} freshness={freshness} />
    </div>
  );
}

function NavLink({ item, active, onClick }: { item: ShellNavigationItem; active: boolean; onClick: (event: MouseEvent<HTMLAnchorElement>) => void }) {
  return <a className="production-shell__nav-link" href={item.href} onClick={onClick} aria-current={active ? "page" : undefined}>
    <span className="production-shell__nav-symbol" aria-hidden="true"><Icon name={item.icon} /></span>
    <span className="production-shell__nav-label">{item.label}</span>
    {item.badge ? <span className="production-shell__nav-badge">{item.badge}<span className="production-shell__visually-hidden"> bekleyen</span></span> : null}
  </a>;
}

function SyncIndicator({ connectivity, freshness }: { connectivity: Connectivity; freshness: Freshness }) {
  const text = connectivity.status === "offline"
    ? `${connectivityLabels.offline} · son veri ${freshness.label}`
    : connectivity.status === "reconnecting"
      ? connectivityLabels.reconnecting
      : freshness.status === "fresh" ? `Güncel · ${freshness.label}` : `Veri eski · ${freshness.label}`;
  const tone = connectivity.status === "online" && freshness.status === "fresh" ? "online" : connectivity.status === "online" ? "stale" : connectivity.status;
  return <span className={`production-shell__sync production-shell__sync--${tone}`} role="status">{text}</span>;
}

function IdentityItem({ label, value }: { label: string; value: string }) {
  return <div className="production-shell__identity-item"><span>{label}</span><strong title={value}>{value}</strong></div>;
}

function AccessContent({ session, authorization, children }: { session: ShellSession; authorization: RouteAuthorization; children: ReactNode }) {
  if (session.status === "unauthenticated") {
    return <div className="production-shell__access-state"><StateMessage tone="unauthorized" title="Oturum gerekli">
      <p>{session.reason === "expired" ? "Oturumunuzun süresi doldu. Devam etmek için yeniden giriş yapın." : "Bu çalışma alanını açmak için giriş yapın."}</p>
      <Button data-telemetry-id="b.shell.productionshell.2" onClick={session.onSignIn}>Giriş yap</Button>
    </StateMessage></div>;
  }
  if (authorization.status === "forbidden") {
    return <div className="production-shell__access-state"><StateMessage tone="forbidden" title="Bu alana erişim izniniz yok">
      <p>Oturum açık, ancak bu çalışma alanı rolünüz için yetkilendirilmemiş.</p>
      {authorization.onReturn && <Button data-telemetry-id="b.shell.productionshell.3" variant="secondary" onClick={authorization.onReturn}>İzinli alana dön</Button>}
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
      {connectivity.status === "offline" && connectivity.onRetry && <button data-telemetry-id="b.shell.productionshell.4" type="button" className="production-shell__status-action" onClick={connectivity.onRetry}>{commonActions.retry}</button>}
    </div>
    <div className={`production-shell__status-item production-shell__status-item--${freshness.status}`} role={freshness.status === "stale" ? "alert" : "status"}>
      <span className="production-shell__status-symbol" aria-hidden="true"><Icon name={freshness.status === "fresh" ? "check" : "clock"} /></span>
      <span><span className="production-shell__status-label-prefix">Veri: </span><time dateTime={freshness.dateTime}>{freshness.label}</time></span>
      {freshness.status === "stale" && <button data-telemetry-id="b.shell.productionshell.5" type="button" className="production-shell__status-action" onClick={freshness.onRefresh}>{commonActions.refresh}</button>}
    </div>
    <span className="production-shell__status-spacer" />
  </footer>;
}
