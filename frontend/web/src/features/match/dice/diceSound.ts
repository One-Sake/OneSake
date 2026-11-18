// Short "clack" for dice hitting the table: a filtered noise burst via the Web Audio API.
// No audio file needed. Browsers may block audio until the user clicked once - then it stays silent.

let audioContext: AudioContext | null = null;
let noiseBuffer: AudioBuffer | null = null;
let lastPlayedAt = 0;

function getContext(): AudioContext | null {
  try {
    if (!audioContext) {
      const AudioContextClass =
        window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!AudioContextClass) {
        return null;
      }

      audioContext = new AudioContextClass();
    }

    if (audioContext.state === 'suspended') {
      void audioContext.resume().catch(() => undefined);
    }

    return audioContext;
  } catch {
    return null;
  }
}

function getNoise(context: AudioContext): AudioBuffer {
  if (!noiseBuffer) {
    const length = Math.floor(context.sampleRate * 0.06);
    noiseBuffer = context.createBuffer(1, length, context.sampleRate);
    const data = noiseBuffer.getChannelData(0);
    for (let index = 0; index < length; index += 1) {
      data[index] = Math.random() * 2 - 1;
    }
  }

  return noiseBuffer;
}

// strength = impact speed from the simulation (about 1.5 .. 15).
export function playDiceImpact(strength: number): void {
  const context = getContext();
  if (!context || context.state !== 'running') {
    return;
  }

  const now = context.currentTime;
  // Many corner contacts happen in the same few milliseconds; play them as one sound.
  if (now - lastPlayedAt < 0.03) {
    return;
  }

  lastPlayedAt = now;
  const volume = Math.min(0.35, (strength / 14) * 0.35);
  const source = context.createBufferSource();
  source.buffer = getNoise(context);

  const filter = context.createBiquadFilter();
  filter.type = 'bandpass';
  filter.frequency.value = 1400 + Math.min(strength, 15) * 90;
  filter.Q.value = 1.4;

  const gain = context.createGain();
  gain.gain.setValueAtTime(volume, now);
  gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.05);

  source.connect(filter).connect(gain).connect(context.destination);
  source.start(now);
  source.stop(now + 0.06);
}
