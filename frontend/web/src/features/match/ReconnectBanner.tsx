import { formatCountdown, useCountdown } from './serverClock';

interface ReconnectBannerProps {
  // Local time when our connection dropped (null = connected).
  lostAt: number | null;
  // How long the server keeps our seat (from the last snapshot).
  graceMs: number | null | undefined;
  showReconnected: boolean;
}

// Banner at the top of the board while our own connection is gone, and "reconnected" afterwards.
export function ReconnectBanner({ lostAt, graceMs, showReconnected }: ReconnectBannerProps) {
  // Local clock: the grace period started when our own connection dropped.
  const left = useCountdown(lostAt !== null && graceMs != null ? lostAt + graceMs : null, 'local');

  if (lostAt !== null) {
    return (
      <div className="match-reconnect-banner is-lost" role="alert">
        <span className="match-reconnect-banner__spinner" aria-hidden="true" />
        Verbindung verloren – verbinde neu …{left !== null ? ` (${formatCountdown(left)})` : ''}
      </div>
    );
  }

  if (showReconnected) {
    return (
      <div className="match-reconnect-banner is-restored" role="status">
        Wieder verbunden ✓
      </div>
    );
  }

  return null;
}
