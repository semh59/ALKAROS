/** A stable ref callback: the control takes focus once when it appears and is not pulled back on later renders. */
export const focusOnMount = (element: HTMLElement | null) => element?.focus();
