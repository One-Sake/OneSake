import type { RoomSummaryDto } from '../../types/realtime';
import { formatPhase } from './matchFormatters';

interface RoomTileProps {
  room: RoomSummaryDto;
  joinDisabled: boolean;
  isJoined: boolean;
  onJoinRoom: (roomCode: string) => void;
}

function getStatusLabel(room: RoomSummaryDto, isFull: boolean): string {
  const phase = formatPhase(room.phase);

  if (phase === 'Lobby') {
    return isFull ? 'Voll' : 'Wartet auf Gegner';
  }

  return phase === 'GameOver' ? 'Beendet' : 'Match läuft';
}

export function RoomTile({ isJoined, joinDisabled, onJoinRoom, room }: RoomTileProps) {
  const isFull = room.playerCount >= room.maxPlayers;
  const hostName = room.playerNames[0] || 'Unbekannter Pirat';
  const isOpen = formatPhase(room.phase) === 'Lobby' && !isFull;

  return (
    <li className={`room-tile ${isJoined ? 'is-joined' : ''}`}>
      <div className="room-tile__info">
        <strong className="room-tile__host">{hostName}</strong>
        <span className="room-tile__meta">
          <span className="room-tile__code">{room.roomCode}</span>
          <span className={`room-tile__status ${isOpen ? 'is-open' : ''}`}>
            {getStatusLabel(room, isFull)}
          </span>
        </span>
      </div>
      <span className="room-tile__players" aria-label={`${room.playerCount} von ${room.maxPlayers} Spielern`}>
        {room.playerCount}/{room.maxPlayers}
      </span>
      <button
        className="lobby-secondary-button room-tile__join"
        disabled={joinDisabled}
        onClick={() => onJoinRoom(room.roomCode)}
        type="button"
      >
        {isJoined ? 'Dein Tisch' : isFull ? 'Voll' : 'Beitreten'}
      </button>
    </li>
  );
}
