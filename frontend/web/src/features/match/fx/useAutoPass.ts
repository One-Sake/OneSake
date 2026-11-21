import { useEffect, useState } from 'react';

import { useFxSettings } from './fxSettings';

interface AutoPassOptions {
  // The open prompt of the local player (null when there is nothing to decide).
  promptId: string;
  promptKind: string;
  // A [Blocker] on our field can block right now (the field is public information).
  canBlock: boolean;
  // A hand card can be used as a counter right now (the hand is secret!).
  canCounter: boolean;
  submit: (option: string) => void;
}

const BLOCK_DELAY_MS = 700;
// Counter: a random short wait, so the opponent cannot tell an empty hand from a decision.
const COUNTER_DELAY_MIN_MS = 800;
const COUNTER_DELAY_MAX_MS = 1600;

// Answers prompts where there is objectively nothing to choose:
// - Block step without a possible blocker (only public information) - always.
// - Counter step without a usable counter card - only with "Auto-Pass Counter" switched on by the
//   player, because passing at once would tell the opponent that the hand holds no counter.
// Returns a short text for the hint line while it waits.
export function useAutoPass({ canBlock, canCounter, promptId, promptKind, submit }: AutoPassOptions): string {
  const { autoPassCounter } = useFxSettings();
  const [notice, setNotice] = useState('');

  useEffect(() => {
    setNotice('');
    let delay = -1;
    let option = '';

    if (promptKind === 'BLOCK_STEP' && !canBlock) {
      delay = BLOCK_DELAY_MS;
      option = 'NO_BLOCK';
    } else if (promptKind === 'COUNTER_STEP' && !canCounter && autoPassCounter) {
      delay = COUNTER_DELAY_MIN_MS + Math.random() * (COUNTER_DELAY_MAX_MS - COUNTER_DELAY_MIN_MS);
      option = 'PASS';
    }

    if (!promptId || delay < 0) {
      return undefined;
    }

    setNotice(option === 'PASS' ? 'Auto-Pass: kein Counter möglich …' : 'Kein Blocker – weiter …');
    const timer = window.setTimeout(() => {
      setNotice('');
      submit(option);
    }, delay);
    return () => window.clearTimeout(timer);
    // The submit function changes every render; the prompt decides.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [promptId, promptKind, canBlock, canCounter, autoPassCounter]);

  return notice;
}
