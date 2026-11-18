import { useEffect, useState } from 'react';

import { CopyIcon } from '../../components/ui/Icons';

// Which big action the lobby offers right now. Derived from the room state in LobbyPanel.
export type LobbyActionMode = 'queue' | 'searching' | 'waiting' | 'start' | 'board';

interface PrimaryLobbyActionProps {
  mode: LobbyActionMode;
  disabled: boolean;
  createDisabled: boolean;
  pending: boolean;
  blockedReason: string;
  joinedRoomCode: string;
  copyStatus: '' | 'copied' | 'failed';
  queueStartedAt: number | null;
  onStartQuickMatch: () => void;
  onCancelQuickMatch: () => void;
  onCreateRoom: () => void;
  onStartMatch: () => void;
  onOpenBoard: () => void;
  onCopyRoomCode: () => void;
}

const BUTTON_LABELS: Record<LobbyActionMode, string> = {
  queue: '⚔ Quick Match',
  searching: 'Suche Gegner…',
  waiting: 'Warte auf Gegner…',
  start: '⚔ Match starten',
  board: 'Zum Board',
};

function formatElapsed(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${String(seconds).padStart(2, '0')}`;
}

// Ticks once per second while a Quick Match search is running.
function SearchTimer({ startedAt }: { startedAt: number }) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const intervalId = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(intervalId);
  }, []);

  return <span className="lobby-primary-button__timer">{formatElapsed(now - startedAt)}</span>;
}

export function PrimaryLobbyAction({
  blockedReason,
  copyStatus,
  createDisabled,
  disabled,
  joinedRoomCode,
  mode,
  onCancelQuickMatch,
  onCopyRoomCode,
  onCreateRoom,
  onOpenBoard,
  onStartMatch,
  onStartQuickMatch,
  pending,
  queueStartedAt,
}: PrimaryLobbyActionProps) {
  const handleClick = (): void => {
    if (mode === 'queue') {
      onStartQuickMatch();
    } else if (mode === 'start') {
      onStartMatch();
    } else if (mode === 'board') {
      onOpenBoard();
    }
  };

  const isWaitingMode = mode === 'waiting' || mode === 'searching';
  const isDisabled = disabled || isWaitingMode;
  const label = pending && !isWaitingMode ? 'Bitte warten…' : BUTTON_LABELS[mode];

  return (
    <div className="lobby-action">
      <div className="lobby-action__row">
        <button
          aria-live={mode === 'searching' ? 'polite' : undefined}
          className={`lobby-primary-button lobby-primary-button--${mode}`}
          disabled={isDisabled}
          onClick={handleClick}
          type="button"
        >
          <span>
            {label}
            {mode === 'searching' && queueStartedAt !== null && (
              <>
                {' '}
                <SearchTimer startedAt={queueStartedAt} />
              </>
            )}
          </span>
        </button>

        {mode === 'queue' && (
          <button
            className="lobby-secondary-button lobby-action__secondary"
            disabled={createDisabled}
            onClick={onCreateRoom}
            type="button"
          >
            Tisch eröffnen
          </button>
        )}

        {joinedRoomCode && (
          <button
            aria-label={`Room-Code ${joinedRoomCode} kopieren`}
            className="lobby-room-chip"
            onClick={onCopyRoomCode}
            title="Room-Code kopieren"
            type="button"
          >
            <span className="lobby-room-chip__label">
              {copyStatus === 'copied' ? 'Kopiert' : 'Code'}
            </span>
            <strong>{joinedRoomCode}</strong>
            <CopyIcon />
          </button>
        )}
      </div>

      {copyStatus === 'failed' && (
        <p className="lobby-action__reason lobby-action__reason--error" role="alert">
          Clipboard is not available in this browser context.
        </p>
      )}
      {blockedReason && (
        <p className="lobby-action__reason" role="status">
          {blockedReason}
        </p>
      )}
      {mode === 'searching' && (
        <button className="lobby-text-link lobby-action__cancel" onClick={onCancelQuickMatch} type="button">
          Abbrechen
        </button>
      )}
    </div>
  );
}
