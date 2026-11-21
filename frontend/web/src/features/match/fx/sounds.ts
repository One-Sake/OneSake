import { getFxSettings } from './fxSettings';

// Short, quiet match sounds, all synthesized with the Web Audio API (like the dice sound):
// no audio files, no third-party sounds. The AudioContext is only created after the first
// click or key press (browser rule), so nothing plays before the player interacted.

export type SoundName =
  | 'draw'
  | 'play'
  | 'event'
  | 'attack'
  | 'hit'
  | 'block'
  | 'counter'
  | 'ko'
  | 'life'
  | 'don'
  | 'turnMine'
  | 'turnTheirs'
  | 'win'
  | 'lose'
  | 'tick';

let context: AudioContext | null = null;
let master: GainNode | null = null;
let noise: AudioBuffer | null = null;
let unlocked = false;
const lastPlayed = new Map<SoundName, number>();

function unlock(): void {
  if (unlocked) {
    return;
  }

  unlocked = true;
  try {
    const AudioContextClass =
      window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioContextClass) {
      return;
    }

    context = new AudioContextClass();
    master = context.createGain();
    master.connect(context.destination);
  } catch {
    context = null;
  }
}

if (typeof window !== 'undefined') {
  // The first user gesture unlocks audio; until then every sound is skipped silently.
  window.addEventListener('pointerdown', unlock, { once: true, capture: true });
  window.addEventListener('keydown', unlock, { once: true, capture: true });
}

// True when sound may play right now (unlocked, not muted, volume > 0).
export function soundReady(): boolean {
  const settings = getFxSettings();
  return Boolean(context && master) && !settings.soundMuted && settings.volume > 0;
}

function noiseBuffer(audio: AudioContext): AudioBuffer {
  if (!noise) {
    const length = Math.floor(audio.sampleRate * 0.5);
    noise = audio.createBuffer(1, length, audio.sampleRate);
    const data = noise.getChannelData(0);
    for (let index = 0; index < length; index += 1) {
      data[index] = Math.random() * 2 - 1;
    }
  }

  return noise;
}

// A tone with a quick attack and an exponential decay.
function tone(
  audio: AudioContext,
  out: AudioNode,
  at: number,
  frequency: number,
  duration: number,
  volume: number,
  type: OscillatorType = 'sine',
  endFrequency?: number,
): void {
  const oscillator = audio.createOscillator();
  oscillator.type = type;
  oscillator.frequency.setValueAtTime(frequency, at);
  if (endFrequency) {
    oscillator.frequency.exponentialRampToValueAtTime(endFrequency, at + duration);
  }

  const gain = audio.createGain();
  gain.gain.setValueAtTime(0.0001, at);
  gain.gain.exponentialRampToValueAtTime(volume, at + 0.008);
  gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
  oscillator.connect(gain).connect(out);
  oscillator.start(at);
  oscillator.stop(at + duration + 0.02);
}

// Filtered noise (whoosh, crumble, card swish).
function hiss(
  audio: AudioContext,
  out: AudioNode,
  at: number,
  duration: number,
  volume: number,
  filterType: BiquadFilterType,
  frequency: number,
  endFrequency?: number,
): void {
  const source = audio.createBufferSource();
  source.buffer = noiseBuffer(audio);
  const filter = audio.createBiquadFilter();
  filter.type = filterType;
  filter.Q.value = 1.2;
  filter.frequency.setValueAtTime(frequency, at);
  if (endFrequency) {
    filter.frequency.exponentialRampToValueAtTime(endFrequency, at + duration);
  }

  const gain = audio.createGain();
  gain.gain.setValueAtTime(0.0001, at);
  gain.gain.exponentialRampToValueAtTime(volume, at + Math.min(0.03, duration / 3));
  gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
  source.connect(filter).connect(gain).connect(out);
  source.start(at, Math.random() * 0.3);
  source.stop(at + duration + 0.02);
}

const SOUNDS: Record<SoundName, (audio: AudioContext, out: AudioNode, now: number) => void> = {
  draw: (audio, out, now) => hiss(audio, out, now, 0.09, 0.25, 'highpass', 2500),
  play: (audio, out, now) => {
    tone(audio, out, now, 170, 0.16, 0.5, 'sine', 70);
    hiss(audio, out, now, 0.05, 0.18, 'bandpass', 1800);
  },
  event: (audio, out, now) => {
    [660, 880, 1320].forEach((frequency, index) => tone(audio, out, now + index * 0.06, frequency, 0.32, 0.18, 'triangle'));
  },
  attack: (audio, out, now) => hiss(audio, out, now, 0.26, 0.45, 'bandpass', 380, 2200),
  hit: (audio, out, now) => {
    tone(audio, out, now, 110, 0.22, 0.7, 'sine', 45);
    hiss(audio, out, now, 0.12, 0.35, 'lowpass', 900);
  },
  block: (audio, out, now) => {
    tone(audio, out, now, 520, 0.28, 0.16, 'triangle');
    tone(audio, out, now, 785, 0.22, 0.1, 'square');
  },
  counter: (audio, out, now) => tone(audio, out, now, 480, 0.18, 0.28, 'sine', 980),
  ko: (audio, out, now) => {
    [0, 0.05, 0.11].forEach((delay) => hiss(audio, out, now + delay, 0.12, 0.3, 'lowpass', 1400 - delay * 4000));
    tone(audio, out, now, 220, 0.35, 0.25, 'triangle', 80);
  },
  life: (audio, out, now) => {
    hiss(audio, out, now, 0.04, 0.22, 'highpass', 3000);
    hiss(audio, out, now + 0.07, 0.05, 0.18, 'highpass', 2200);
  },
  don: (audio, out, now) => {
    tone(audio, out, now, 1250, 0.18, 0.16, 'sine');
    tone(audio, out, now + 0.03, 1880, 0.14, 0.1, 'sine');
  },
  turnMine: (audio, out, now) => {
    tone(audio, out, now, 523, 0.2, 0.22, 'triangle');
    tone(audio, out, now + 0.12, 784, 0.32, 0.24, 'triangle');
  },
  turnTheirs: (audio, out, now) => tone(audio, out, now, 330, 0.3, 0.15, 'sine'),
  win: (audio, out, now) => {
    [523, 659, 784, 1047].forEach((frequency, index) => tone(audio, out, now + index * 0.11, frequency, 0.45, 0.22, 'triangle'));
  },
  lose: (audio, out, now) => {
    [392, 311, 262].forEach((frequency, index) => tone(audio, out, now + index * 0.18, frequency, 0.5, 0.2, 'sine'));
  },
  tick: (audio, out, now) => tone(audio, out, now, 1100, 0.05, 0.12, 'square'),
};

export function playSound(name: SoundName): void {
  if (!soundReady() || !context || !master) {
    return;
  }

  const now = performance.now();
  // The same sound several times within a few milliseconds is played once.
  if (now - (lastPlayed.get(name) ?? 0) < 45) {
    return;
  }

  lastPlayed.set(name, now);
  try {
    if (context.state === 'suspended') {
      void context.resume().catch(() => undefined);
    }

    // Curve: the slider feels linear to the ear.
    master.gain.value = getFxSettings().volume ** 2;
    SOUNDS[name](context, master, context.currentTime + 0.005);
  } catch {
    // Audio is optional.
  }
}
