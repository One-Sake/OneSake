import { CopyIcon } from './LobbyIcons';

// Which big action the lobby offers right now. Derived from the room state in LobbyPanel.
export type LobbyActionMode = 'create' | 'waiting' | 'start' | 'board';

interface PrimaryLobbyActionProps {
  mode: LobbyActionMode;
  disabled: boolean;
  pending: boolean;
  blockedReason: string;
  joinedRoomCode: string;
  copyStatus: '' | 'copied' | 'failed';
  onCreateRoom: () => void;
  onStartMatch: () => void;
  onOpenBoard: () => void;
  onCopyRoomCode: () => void;
}

const BUTTON_LABELS: Record<LobbyActionMode, string> = {
  create: '⚔ Tisch eröffnen',
  waiting: 'Warte auf Gegner…',
  start: '⚔ Match starten',
  board: 'Zum Board',
};

export function PrimaryLobbyAction({
  blockedReason,
  copyStatus,
  disabled,
  joinedRoomCode,
  mode,
  onCopyRoomCode,
  onCreateRoom,
  onOpenBoard,
  onStartMatch,
  pending,
}: PrimaryLobbyActionProps) {
  const handleClick = (): void => {
    if (mode === 'create') {
      onCreateRoom();
    } else if (mode === 'start') {
      onStartMatch();
    } else if (mode === 'board') {
      onOpenBoard();
    }
  };

  const isDisabled = disabled || mode === 'waiting';
  const label = pending && mode !== 'waiting' ? 'Bitte warten…' : BUTTON_LABELS[mode];

  return (
    <div className="lobby-action">
      <div className="lobby-action__row">
        <button
          className={`lobby-primary-button lobby-primary-button--${mode}`}
          disabled={isDisabled}
          onClick={handleClick}
          type="button"
        >
          <span>{label}</span>
        </button>

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
    </div>
  );
}
