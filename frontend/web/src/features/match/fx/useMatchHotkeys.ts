import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';

// Keyboard shortcuts on the board. They only call the existing handlers of the board, and only
// when no input field has the focus (chat, room code ...).

export interface HotkeyActions {
  canEndTurn: boolean;
  endTurn: () => void;
  canAttack: boolean;
  startAttack: () => void;
  canGiveDon: boolean;
  giveDon: () => void;
  canActivate: boolean;
  activate: () => void;
  cancel: () => void;
  // Tab: the next own Leader/Character is selected.
  selectNextFieldCard: () => boolean;
}

// End turn needs a second press or holding the key, so it does not happen by accident.
const HOLD_MS = 400;
const SECOND_PRESS_MS = 1500;

function isTyping(): boolean {
  const element = document.activeElement as HTMLElement | null;
  if (!element) {
    return false;
  }

  const tag = element.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || element.isContentEditable;
}

export function useMatchHotkeys(frameRef: RefObject<HTMLElement>, actions: HotkeyActions) {
  const actionsRef = useRef(actions);
  actionsRef.current = actions;
  const [endTurnArmed, setEndTurnArmed] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const holdTimer = useRef<number>();
  const armTimer = useRef<number>();
  const armed = useRef(false);

  useEffect(() => {
    function disarm(): void {
      armed.current = false;
      setEndTurnArmed(false);
      window.clearTimeout(armTimer.current);
    }

    function confirmEndTurn(): void {
      window.clearTimeout(holdTimer.current);
      holdTimer.current = undefined;
      disarm();
      if (actionsRef.current.canEndTurn) {
        actionsRef.current.endTurn();
      }
    }

    function clickDialogButton(): boolean {
      const frame = frameRef.current;
      const dialog = frame?.querySelector('.effect-choice-overlay__actions, .match-choice-bar');
      const button =
        dialog?.querySelector<HTMLButtonElement>('button.is-primary:not(:disabled)') ??
        dialog?.querySelector<HTMLButtonElement>('button:not(:disabled)');
      if (!button) {
        return false;
      }

      button.click();
      return true;
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (isTyping() || event.ctrlKey || event.metaKey || event.altKey || !frameRef.current?.isConnected) {
        return;
      }

      const current = actionsRef.current;
      const key = event.key;

      if (key === '?') {
        event.preventDefault();
        setHelpOpen((open) => !open);
        return;
      }

      if (key === 'Escape') {
        setHelpOpen(false);
        disarm();
        current.cancel();
        return;
      }

      if (key === ' ') {
        if (!current.canEndTurn) {
          return;
        }

        event.preventDefault();
        if (event.repeat) {
          return;
        }

        if (armed.current) {
          confirmEndTurn();
          return;
        }

        armed.current = true;
        setEndTurnArmed(true);
        holdTimer.current = window.setTimeout(confirmEndTurn, HOLD_MS);
        armTimer.current = window.setTimeout(disarm, SECOND_PRESS_MS);
        return;
      }

      const lower = key.toLowerCase();
      if (lower === 'a' && current.canAttack) {
        event.preventDefault();
        current.startAttack();
      } else if (lower === 'd' && current.canGiveDon) {
        event.preventDefault();
        current.giveDon();
      } else if (lower === 'e' && current.canActivate) {
        event.preventDefault();
        current.activate();
      } else if (/^[1-9]$/.test(key)) {
        // Focus a hand card: Enter (or a click) then uses it as usual.
        const cards = frameRef.current?.querySelectorAll<HTMLElement>('.match-hand .match-card-view');
        const card = cards?.[Number(key) - 1];
        if (card) {
          event.preventDefault();
          card.focus();
        }
      } else if (key === 'Tab' && !event.shiftKey) {
        if (current.selectNextFieldCard()) {
          event.preventDefault();
        }
      } else if (key === 'Enter') {
        const focused = document.activeElement;
        // A focused button (e.g. a hand card) gets its normal Enter click.
        if (!(focused instanceof HTMLButtonElement) && clickDialogButton()) {
          event.preventDefault();
        }
      }
    }

    function handleKeyUp(event: KeyboardEvent): void {
      // Released before 400 ms: the second press decides.
      if (event.key === ' ' && holdTimer.current) {
        window.clearTimeout(holdTimer.current);
        holdTimer.current = undefined;
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      window.clearTimeout(holdTimer.current);
      window.clearTimeout(armTimer.current);
    };
  }, [frameRef]);

  return { endTurnArmed, helpOpen, closeHelp: () => setHelpOpen(false) };
}
