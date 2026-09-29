import { useState } from "react";
import { StateMessage } from "../../design-system";
import { managementText } from "../../strings";
import { managementSections, type ManagementSection } from "./sections";
import "./management.css";

/** One "Yönetim" workspace that carries role-based sections; a section a session cannot use is not listed. */
export function ManagementArea({ capabilities, sections = managementSections }: {
  capabilities: ReadonlySet<string>;
  sections?: readonly ManagementSection[];
}) {
  const visible = sections.filter((section) => capabilities.has(section.requiredCapability));
  const [activeId, setActiveId] = useState<string>();
  if (visible.length === 0) {
    return <StateMessage tone="forbidden" title={managementText.noSectionTitle}><p>{managementText.noSectionBody}</p></StateMessage>;
  }
  const active = visible.find((section) => section.id === activeId) ?? visible[0]!;
  const Active = active.component;
  return <div className="management">
    <nav className="management__tabs" aria-label={managementText.sectionsLabel}>
      {visible.map((section) => <button
        key={section.id}
        type="button"
        className="management__tab"
        aria-current={section.id === active.id ? "page" : undefined}
        onClick={() => setActiveId(section.id)}
      >{section.label}</button>)}
    </nav>
    <Active capabilities={capabilities} />
  </div>;
}
