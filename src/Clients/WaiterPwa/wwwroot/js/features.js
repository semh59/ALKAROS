// ALKAROS Waiter PWA — per-deployment Garson feature flags (V1-WTR-045,
// carved out alongside the bill.js extraction). Small enough that the
// plan's own file listing never named it, but a real shared dependency:
// bill.js, product-sheet.js and several sheets all read a flag before
// showing or hiding something, and none of them should import a state
// reader from waiter-app.js itself.

import { state } from './state.js';
import { apiUrl, api } from './api.js';

// V1-SET-004: fetched once per session start - a deployment's feature
// set does not change mid-shift, so nothing re-fetches this later.
export async function loadFeatures() {
  const result = await api(apiUrl('/runtime-configuration'));
  state.features = result.ok && result.data && result.data.garsonFeatures ? result.data.garsonFeatures : {};
}

// Missing (fetch failed, older server, key not yet in the response)
// reads as enabled - the same fail-open default GarsonFeatureToggles
// itself uses server-side, so a network hiccup never silently hides a
// feature that is actually still on.
export function featureEnabled(name) {
  return state.features[name] !== false;
}
