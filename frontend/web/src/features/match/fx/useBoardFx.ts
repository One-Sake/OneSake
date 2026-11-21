import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';

import type { ChoicePromptDto, GameStateDto, LogEventDto } from '../../../types/realtime';
import { boxOf, isOnBoard, type Box } from './fxDom';
import { logFx, promptFx, snapshotFx, type FxEnv } from './fxPlayers';
import { FxQueue } from './fxQueue';
import { getFxSettings } from './fxSettings';

interface BoardFxOptions {
  frameRef: RefObject<HTMLElement>;
  layerRef: RefObject<HTMLElement>;
  gameState: GameStateDto | null;
  logEvents: LogEventDto[];
  currentPrompt: ChoicePromptDto | null;
}

// Watches the game state, the log and our prompts, and queues animations for what happened.
// The board always shows the new state at once; the animations are only a layer on top.
export function useBoardFx({ currentPrompt, frameRef, gameState, layerRef, logEvents }: BoardFxOptions): FxQueue {
  const [queue] = useState(() => new FxQueue());
  const previousState = useRef<GameStateDto | null>(null);
  const boxes = useRef(new Map<string, Box>());
  const seenLogCount = useRef(logEvents.length);
  const seenPromptId = useRef(currentPrompt?.choiceId ?? '');

  function env(): FxEnv | null {
    const frame = frameRef.current;
    const layer = layerRef.current;
    if (!frame || !layer) {
      return null;
    }

    return {
      frame,
      layer,
      queue,
      settings: getFxSettings(),
      viewerId: gameState?.viewerPlayerId ?? '',
      previousBoxes: boxes.current,
    };
  }

  // A click anywhere or an own key press: everything that is still animating jumps to the end.
  useEffect(() => {
    const skip = () => queue.skipAll();
    window.addEventListener('pointerdown', skip, true);
    window.addEventListener('keydown', skip, true);
    return () => {
      window.removeEventListener('pointerdown', skip, true);
      window.removeEventListener('keydown', skip, true);
      queue.dispose();
    };
  }, [queue]);

  // Layout effect: runs after React put the new state into the DOM, before the browser paints,
  // so a card that is about to fly in can be hidden without flickering.
  useLayoutEffect(() => {
    const previous = previousState.current;
    previousState.current = gameState;
    const context = env();

    if (context && previous && gameState && previous.roomCode === gameState.roomCode && previous !== gameState) {
      try {
        snapshotFx(previous, gameState, context);
      } catch (error) {
        console.warn('[OneSake] fx snapshot failed', error);
      }
    }

    // Remember where every card is now, for cards that leave the DOM with the next state.
    const layer = layerRef.current;
    const frame = frameRef.current;
    if (layer && frame) {
      const next = new Map<string, Box>();
      frame.querySelectorAll<HTMLElement>('[data-fx-card]').forEach((element) => {
        const box = boxOf(layer, element);
        if (box && element.dataset.fxCard && isOnBoard(element)) {
          next.set(element.dataset.fxCard, box);
        }
      });
      boxes.current = next;
    }
    // Only a new game state starts animations.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameState]);

  useEffect(() => {
    if (logEvents.length < seenLogCount.current) {
      // A new room or match: the log started again.
      seenLogCount.current = 0;
    }

    const fresh = logEvents.slice(seenLogCount.current);
    seenLogCount.current = logEvents.length;
    const context = env();
    if (!context) {
      return;
    }

    fresh.forEach((event) => {
      try {
        logFx(event, gameState, context);
      } catch (error) {
        console.warn('[OneSake] fx log failed', error);
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [logEvents]);

  useEffect(() => {
    const id = currentPrompt?.choiceId ?? '';
    if (!currentPrompt || id === seenPromptId.current || currentPrompt.playerId !== gameState?.viewerPlayerId) {
      seenPromptId.current = id;
      return;
    }

    seenPromptId.current = id;
    const context = env();
    if (context) {
      promptFx(currentPrompt, context);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentPrompt]);

  return queue;
}
