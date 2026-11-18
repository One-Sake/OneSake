import type { RoomSummaryDto } from '../../types/realtime';
import { RefreshIcon } from './LobbyIcons';
import { RoomTile } from './RoomTile';

interface ServerBrowserProps {
  rooms: RoomSummaryDto[];
  canJoin: boolean;
  canCreate: boolean;
  joinedRoomCode: string;
  pending: boolean;
  onCreateRoom: () => void;
  onJoinRoom: (roomCode: string) => void;
  onRefresh: () => void;
}

export function ServerBrowser({
  canCreate,
  canJoin,
  joinedRoomCode,
  onCreateRoom,
  onJoinRoom,
  onRefresh,
  pending,
  rooms,
}: ServerBrowserProps) {
  return (
    <section className="lobby-tables" aria-labelledby="lobby-tables-title">
      <header className="lobby-tables__header">
        <h2 id="lobby-tables-title" className="lobby-heading">
          Offene Tische
          <span className="lobby-count-badge">{rooms.length}</span>
        </h2>
        <button
          aria-label="Liste aktualisieren"
          className="lobby-icon-button"
          disabled={pending}
          onClick={onRefresh}
          title="Liste aktualisieren"
          type="button"
        >
          <RefreshIcon />
        </button>
      </header>

      {rooms.length === 0 ? (
        <div className="lobby-tables__empty">
          <div className="card-back-stack" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          <p>Noch keine offenen Tische – eröffne den ersten.</p>
          <button
            className="lobby-secondary-button"
            disabled={!canCreate || pending || Boolean(joinedRoomCode)}
            onClick={onCreateRoom}
            type="button"
          >
            Tisch eröffnen
          </button>
        </div>
      ) : (
        <ul className="lobby-tables__list">
          {rooms.map((room) => (
            <RoomTile
              key={room.roomCode}
              isJoined={room.roomCode === joinedRoomCode}
              joinDisabled={
                !canJoin ||
                pending ||
                room.playerCount >= room.maxPlayers ||
                Boolean(joinedRoomCode)
              }
              onJoinRoom={onJoinRoom}
              room={room}
            />
          ))}
        </ul>
      )}

      <small className="lobby-tables__hint">Leere Tische werden nach 5 Minuten gelöscht.</small>
    </section>
  );
}
