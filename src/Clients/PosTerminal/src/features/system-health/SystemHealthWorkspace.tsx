import { Button, StateMessage } from "../../design-system";
import {
  backupStatusLabels,
  healthStatusLabel,
  type KitchenBackup,
  type KitchenHealthSnapshot,
} from "../kitchen-operations";
import "./system-health.css";

export type SystemHealthState = "loading" | "ready" | "error" | "offline" | "unauthorized";

export interface SystemHealthWorkspaceProps {
  state: SystemHealthState;
  health: KitchenHealthSnapshot | null;
  backups: readonly KitchenBackup[];
  onRefresh: () => void | Promise<void>;
  errorMessage?: string;
  lastUpdated?: string;
}

function bytesToGigabytes(value: number): string {
  return `${(value / 1_073_741_824).toFixed(1)} GB`;
}

export function SystemHealthWorkspace({ state, health, backups, onRefresh, errorMessage, lastUpdated }: SystemHealthWorkspaceProps) {
  if (state === "loading") {
    return <div className="system-health system-health--state" aria-busy="true"><StateMessage tone="info" title="Sistem sağlığı yükleniyor"><p>Son snapshot ve yedek durumu alınıyor…</p></StateMessage></div>;
  }
  if (state === "unauthorized") {
    return <div className="system-health system-health--state"><StateMessage tone="unauthorized" title="Yönetici oturumu gerekli"><p>Sistem sağlığı yalnızca yönetici rolüne açıktır.</p></StateMessage></div>;
  }
  if (state === "offline") {
    return <div className="system-health system-health--state"><StateMessage tone="offline" title="Bağlantı yok"><p>Güncel sağlık verisi alınamıyor.</p><Button onClick={onRefresh}>Tekrar dene</Button></StateMessage></div>;
  }
  if (state === "error") {
    return <div className="system-health system-health--state"><StateMessage tone="error" title="Sağlık verisi alınamadı"><p>{errorMessage ?? "Beklenmeyen bir hata oluştu."}</p><Button onClick={onRefresh}>Yeniden dene</Button></StateMessage></div>;
  }

  const latestBackup = backups[0];
  return <section className="system-health" aria-label="Sistem sağlığı ve yedek durumu">
    <header className="system-health__header">
      <div><span className="system-health__kicker">YÖNETİM / SİSTEM</span><h2>Sistem sağlığı</h2><p>{lastUpdated ? `Son güncelleme ${lastUpdated}` : "Veritabanı, disk ve yedekleme durumu"}</p></div>
      <Button variant="secondary" onClick={() => void onRefresh()}>Yenile</Button>
    </header>

    {health ? <dl className="system-health__grid">
      <div><dt>Veritabanı</dt><dd className={`system-health__badge system-health__badge--${health.databaseStatus.toLowerCase()}`}>{healthStatusLabel(health.databaseStatus)}</dd></div>
      <div><dt>Disk</dt><dd className={`system-health__badge system-health__badge--${health.diskStatus.toLowerCase()}`}>{healthStatusLabel(health.diskStatus)}</dd></div>
      <div><dt>Son yedek</dt><dd className={`system-health__badge system-health__badge--${health.lastBackupStatus.toLowerCase()}`}>{healthStatusLabel(health.lastBackupStatus)}</dd></div>
      <div><dt>Boş disk alanı</dt><dd>{bytesToGigabytes(health.freeDiskBytes)}</dd></div>
      <div><dt>Veritabanı boyutu</dt><dd>{bytesToGigabytes(health.databaseSizeBytes)}</dd></div>
    </dl> : <StateMessage tone="stale" title="Sağlık verisi yok"><p>Son snapshot alınamadı.</p></StateMessage>}

    {latestBackup && <div className={`system-health__backup system-health__backup--${latestBackup.status.toLowerCase()}`}>
      <span>Son yedekleme</span>
      <strong>{backupStatusLabels[latestBackup.status]}</strong>
      {latestBackup.errorMessage && <p>Yedekleme hatası kaydedildi; ayrıntı sistem günlüğünde.</p>}
    </div>}
  </section>;
}
