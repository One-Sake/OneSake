// Soft tick for the last seconds of the own clock (Web Audio, like the dice sound - no audio file).
// Can be switched off; the choice is kept in localStorage.

const STORAGE_KEY = 'onesake.timerSound';
let audioContext: AudioContext | null = null;

export function isTimerSoundOn(): boolean {
  try {
    return window.localStorage.getItem(STORAGE_KEY) !== 'off';
  } catch {
    return true;
  }
}

export function setTimerSoundOn(on: boolean): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, on ? 'on' : 'off');
  } catch {
    // Not stored: the setting lasts until the page is reloaded.
  }
}

export function playTimerTick(urgent: boolean): void {
  try {
    if (!audioContext) {
      const AudioContextClass =
        window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!AudioContextClass) {
        return;
      }

      audioContext = new AudioContextClass();
    }

    const context = audioContext;
    if (context.state === 'suspended') {
      void context.resume().catch(() => undefined);
    }

    if (context.state !== 'running') {
      return;
    }

    const now = context.currentTime;
    const oscillator = context.createOscillator();
    oscillator.type = 'sine';
    oscillator.frequency.value = urgent ? 1320 : 990;
    const gain = context.createGain();
    gain.gain.setValueAtTime(0.08, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.08);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(now);
    oscillator.stop(now + 0.09);
  } catch {
    // Audio is optional.
  }
}
