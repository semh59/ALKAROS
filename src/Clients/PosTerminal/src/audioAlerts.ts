// V1-RMD-384/V1-RMD-386 (module-by-module UI audit round 2, P1/P2 - competitor
// comparison/field reality): every real commercial KDS/order-management screen (Toast
// KDS, QSR Automations, online-ordering aggregator dashboards) sounds an audible alert
// the moment new, actionable work lands - staff running the floor or the kitchen have
// their hands full and are not staring at a screen most of a shift. Shared here so
// KitchenOperationsWorkspace and OnlineOperationsWorkspace (both poll-driven queues of
// work that needs a timely human response) do not each carry their own copy.
//
// Synthesized via the Web Audio API rather than shipping an audio asset - two short
// tones is enough to cut through a loud kitchen/counter without needing a sample file
// this codebase would otherwise have to bundle and licence.
export function playNewItemChime() {
  try {
    const AudioContextClass = window.AudioContext
      || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioContextClass) return;
    const context = new AudioContextClass();
    const playTone = (frequency: number, startOffset: number) => {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = "sine";
      oscillator.frequency.value = frequency;
      gain.gain.setValueAtTime(0.0001, context.currentTime + startOffset);
      gain.gain.exponentialRampToValueAtTime(0.3, context.currentTime + startOffset + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + startOffset + 0.18);
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(context.currentTime + startOffset);
      oscillator.stop(context.currentTime + startOffset + 0.2);
    };
    playTone(880, 0);
    playTone(1175, 0.14);
    window.setTimeout(() => void context.close(), 500);
  } catch {
    // A browser that refuses to synthesize audio (autoplay policy before any user
    // gesture, an unsupported engine) must never break the screen itself - the visual
    // item still appears either way, this is a convenience on top.
  }
}
