import type { ComponentType } from "react";
import { ClosingSection } from "../management-closing";
import { StockSection } from "../management-stock";
import { ProductionSection, PurchasingSection } from "../management-purchasing-production";
import { MenusSection, RecipeCostSection } from "../management-menus-recipe-cost";
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
  { id: "purchasing", label: managementText.purchasing.tab, requiredCapability: "purchasing.manage", component: PurchasingSection },
  { id: "production", label: managementText.production.tab, requiredCapability: "production.manage", component: ProductionSection },
  { id: "menus", label: managementText.menus.tab, requiredCapability: "menu.manage", component: MenusSection },
  { id: "recipe-cost", label: managementText.recipeCost.tab, requiredCapability: "inventory.manage", component: RecipeCostSection },
];
