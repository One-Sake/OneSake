import { useEffect, useRef } from 'react';
import type { FormEvent } from 'react';

import type { ChatMessageDto, LogEventDto, PlayerStateDto } from '../../types/realtime';
import { formatPhase } from './matchFormatters';

interface MatchFeedProps {
  canSendChat: boolean;
  chatInput: string;
  chatMessages: ChatMessageDto[];
  joinedRoomCode: string;
  logEvents: LogEventDto[];
  onChatInputChange: (value: string) => void;
  onSendChat: (event: FormEvent) => void;
  pending: boolean;
  players: PlayerStateDto[];
}

type FeedItem =
  | { kind: 'log'; key: string; ts: number; event: LogEventDto }
  | { kind: 'chat'; key: string; ts: number; message: ChatMessageDto };

// How close (in px) to the bottom still counts as "at the bottom".
const STICK_TO_BOTTOM_PX = 24;

// Match log and table chat in one chronological list.
export function MatchFeed({
  canSendChat,
  chatInput,
  chatMessages,
  joinedRoomCode,
  logEvents,
  onChatInputChange,
  onSendChat,
  pending,
  players,
}: MatchFeedProps) {
  const listRef = useRef<HTMLDivElement>(null);
  const isAtBottom = useRef(true);

  // The server also writes every chat message as a CHAT log event; skip those to avoid duplicates.
  const items: FeedItem[] = [
    ...logEvents
      .filter((event) => event.type !== 'CHAT')
      .map((event): FeedItem => ({ kind: 'log', key: `log-${event.seq}`, ts: event.tsUnixMs, event })),
    ...chatMessages.map(
      (message, index): FeedItem => ({
        kind: 'chat',
        key: `chat-${message.senderId}-${message.tsUnixMs}-${index}`,
        ts: message.tsUnixMs,
        message,
      }),
    ),
  ].sort((a, b) => a.ts - b.ts);

  // Follow new entries, but only if the user has not scrolled up to read older ones.
  useEffect(() => {
    const list = listRef.current;
    if (list && isAtBottom.current) {
      list.scrollTop = list.scrollHeight;
    }
  }, [items.length]);

  function handleScroll(): void {
    const list = listRef.current;
    if (list) {
      isAtBottom.current = list.scrollHeight - list.scrollTop - list.clientHeight < STICK_TO_BOTTOM_PX;
    }
  }

  function getSenderName(senderId: string): string {
    return players.find((player) => player.playerId === senderId)?.displayName || senderId;
  }

  return (
    <section className="match-feed" aria-label="Match log and chat">
      <div ref={listRef} className="match-feed__list" onScroll={handleScroll} role="log">
        {items.length === 0 && <p className="match-feed__empty">No events yet.</p>}
        {items.map((item) =>
          item.kind === 'log' ? (
            <p key={item.key} className="match-feed__log">
              <span>
                T{item.event.turnNumber} · {formatPhase(item.event.phase)} ·{' '}
              </span>
              {item.event.text}
            </p>
          ) : (
            <p key={item.key} className="match-feed__chat">
              <strong>{getSenderName(item.message.senderId)}:</strong> {item.message.text}
            </p>
          ),
        )}
      </div>
      <form className="match-feed__form" onSubmit={onSendChat}>
        <input
          aria-label="Chat message"
          onChange={(event) => onChatInputChange(event.target.value)}
          placeholder="Write a message..."
          value={chatInput}
        />
        <button disabled={!joinedRoomCode || pending || !canSendChat} type="submit">
          Send
        </button>
      </form>
    </section>
  );
}
