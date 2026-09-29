import type { ComponentType } from "react";
import { ClosingSection } from "../management-closing";
import { StockSection } from "../management-stock";
import { managementText } from "../../strings";

export interface ManagementSectionProps {
  capabilities: ReadonlySet<string>;
}

export interface ManagementSection {
  id: string;
  label: string;
  /** The section is listed only for a session that holds this capability. */
  requiredCapability: string;
  component: ComponentType<ManagementSectionProps>;
}

/** Later sections (V1-RMD-446..451) are added here; the shell needs no other change. */
export const managementSections: readonly ManagementSection[] = [
  { id: "closing", label: managementText.closing.tab, requiredCapability: "reports.view", component: ClosingSection },
  { id: "stock", label: managementText.stock.tab, requiredCapability: "reports.view", component: StockSection },
];
