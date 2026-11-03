import { Button } from '../../components/ui/Button';
import type { RoomSummaryDto } from '../../types/realtime';
import { formatPhase } from './matchFormatters';

interface ServerBrowserProps {
  rooms: RoomSummaryDto[];
  canJoin: boolean;
  joinedRoomCode: string;
  pending: boolean;
  onJoinRoom: (roomCode: string) => void;
  onRefresh: () => void;
}

export function ServerBrowser({
  canJoin,
  joinedRoomCode,
  onJoinRoom,
  onRefresh,
  pending,
  rooms,
}: ServerBrowserProps) {
  return (
    <section className="match-setup-section match-server-browser" aria-label="Open servers">
      <div className="match-setup-card__header">
        <span>Servers Online</span>
        <strong className="match-room-code-chip">{rooms.length}</strong>
      </div>

      {rooms.length === 0 ? (
        <p className="match-lobby-notice">No servers online. Create one to get started.</p>
      ) : (
        <ul className="match-server-list">
          {rooms.map((room) => {
            const isFull = room.playerCount >= room.maxPlayers;
            const isJoined = room.roomCode === joinedRoomCode;

            return (
              <li key={room.roomCode} className={isJoined ? 'is-joined' : ''}>
                <div className="match-server-list__info">
                  <strong>{room.roomCode}</strong>
                  <small>
                    {room.playerCount}/{room.maxPlayers} players · {formatPhase(room.phase)}
                    {room.playerNames.length > 0 ? ` · ${room.playerNames.join(', ')}` : ''}
                  </small>
                </div>
                <Button
                  disabled={!canJoin || pending || isFull || Boolean(joinedRoomCode)}
                  onClick={() => onJoinRoom(room.roomCode)}
                  variant="secondary"
                >
                  {isJoined ? 'Joined' : isFull ? 'Full' : 'Join'}
                </Button>
              </li>
            );
          })}
        </ul>
      )}

      <Button disabled={pending} onClick={onRefresh} variant="ghost">
        Refresh List
      </Button>
      <small className="match-server-browser__hint">Empty servers are deleted after 5 minutes.</small>
    </section>
  );
}
