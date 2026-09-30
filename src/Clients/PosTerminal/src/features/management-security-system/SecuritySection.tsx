import type { ManagementSectionProps } from "../management/sections";
import type { SecurityClient } from "./api";
import { BacklogPanel, DiagnosticPanel } from "./DiagnosticBacklogPanels";
import { BackupPanel, MaintenancePanel } from "./MaintenanceBackupPanels";
import { RecoveryPanel } from "./RecoveryPanel";
import "./management-security-system.css";

export function SecuritySection({ client }: Partial<ManagementSectionProps> & { client?: SecurityClient }) {
  return <div className="msys">
    <RecoveryPanel client={client} />
    <MaintenancePanel client={client} />
    <BackupPanel client={client} />
    <BacklogPanel client={client} />
    <DiagnosticPanel client={client} />
  </div>;
}
