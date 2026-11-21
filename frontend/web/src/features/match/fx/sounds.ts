// Match sounds. Placeholder until the sound commit: every call is silent.
export type SoundName = 'turnMine' | 'turnTheirs' | 'win' | 'lose';

export function playSound(name: SoundName): void {
  void name;
}
