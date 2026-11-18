// Small inline SVG icons for the lobby. They use currentColor, so CSS controls the color.

export function DiceIcon() {
  return (
    <svg aria-hidden="true" fill="none" height="22" viewBox="0 0 24 24" width="22">
      <rect height="16" rx="3.5" stroke="currentColor" strokeWidth="1.8" width="16" x="4" y="4" />
      <circle cx="9" cy="9" fill="currentColor" r="1.4" />
      <circle cx="15" cy="9" fill="currentColor" r="1.4" />
      <circle cx="12" cy="12" fill="currentColor" r="1.4" />
      <circle cx="9" cy="15" fill="currentColor" r="1.4" />
      <circle cx="15" cy="15" fill="currentColor" r="1.4" />
    </svg>
  );
}

export function CopyIcon() {
  return (
    <svg aria-hidden="true" fill="none" height="16" viewBox="0 0 24 24" width="16">
      <rect height="12" rx="2" stroke="currentColor" strokeWidth="2" width="11" x="9" y="9" />
      <path d="M5 15V6a2 2 0 0 1 2-2h8" stroke="currentColor" strokeLinecap="round" strokeWidth="2" />
    </svg>
  );
}

export function RefreshIcon() {
  return (
    <svg aria-hidden="true" fill="none" height="18" viewBox="0 0 24 24" width="18">
      <path
        d="M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2"
      />
    </svg>
  );
}

export function SparkIcon() {
  return (
    <svg aria-hidden="true" fill="none" height="18" viewBox="0 0 24 24" width="18">
      <path
        d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M18 6l-2.5 2.5M8.5 15.5 6 18"
        stroke="currentColor"
        strokeLinecap="round"
        strokeWidth="2"
      />
    </svg>
  );
}
