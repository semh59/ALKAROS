import { useCallback, useMemo, useState } from "react";
import { Button, ModalDialog } from "../../design-system";
import { managementText } from "../../strings";
import type { ManagementSectionProps } from "../management/sections";
import { Notice, PanelView, errorText, usePanel } from "../management/panel";
import { createTableQrClient, type TableQrClient } from "./api";
import type { TableQrRow } from "./models";
import { QrCodeSvg } from "./QrCodeSvg";
import "./management-table-qr.css";
import { formatDateTime } from "../../format";

const t = managementText.tableQr;

type Confirm = { action: "rotate" | "revoke"; row: TableQrRow };
type Shown = { row: TableQrRow; url: string };

const tableName = (row: TableQrRow) => row.zoneName ? `${row.tableNumber} · ${row.zoneName}` : `${row.tableNumber} · ${t.noZone}`;

export function TableQrSection({ client }: Partial<ManagementSectionProps> & { client?: TableQrClient }) {
  const api = useMemo(() => client ?? createTableQrClient(), [client]);
  const [refreshKey, setRefreshKey] = useState(0);
  const load = useCallback(() => api.list(), [api]);
  const [panel, reload] = usePanel(load, t.loadFailed, refreshKey);
  const [confirm, setConfirm] = useState<Confirm>();
  // The address holds the raw token; it lives in this state only until the dialog closes, never in storage.
  const [shown, setShown] = useState<Shown>();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();

  const run = async (row: TableQrRow, action: "issue" | "rotate" | "revoke") => {
    setBusy(true);
    setNotice(undefined);
    setConfirm(undefined);
    try {
      if (action === "revoke") {
        await api.revoke(row.tableId);
        setNotice({ tone: "success", text: t.revoked });
      } else {
        const code = await api[action](row.tableId);
        setShown({ row, url: code.qrUrl });
      }
    } catch (reason) {
      setNotice({ tone: "error", text: errorText(reason, t.actionFailed) });
    } finally {
      // A lost answer may still have changed the server, so the list always reflects its real state.
      setRefreshKey((key) => key + 1);
      setBusy(false);
    }
  };

  return <div className="mtq">
    <p className="mtq__intro">{t.intro}</p>
    <Notice notice={notice} />
    <PanelView title={t.heading} loading={t.loading} failed={t.loadFailed} panel={panel} onRetry={reload}>
      {(rows) => rows.length === 0 ? <p className="mtq__empty">{t.empty}</p> : <table>
        <thead><tr><th>{t.table}</th><th>{t.status}</th><th>{t.actions}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.tableId}>
          <td>{tableName(row)}</td>
          <td>{row.qrIssuedAt ? `${t.hasQr} ${formatDateTime(row.qrIssuedAt)}` : t.noQr}</td>
          <td><div className="mtq__actions">
            {row.qrIssuedAt === null
              ? <Button data-telemetry-id="b.management-table-qr.tableqrsection.1" variant="secondary" disabled={busy} aria-label={`${t.issue} ${tableName(row)}`} onClick={() => void run(row, "issue")}>{t.issue}</Button>
              : <>
                <Button data-telemetry-id="b.management-table-qr.tableqrsection.2" variant="secondary" disabled={busy} aria-label={`${t.rotate} ${tableName(row)}`} onClick={() => setConfirm({ action: "rotate", row })}>{t.rotate}</Button>
                <Button data-telemetry-id="b.management-table-qr.tableqrsection.3" variant="quiet" disabled={busy} aria-label={`${t.revoke} ${tableName(row)}`} onClick={() => setConfirm({ action: "revoke", row })}>{t.revoke}</Button>
              </>}
          </div></td>
        </tr>)}</tbody>
      </table>}
    </PanelView>

    <ModalDialog open={confirm !== undefined} title={confirm?.action === "rotate" ? t.rotateTitle : t.revokeTitle} onClose={() => setConfirm(undefined)}>
      {confirm && <>
        <p>{tableName(confirm.row)}</p>
        <p>{confirm.action === "rotate" ? t.rotateBody : t.revokeBody}</p>
        <div className="mtq__dialog-actions">
          <Button data-telemetry-id="b.management-table-qr.tableqrsection.4" variant="quiet" onClick={() => setConfirm(undefined)}>{t.cancel}</Button>
          <Button data-telemetry-id="b.management-table-qr.tableqrsection.5" variant="primary" onClick={() => void run(confirm.row, confirm.action)}>
            {confirm.action === "rotate" ? t.confirmRotate : t.confirmRevoke}
          </Button>
        </div>
      </>}
    </ModalDialog>

    <ModalDialog open={shown !== undefined} title={t.codeTitle} onClose={() => setShown(undefined)}>
      {shown && <>
        <div className="mtq__label">
          <h3>{tableName(shown.row)}</h3>
          <QrCodeSvg text={shown.url} label={`${t.qrLabel}: ${tableName(shown.row)}`} />
          <p>{t.scanHint}</p>
        </div>
        <p className="mtq__warning">{t.codeWarning}</p>
        <div className="mtq__dialog-actions">
          <Button data-telemetry-id="b.management-table-qr.tableqrsection.6" variant="primary" onClick={() => window.print()}>{t.print}</Button>
          <Button data-telemetry-id="b.management-table-qr.tableqrsection.7" variant="quiet" onClick={() => setShown(undefined)}>{t.close}</Button>
        </div>
      </>}
    </ModalDialog>
  </div>;
}
